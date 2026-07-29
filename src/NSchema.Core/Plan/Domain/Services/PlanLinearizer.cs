using System.Collections.Frozen;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Indexes;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Scripts;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;
using NSchema.Plan.Domain.Views;

namespace NSchema.Plan.Domain.Services;

/// <summary>
/// Walks the structured diff and produces a migration plan.
/// </summary>
internal sealed class PlanLinearizer : IPlanLinearizer
{
    public IReadOnlyList<MigrationAction> Linearize(DatabaseDiff diff, PlanDependencies dependencies, DialectCapabilities capabilities)
    {
        var actions = new List<MigrationAction>();
        EmitExtensions(diff, actions);
        foreach (var schema in diff.Schemas)
        {
            EmitSchema(schema, actions);
        }

        // Tables and views are each emitted in one cross-schema pass: their create/drop order is governed by a
        // dependency sort (created after what they need, dropped before it), which the per-schema walk above
        // cannot express — a foreign key or a view body may reach into another schema.
        EmitTables(diff, dependencies, capabilities, actions);
        EmitViews(diff, dependencies, actions);

        actions = [.. MigrationActionOrdering.Order(actions)];

        // Deployment scripts bookend the plan: pre scripts run before everything, post scripts after.
        return [.. ScriptActions(diff, DeploymentPhase.Pre), .. actions, .. ScriptActions(diff, DeploymentPhase.Post)];
    }

    private static IEnumerable<ExecuteScript> ScriptActions(DatabaseDiff diff, DeploymentPhase phase) =>
        diff.DeploymentScripts.Where(s => s.Phase == phase).Select(s => new ExecuteScript(s));

    /// <summary>
    /// Emits the table actions across every schema, in the order their foreign keys require: a table is created
    /// after the tables it references and dropped before them. Tables may legally point at each other, and no
    /// order satisfies a cycle — so any foreign key the order leaves unsatisfied is taken out of the way first,
    /// unless the dialect keeps its keys on the tables that declare them.
    /// </summary>
    private static void EmitTables(
        DatabaseDiff diff,
        PlanDependencies dependencies,
        DialectCapabilities capabilities,
        List<MigrationAction> actions)
    {
        var tables = diff.Schemas.SelectMany(schema => schema.Tables).ToList();

        var created = InCreationOrder([.. tables.Where(table => table.Kind != ChangeKind.Remove)], dependencies);
        var unfolded = capabilities.CanAlterForeignKeys ? UnsatisfiedOnCreate(created) : FrozenSet<MemberAddress>.Empty;
        foreach (var table in created)
        {
            EmitTable(table, actions, unfolded);
        }

        var dropped = InRemovalOrder([.. tables.Where(table => table.Kind == ChangeKind.Remove)], dependencies);
        if (capabilities.CanAlterForeignKeys)
        {
            foreach (var foreignKey in UnsatisfiedOnDrop(dropped, dependencies))
            {
                actions.Add(new DropForeignKey(foreignKey));
            }
        }
        foreach (var table in dropped)
        {
            EmitTable(table, actions, FrozenSet<MemberAddress>.Empty);
        }
    }

    /// <summary>
    /// The foreign keys a created table cannot carry inline, because the table they point at is created later in
    /// the same plan. They are added afterwards instead, once every table exists.
    /// </summary>
    private static IReadOnlySet<MemberAddress> UnsatisfiedOnCreate(IReadOnlyList<TableDiff> created)
    {
        var positions = Positions(created, table => new ObjectAddress(table.Schema, table.Name));

        return (from table in created
                let address = new ObjectAddress(table.Schema, table.Name)
                from foreignKey in InlineForeignKeys(table)
                let references = new ObjectAddress(foreignKey.References.Schema, foreignKey.References.Name)
                where positions.TryGetValue(references, out var referenced) && referenced > positions[address]
                select new MemberAddress(table.Schema, table.Name, foreignKey.Name)).ToHashSet();
    }

    /// <summary>The foreign keys a change carries on the table it creates; none when it creates nothing.</summary>
    private static IEnumerable<ForeignKey> InlineForeignKeys(TableDiff table) => table.IsAdd() ? table.Definition.ForeignKeys : [];

    /// <summary>
    /// The foreign keys the drop order leaves unsatisfied, held by a table dropped after the one it points at.
    /// Dropping the constraint first is free: both tables are on their way out.
    /// </summary>
    private static IReadOnlyList<MemberAddress> UnsatisfiedOnDrop(IReadOnlyList<TableDiff> dropped, PlanDependencies dependencies)
    {
        var positions = Positions(dropped, table => new ObjectAddress(table.Schema, table.RenamedFrom ?? table.Name));

        return [.. from table in dropped
                   let address = new ObjectAddress(table.Schema, table.RenamedFrom ?? table.Name)
                   from foreignKey in dependencies.ForeignKeysInto(address)
                   where positions.TryGetValue(foreignKey.Owner, out var holder) && holder > positions[address]
                   select foreignKey];
    }

    private static Dictionary<ObjectAddress, int> Positions(IReadOnlyList<TableDiff> tables, Func<TableDiff, ObjectAddress> address)
    {
        var positions = new Dictionary<ObjectAddress, int>();
        for (var i = 0; i < tables.Count; i++)
        {
            positions[address(tables[i])] = i;
        }

        return positions;
    }

    /// <summary>
    /// The objects in the order they can be created: each after the objects it requires.
    /// </summary>
    private static IReadOnlyList<T> InCreationOrder<T>(IReadOnlyList<T> objects, PlanDependencies dependencies)
        where T : ISchemaObjectDiff =>
        Ordered(objects, o => new ObjectAddress(o.Schema, o.Name), dependencies.Requires);

    /// <summary>
    /// The objects in the order they can be dropped: each before the objects it is required by. Addressed under
    /// the name they currently carry, which is the one the current side knows them by.
    /// </summary>
    private static IReadOnlyList<T> InRemovalOrder<T>(IReadOnlyList<T> objects, PlanDependencies dependencies)
        where T : ISchemaObjectDiff =>
        Ordered(objects, o => new ObjectAddress(o.Schema, o.RenamedFrom ?? o.Name), dependencies.RequiredBy);

    /// <summary>
    /// The instance-level ordering layered on top of the fixed action-type order: where two objects of the same
    /// kind change together and one requires the other, the type order cannot separate them — this can.
    /// </summary>
    /// <remarks>
    /// Only an edge between two objects being changed together orders anything; everything else the graph knows
    /// about is left where it is. Cycles are broken rather than reported: mutual foreign keys are legal, and a
    /// view cycle can only come from a dependency NSchema inferred, which is too weak a thing to fail a plan on.
    /// </remarks>
    private static IReadOnlyList<T> Ordered<T>(
        IReadOnlyList<T> objects,
        Func<T, ObjectAddress> address,
        Func<ObjectAddress, IReadOnlyCollection<ObjectAddress>> edges
    ) where T : ISchemaObjectDiff =>
        objects.OrderedByDependencies(
            address,
            o => edges(address(o)),
            o => $"{o.Schema}.{o.Name}",
            allowCycles: true);

    /// <summary>
    /// Emits the view actions across every schema. <see cref="CreateView"/>s are appended in dependency order and
    /// <see cref="DropView"/>s in the reverse, so that once the stable type sort above gathers each kind into its
    /// band, a view is created after the views it reads and dropped before them.
    /// </summary>
    private static void EmitViews(DatabaseDiff diff, PlanDependencies dependencies, List<MigrationAction> actions)
    {
        var creates = new List<ViewDiff>();
        var drops = new List<ViewDiff>();

        foreach (var schema in diff.Schemas)
        {
            foreach (var view in schema.Views)
            {
                // A rename that accompanies a recreate is subsumed by it: the drop removes the old name and the
                // definition recreates the view under the new one, so no RenameView is emitted.
                if (view.RenamedFrom is not null && !view.RequiresRecreate)
                {
                    actions.Add(new RenameView(new ObjectAddress(view.Schema, view.RenamedFrom), view.Name, view.IsMaterialized));
                }

                if (view.Kind == ChangeKind.Remove)
                {
                    drops.Add(view);
                }
                else if (view.RequiresRecreate)
                {
                    // A materialized view's body change (or a view <-> materialized-view conversion) can't be
                    // replaced in place, so it is both dropped and recreated; its indexes rebuild with it.
                    drops.Add(view);
                    creates.Add(view);
                }
                else if (view.Definition is not null)
                {
                    // A plain view's body change, applied as CREATE OR REPLACE.
                    creates.Add(view);
                }

                if (view.Kind != ChangeKind.Remove && view.Comment is not null)
                {
                    actions.Add(new SetViewComment(new ObjectAddress(view.Schema, view.Name), view.Comment.Old, view.Comment.New, view.IsMaterialized));
                }

                // In-place index changes on a materialized view whose body is unchanged; on a create/recreate the
                // indexes ride along on the definition instead. Index drops sort before RenameView, so on a
                // renamed view they run while it still carries its old name.
                foreach (var index in view.Indexes)
                {
                    actions.Add(IndexAction(view.Schema, view.Name, view.RenamedFrom ?? view.Name, index));
                }
            }
        }

        foreach (var view in InCreationOrder(creates, dependencies))
        {
            if (view.Definition is { } definition)
            {
                actions.Add(new CreateView(view.Schema, definition));
            }
        }

        // A renamed view recreating is dropped under its old name (no rename precedes the drop), and a converting
        // view is dropped as what it currently is — IsMaterialized reflects the desired side, so a flip drops with
        // the old materialization.
        foreach (var view in InRemovalOrder(drops, dependencies))
        {
            actions.Add(new DropView(new ObjectAddress(view.Schema, view.RenamedFrom ?? view.Name), view.Materialized?.Old ?? view.IsMaterialized));
        }
    }

    /// <summary>
    /// Emits the root-level extension actions. Ordering (extensions created/updated before schemas, dropped after
    /// everything) is governed by the priority table above; this just maps each <see cref="ExtensionDiff"/> to its
    /// action(s).
    /// </summary>
    private static void EmitExtensions(DatabaseDiff diff, List<MigrationAction> actions)
    {
        foreach (var extension in diff.Extensions)
        {
            switch (extension.Kind)
            {
                case ChangeKind.Add when extension.IsAdd():
                    actions.Add(new CreateExtension(extension.Definition));
                    break;

                case ChangeKind.Remove:
                    actions.Add(new DropExtension(extension.Name));
                    break;

                default: // Modify
                    if (extension.Version is not null)
                    {
                        actions.Add(new AlterExtension(extension.Name, extension.Version.Old, extension.Version.New));
                    }
                    break;
            }

            if (extension.Kind != ChangeKind.Remove && extension.Comment is not null)
            {
                actions.Add(new SetExtensionComment(extension.Name, extension.Comment.Old, extension.Comment.New));
            }
        }
    }

    private static void EmitSchema(SchemaDiff schema, List<MigrationAction> actions)
    {
        switch (schema.Kind)
        {
            case ChangeKind.Add:
                actions.Add(new CreateSchema(schema.Name));
                EmitSchemaAttributes(schema, actions);
                EmitEnums(schema, actions);
                EmitSequences(schema, actions);
                EmitRoutines(schema, actions);
                EmitDomains(schema, actions);
                EmitCompositeTypes(schema, actions);
                break;

            case ChangeKind.Remove:
                // Drop everything the schema contains before the schema itself, rather than relying on a
                // provider-specific DROP SCHEMA CASCADE. The final type-sort orders these object drops ahead of
                // the DropSchema, and tables and views are emitted by their own cross-schema passes.
                EmitEnums(schema, actions);
                EmitSequences(schema, actions);
                EmitRoutines(schema, actions);
                EmitDomains(schema, actions);
                EmitCompositeTypes(schema, actions);
                actions.Add(new DropSchema(schema.Name));
                break;

            default: // Modify, or a null-Kind container whose tables changed.
                if (schema.RenamedFrom is not null)
                {
                    actions.Add(new RenameSchema(schema.RenamedFrom, schema.Name));
                }
                EmitSchemaAttributes(schema, actions);
                EmitEnums(schema, actions);
                EmitSequences(schema, actions);
                EmitRoutines(schema, actions);
                EmitDomains(schema, actions);
                EmitCompositeTypes(schema, actions);
                break;
        }
    }

    /// <summary>
    /// Emits one schema-object kind: create on add, drop on remove, and on modify a rename (when one is
    /// recorded) followed by the kind's own modify actions; a comment change trails every non-remove.
    /// </summary>
    private static void EmitObjects<T, TDefinition>(
        IReadOnlyList<T> objects,
        List<MigrationAction> actions,
        Func<T, TDefinition?> definition,
        Func<T, TDefinition, MigrationAction> create,
        Func<T, MigrationAction> drop,
        Func<T, SqlIdentifier, MigrationAction> rename,
        Func<T, ValueChange<string>, MigrationAction> comment,
        Action<T> modify
    ) where T : ISchemaObjectDiff where TDefinition : class
    {
        // The rename and comment builders receive the narrowed value rather than re-reading it: the guard is
        // here, so the lambda should not have to restate that it holds.
        foreach (var diff in objects)
        {
            switch (diff.Kind)
            {
                // An add always carries the definition to create from; one without is not a change we can emit.
                case ChangeKind.Add when definition(diff) is { } toCreate:
                    actions.Add(create(diff, toCreate));
                    break;

                case ChangeKind.Add:
                    break;

                case ChangeKind.Remove:
                    actions.Add(drop(diff));
                    break;

                default: // Modify
                    if (diff.RenamedFrom is { } renamedFrom)
                    {
                        actions.Add(rename(diff, renamedFrom));
                    }
                    modify(diff);
                    break;
            }

            if (diff.Kind != ChangeKind.Remove && diff.Comment is { } changedComment)
            {
                actions.Add(comment(diff, changedComment));
            }
        }
    }

    private static void EmitRoutines(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Routines, actions,
            r => r.Definition,
            (r, definition) => new CreateRoutine(r.Schema, definition),
            r => new DropRoutine(new ObjectAddress(r.Schema, r.Name), r.RoutineKind),
            (r, renamedFrom) => new RenameRoutine(new ObjectAddress(r.Schema, renamedFrom), r.Name, r.RoutineKind),
            (r, comment) => new SetRoutineComment(new ObjectAddress(r.Schema, r.Name), comment.Old, comment.New, r.RoutineKind),
            r =>
            {
                // A signature (or kind) change recreates (a replace under different arguments would create a
                // separate overload); a definition-only change replaces in place.
                if (r.Definition is { } definition)
                {
                    actions.Add(r.RequiresRecreate
                        ? new RecreateRoutine(r.Schema, definition)
                        : new CreateRoutine(r.Schema, definition));
                }
            });

    private static void EmitDomains(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Domains, actions,
            d => d.Definition,
            (d, definition) => new CreateDomain(d.Schema, definition),
            d => new DropDomain(new ObjectAddress(d.Schema, d.Name)),
            (d, renamedFrom) => new RenameDomain(new ObjectAddress(d.Schema, renamedFrom), d.Name),
            (d, comment) => new SetDomainComment(new ObjectAddress(d.Schema, d.Name), comment.Old, comment.New),
            d =>
            {
                // A base-type change can't be altered in place, so it recreates (default/not-null/checks rebuild
                // with the definition); otherwise each facet is altered in place.
                if (d.RequiresRecreate)
                {
                    if (d.Definition is { } definition)
                    {
                        actions.Add(new RecreateDomain(d.Schema, definition));
                    }
                    return;
                }

                if (d.Default is not null)
                {
                    actions.Add(new AlterDomainDefault(new ObjectAddress(d.Schema, d.Name), d.Default.Old, d.Default.New));
                }
                if (d.NotNull is not null)
                {
                    actions.Add(new AlterDomainNotNull(new ObjectAddress(d.Schema, d.Name), d.NotNull.New));
                }
                foreach (var check in d.Checks)
                {
                    if (check.Kind == ChangeKind.Remove)
                    {
                        actions.Add(new DropDomainCheck(new MemberAddress(d.Schema, d.Name, check.Name)));
                    }
                    else if (check.Definition is { } definition)
                    {
                        actions.Add(new AddDomainCheck(new ObjectAddress(d.Schema, d.Name), definition));
                    }
                }
            });

    private static void EmitCompositeTypes(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.CompositeTypes, actions,
            t => t.Definition,
            (t, definition) => new CreateCompositeType(t.Schema, definition),
            t => new DropCompositeType(new ObjectAddress(t.Schema, t.Name)),
            (t, renamedFrom) => new RenameCompositeType(new ObjectAddress(t.Schema, renamedFrom), t.Name),
            (t, comment) => new SetCompositeTypeComment(new ObjectAddress(t.Schema, t.Name), comment.Old, comment.New),
            t =>
            {
                // Every field change applies in place: a matched field whose type differs is retyped, a missing
                // field is dropped, a new field is added. There is no recreate.
                foreach (var field in t.Fields)
                {
                    actions.Add(field switch
                    {
                        { Kind: ChangeKind.Remove } => new DropCompositeField(new MemberAddress(t.Schema, t.Name, field.Name)),
                        { Kind: ChangeKind.Modify, Type: { Old: { } oldType, New: { } newType } } =>
                            new AlterCompositeFieldType(new MemberAddress(t.Schema, t.Name, field.Name), oldType, newType),
                        { Definition: { } definition } => new AddCompositeField(new ObjectAddress(t.Schema, t.Name), definition),
                        _ => throw new NotSupportedException(
                            $"Cannot linearize composite field change {field.Kind} on '{t.Schema}.{t.Name}'."),
                    });
                }
            });

    private static void EmitEnums(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Enums, actions,
            e => e.Definition,
            (e, definition) => new CreateEnum(e.Schema, definition),
            e => new DropEnum(new ObjectAddress(e.Schema, e.Name)),
            (e, renamedFrom) => new RenameEnum(new ObjectAddress(e.Schema, renamedFrom), e.Name),
            (e, comment) => new SetEnumComment(new ObjectAddress(e.Schema, e.Name), comment.Old, comment.New),
            e =>
            {
                // Additions are emitted in list order so each anchor exists when its addition runs (the
                // stable priority sort preserves this). A removal/reorder has no AddedValues — it cannot be
                // planned, and the always-on EnumValueRemovalPolicy fails the run before execution.
                foreach (var addition in e.AddedValues)
                {
                    actions.Add(new AddEnumValue(new ObjectAddress(e.Schema, e.Name), addition.Value, addition.Before, addition.After));
                }
            });

    private static void EmitSequences(SchemaDiff schema, List<MigrationAction> actions) =>
        EmitObjects(schema.Sequences, actions,
            s => s.Definition,
            (s, definition) => new CreateSequence(s.Schema, definition),
            s => new DropSequence(new ObjectAddress(s.Schema, s.Name)),
            (s, renamedFrom) => new RenameSequence(new ObjectAddress(s.Schema, renamedFrom), s.Name),
            (s, comment) => new SetSequenceComment(new ObjectAddress(s.Schema, s.Name), comment.Old, comment.New),
            s =>
            {
                if (s.Options is { Old: { } oldOptions, New: { } newOptions })
                {
                    actions.Add(new AlterSequence(new ObjectAddress(s.Schema, s.Name), oldOptions, newOptions));
                }
            });

    private static void EmitSchemaAttributes(SchemaDiff schema, List<MigrationAction> actions)
    {
        if (schema.Comment is not null)
        {
            actions.Add(new SetSchemaComment(schema.Name, schema.Comment.Old, schema.Comment.New));
        }

        foreach (var grant in schema.Grants)
        {
            actions.Add(grant.Kind == ChangeKind.Add
                ? new GrantSchemaUsage(schema.Name, grant.Role)
                : new RevokeSchemaUsage(schema.Name, grant.Role));
        }
    }

    /// <summary>
    /// Emits one table's actions.
    /// </summary>
    /// <param name="table">The change to emit.</param>
    /// <param name="actions">The action list being built.</param>
    /// <param name="unfolded">
    /// The foreign keys that cannot ride the CREATE TABLE, and are added separately afterwards instead.
    /// </param>
    private static void EmitTable(TableDiff table, List<MigrationAction> actions, IReadOnlySet<MemberAddress> unfolded)
    {
        switch (table.Kind)
        {
            case ChangeKind.Add when table.IsAdd():
                // The columns and every table constraint are created inline by CREATE TABLE (carried on
                // Definition); only indexes, triggers, comments and grants arrive as separate actions.
                actions.Add(new CreateTable(table.Schema, WithoutForeignKeys(table, unfolded)));
                if (table.Comment is { } tableComment)
                {
                    actions.Add(new SetTableComment(new ObjectAddress(table.Schema, table.Name), tableComment.Old, tableComment.New));
                }
                foreach (var column in table.Columns)
                {
                    if (column.Comment is { } columnComment)
                    {
                        actions.Add(new SetColumnComment(new MemberAddress(table.Schema, table.Name, column.Name), columnComment.Old, columnComment.New));
                    }
                }
                EmitConstraints(table, actions, unfolded);
                EmitIndexes(table, actions);
                EmitTriggers(table, actions);
                EmitGrants(table, actions);
                break;

            case ChangeKind.Remove:
                actions.Add(new DropTable(new ObjectAddress(table.Schema, table.Name)));
                break;

            default: // Modify
                if (table.RenamedFrom is not null)
                {
                    actions.Add(new RenameTable(new ObjectAddress(table.Schema, table.RenamedFrom), table.Name));
                }
                if (table.Comment is not null)
                {
                    actions.Add(new SetTableComment(new ObjectAddress(table.Schema, table.Name), table.Comment.Old, table.Comment.New));
                }
                foreach (var column in table.Columns)
                {
                    EmitColumn(table, column, actions);
                }
                EmitConstraints(table, actions, unfolded);
                EmitIndexes(table, actions);
                EmitTriggers(table, actions);
                EmitGrants(table, actions);
                break;
        }
    }

    private static void EmitColumn(TableDiff table, ColumnDiff column, List<MigrationAction> actions)
    {
        switch (column.Kind)
        {
            case ChangeKind.Add:
                // A required column with a matched backfill migration is decomposed: added nullable, backfilled
                // by the migration SQL, then tightened to NOT NULL. Identity and generated columns fill
                // themselves and a default covers existing rows, so those adds keep their declared shape.
                if (column is { MigrationScript: { } backfill, Definition: { IsNullable: false, DefaultExpression: null, IsIdentity: false, GeneratedExpression: null } })
                {
                    // The declared column belongs to the project tree, so the nullable variant is a copy.
                    var nullable = column.Definition.Clone();
                    nullable.IsNullable = true;
                    actions.Add(new AddColumn(new ObjectAddress(table.Schema, table.Name), nullable));
                    actions.Add(new ExecuteScript(backfill));
                    actions.Add(new AlterColumn(new ObjectAddress(table.Schema, table.Name), column.Definition, Nullability: new ValueChange<bool>(true, false)));
                }
                else
                {
                    actions.Add(new AddColumn(new ObjectAddress(table.Schema, table.Name), column.Definition));
                    if (column.MigrationScript is { } migration)
                    {
                        actions.Add(new ExecuteScript(migration));
                    }
                }
                if (column.Comment is not null)
                {
                    actions.Add(new SetColumnComment(new MemberAddress(table.Schema, table.Name, column.Name), column.Comment.Old, column.Comment.New));
                }
                break;

            case ChangeKind.Remove:
                actions.Add(new DropColumn(new ObjectAddress(table.Schema, table.Name), column.Definition));
                break;

            case ChangeKind.Modify:
                if (column.RenamedFrom is not null)
                {
                    actions.Add(new RenameColumn(new MemberAddress(table.Schema, table.Name, column.RenamedFrom), column.Name));
                }
                if (column.Type is not null)
                {
                    // A matched migration prepares the data for the cast; the priority table runs it first.
                    if (column.MigrationScript is { } prep)
                    {
                        actions.Add(new ExecuteScript(prep));
                    }
                }
                if (column.Type is not null || column.Nullability is not null)
                {
                    actions.Add(new AlterColumn(new ObjectAddress(table.Schema, table.Name), column.Definition, column.Type, column.Nullability));
                }
                if (column.Default is not null)
                {
                    actions.Add(new SetColumnDefault(new MemberAddress(table.Schema, table.Name, column.Name), column.Default.Old, column.Default.New));
                }
                if (column.Generated is not null)
                {
                    actions.Add(new SetColumnGenerated(new MemberAddress(table.Schema, table.Name, column.Name), column.Generated.Old, column.Generated.New));
                }
                if (column.Identity is not null)
                {
                    actions.Add(new AlterIdentitySequence(new MemberAddress(table.Schema, table.Name, column.Name), column.Identity.Old, column.Identity.New));
                }
                if (column.Comment is not null)
                {
                    actions.Add(new SetColumnComment(new MemberAddress(table.Schema, table.Name, column.Name), column.Comment.Old, column.Comment.New));
                }
                break;
            default: throw new NotSupportedException($"Cannot linearize column change {column.Kind}.");
        }
    }

    // Drops and revokes are sorted before RenameTable, so on a renamed table they run while it still carries
    // its old name; every action from the rename onward targets the new name.
    private static void EmitConstraints(TableDiff table, List<MigrationAction> actions, IReadOnlySet<MemberAddress> unfolded)
    {
        var preRenameName = table.RenamedFrom ?? table.Name;

        // A newly-created table carries its constraints inline on CreateTable's definition, so their adds fold
        // into the CREATE TABLE and only comment changes still arrive as separate actions.
        var foldAdds = table.Kind == ChangeKind.Add;

        EmitConstraintKind(table.PrimaryKeys, actions, _ => foldAdds,
            pk => pk.Definition,
            (pk, definition) => new AddPrimaryKey(new ObjectAddress(table.Schema, table.Name), definition),
            pk => new DropPrimaryKey(new MemberAddress(table.Schema, preRenameName, pk.Name)),
            (pk, comment) => new SetConstraintComment(new MemberAddress(table.Schema, table.Name, pk.Name), comment.Old, comment.New));

        // A foreign key left out of the CREATE TABLE is added here instead, once the table it points at exists.
        EmitConstraintKind(table.ForeignKeys, actions,
            fk => foldAdds && !unfolded.Contains(new MemberAddress(table.Schema, table.Name, fk.Name)),
            fk => fk.Definition,
            (fk, definition) => new AddForeignKey(new ObjectAddress(table.Schema, table.Name), definition),
            fk => new DropForeignKey(new MemberAddress(table.Schema, preRenameName, fk.Name)),
            (fk, comment) => new SetConstraintComment(new MemberAddress(table.Schema, table.Name, fk.Name), comment.Old, comment.New));

        EmitConstraintKind(table.UniqueConstraints, actions, _ => foldAdds,
            uq => uq.Definition,
            (uq, definition) => new AddUniqueConstraint(new ObjectAddress(table.Schema, table.Name), definition),
            uq => new DropUniqueConstraint(new MemberAddress(table.Schema, preRenameName, uq.Name)),
            (uq, comment) => new SetConstraintComment(new MemberAddress(table.Schema, table.Name, uq.Name), comment.Old, comment.New));

        EmitConstraintKind(table.Checks, actions, _ => foldAdds,
            ck => ck.Definition,
            (ck, definition) => new AddCheckConstraint(new ObjectAddress(table.Schema, table.Name), definition),
            ck => new DropCheckConstraint(new MemberAddress(table.Schema, preRenameName, ck.Name)),
            (ck, comment) => new SetConstraintComment(new MemberAddress(table.Schema, table.Name, ck.Name), comment.Old, comment.New));

        EmitConstraintKind(table.ExclusionConstraints, actions, _ => foldAdds,
            ex => ex.Definition,
            (ex, definition) => new AddExclusionConstraint(new ObjectAddress(table.Schema, table.Name), definition),
            ex => new DropExclusionConstraint(new MemberAddress(table.Schema, preRenameName, ex.Name)),
            (ex, comment) => new SetConstraintComment(new MemberAddress(table.Schema, table.Name, ex.Name), comment.Old, comment.New));
    }

    /// <summary>
    /// The table to create, less the foreign keys that cannot ride it. The definition belongs to the project
    /// tree, so trimming one is a copy.
    /// </summary>
    private static Table WithoutForeignKeys(TableDiff table, IReadOnlySet<MemberAddress> unfolded)
    {
        if (!table.IsAdd() || unfolded.Count == 0)
        {
            return table.Definition!;
        }

        var trimmed = table.Definition.Clone();
        foreach (var foreignKey in trimmed.ForeignKeys.Where(fk => unfolded.Contains(new MemberAddress(table.Schema, table.Name, fk.Name))).ToList())
        {
            trimmed.ForeignKeys.Remove(foreignKey);
        }

        return trimmed;
    }

    /// <summary>
    /// Emits one constraint kind: an add's matched migration first (it prepares the data the constraint depends
    /// on — de-duplication, backfill — and the priority table runs every data migration before the constraint
    /// adds), then the change itself. A constraint Modify is always a comment-only change. When <paramref
    /// name="foldAdd"/> holds the table is being created, so the add is inlined into the CREATE TABLE and skipped.
    /// </summary>
    private static void EmitConstraintKind<T, TDefinition>(
        IReadOnlyList<T> constraints,
        List<MigrationAction> actions,
        Func<T, bool> foldAdd,
        Func<T, TDefinition?> definition,
        Func<T, TDefinition, MigrationAction> add,
        Func<T, MigrationAction> drop,
        Func<T, ValueChange<string>, MigrationAction> comment
    ) where T : IMigratableDiff where TDefinition : class
    {
        foreach (var constraint in constraints)
        {
            if (constraint.Kind == ChangeKind.Add && foldAdd(constraint))
            {
                continue;
            }

            EmitConstraintMigration(constraint.Kind, constraint.MigrationScript, actions);
            switch (constraint.Kind)
            {
                case ChangeKind.Add when definition(constraint) is { } toAdd:
                    actions.Add(add(constraint, toAdd));
                    break;
                case ChangeKind.Remove:
                    actions.Add(drop(constraint));
                    break;
                // A member Modify is a comment change and nothing else, so one without a comment has nothing to emit.
                case ChangeKind.Modify when constraint.Comment is { } changed:
                    actions.Add(comment(constraint, changed));
                    break;
            }
        }
    }

    private static void EmitConstraintMigration(ChangeKind kind, ChangeScript? migration, List<MigrationAction> actions)
    {
        if (kind == ChangeKind.Add && migration is { } script)
        {
            actions.Add(new ExecuteScript(script));
        }
    }

    private static void EmitIndexes(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var index in table.Indexes)
        {
            actions.Add(IndexAction(table.Schema, table.Name, table.RenamedFrom ?? table.Name, index));
        }
    }

    /// <summary>
    /// The action for one index change on <paramref name="owner"/>. Drops target <paramref name="preRenameName"/>,
    /// since they sort before the owner's rename and so run while it still carries its old name.
    /// </summary>
    private static MigrationAction IndexAction(SqlIdentifier schema, SqlIdentifier owner, SqlIdentifier preRenameName, IndexDiff index) => index switch
    {
        { Kind: ChangeKind.Add, Definition: { } definition } => new CreateIndex(new ObjectAddress(schema, owner), definition),
        { Kind: ChangeKind.Remove } => new DropIndex(new MemberAddress(schema, preRenameName, index.Name)),
        { Comment: { } comment } => new SetIndexComment(new MemberAddress(schema, owner, index.Name), comment.Old, comment.New),
        _ => throw new NotSupportedException($"Cannot linearize index change {index.Kind} on '{schema}.{owner}'."),
    };


    private static void EmitTriggers(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var trigger in table.Triggers)
        {
            actions.Add(trigger switch
            {
                { Kind: ChangeKind.Add, Definition: { } definition } => new CreateTrigger(new ObjectAddress(table.Schema, table.Name), definition),
                { Kind: ChangeKind.Remove } => new DropTrigger(new MemberAddress(table.Schema, table.RenamedFrom ?? table.Name, trigger.Name)),
                { Comment: { } comment } => new SetTriggerComment(new MemberAddress(table.Schema, table.Name, trigger.Name), comment.Old, comment.New),
                _ => throw new NotSupportedException($"Cannot linearize trigger change {trigger.Kind} on '{table.Schema}.{table.Name}'."),
            });
        }
    }

    private static void EmitGrants(TableDiff table, List<MigrationAction> actions)
    {
        foreach (var grant in table.Grants)
        {
            if (grant.Privileges is not { } privileges)
            {
                continue;
            }

            actions.Add(grant.Kind == ChangeKind.Add
                ? new GrantTablePrivileges(new ObjectAddress(table.Schema, table.Name), grant.Role, privileges)
                : new RevokeTablePrivileges(new ObjectAddress(table.Schema, table.RenamedFrom ?? table.Name), grant.Role, privileges));
        }
    }
}
