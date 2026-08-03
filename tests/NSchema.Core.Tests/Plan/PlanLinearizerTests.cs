using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Triggers;
using NSchema.Diff.Domain.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Enums;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Indexes;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;
using NSchema.Plan.Domain.Views;

namespace NSchema.Tests.Plan;

/// <summary>
/// Exercises <see cref="PlanLinearizer"/> in isolation: structured <see cref="DatabaseDiff"/> nodes go in,
/// the emitted <see cref="MigrationAction"/>s come out. The comparer is deliberately not involved, so these tests pin
/// the diff-node → action mapping and the priority ordering that are the linearizer's sole responsibility.
/// </summary>
public sealed class PlanLinearizerTests
{
    private readonly PlanLinearizer _linearizer = new();
    private readonly MigrationSides _sides = new();

    private IReadOnlyList<MigrationAction> Linearize(params SchemaDiff[] schemas) =>
        _linearizer.Linearize(new DatabaseDiff(schemas), _sides.Dependencies, DialectCapabilities.Standard);

    // -- diff node builders ----------------------------------------------------

    private static SchemaDiff SchemaNode(
        string name,
        ChangeKind? kind = null,
        SqlIdentifier? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<TableDiff>? tables = null,
        IReadOnlyList<ViewDiff>? views = null,
        IReadOnlyList<EnumDiff>? enums = null,
        IReadOnlyList<SequenceDiff>? sequences = null,
        IReadOnlyList<RoutineDiff>? routines = null
    )
        => SchemaForKind(kind, name) with
        {
            RenamedFrom = renamedFrom,
            Comment = comment,
            Grants = grants ?? [],
            Tables = tables ?? [],
            Views = views ?? [],
            Enums = enums ?? [],
            Sequences = sequences ?? [],
            Routines = routines ?? [],
        };

    /// <summary>The empty diff for a schema kind (null = the schema itself is untouched).</summary>
    private static SchemaDiff SchemaForKind(ChangeKind? kind, string name) => kind switch
    {
        ChangeKind.Add => SchemaDiff.Added(name),
        ChangeKind.Remove => SchemaDiff.Removed(name),
        ChangeKind.Modify => SchemaDiff.Modified(name),
        _ => SchemaDiff.Containing(name),
    };


    private static TableDiff TableNode(
        string name,
        ChangeKind kind,
        string schema = "app",
        SqlIdentifier? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<ColumnDiff>? columns = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<IndexDiff>? indexes = null,
        IReadOnlyList<PrimaryKeyDiff>? primaryKey = null,
        IReadOnlyList<ForeignKeyDiff>? foreignKeys = null,
        IReadOnlyList<UniqueConstraintDiff>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraintDiff>? checks = null,
        IReadOnlyList<ExclusionConstraintDiff>? exclusionConstraints = null,
        IReadOnlyList<TriggerDiff>? triggers = null,
        Table? definition = null)
        => ForKind(kind, schema, name, definition) with
        {
            RenamedFrom = renamedFrom,
            Comment = comment,
            Columns = columns ?? [],
            Grants = grants ?? [],
            Indexes = indexes ?? [],
            PrimaryKeys = primaryKey ?? [],
            ForeignKeys = foreignKeys ?? [],
            UniqueConstraints = uniqueConstraints ?? [],
            Checks = checks ?? [],
            ExclusionConstraints = exclusionConstraints ?? [],
            Triggers = triggers ?? [],
        };

    /// <summary>The empty diff for a kind, so a test can then set only the members it cares about.</summary>
    private static TableDiff ForKind(ChangeKind kind, string schema, string name, Table? definition) => kind switch
    {
        ChangeKind.Add => TableDiff.Added(schema, definition ?? new Table { Name = name }),
        ChangeKind.Remove => TableDiff.Removed(schema, name),
        _ => TableDiff.Modified(schema, name),
    };

    private static ColumnDiff AddedColumn(Column definition, ValueChange<string>? comment = null)
        => comment is null ? ColumnDiff.Added(definition) : ColumnDiff.Added(definition) with { Comment = comment };

    private static ColumnDiff RemovedColumn(Column definition) => ColumnDiff.Removed(definition);

    private static ColumnDiff ModifiedColumn(
        string name,
        SqlIdentifier? renamedFrom = null,
        ValueChange<SqlType>? type = null,
        ValueChange<bool>? nullability = null,
        ValueChange<SqlDefaultExpression>? @default = null,
        ValueChange<IdentityOptions>? identity = null,
        ValueChange<string>? comment = null,
        ValueChange<SqlText>? generated = null,
        Column? definition = null)
        => ColumnDiff.Modified(definition ?? new Column
        {
            Name = name,
            Type = type?.New ?? SqlType.Text,
            IsNullable = nullability?.New ?? false,
        }) with
        {
            RenamedFrom = renamedFrom,
            Type = type,
            Nullability = nullability,
            Default = @default,
            Identity = identity,
            Comment = comment,
            Generated = generated,
        };

    private ViewDiff AddView(string name, string schema = "app", params (string Schema, string Name)[] dependsOn)
        => ViewDiff.Added(schema, _sides.Creating(schema, ViewReading(name, dependsOn)));

    private ViewDiff RemoveView(string name, string schema = "app", params (string Schema, string Name)[] dependsOn)
    {
        _sides.Dropping(schema, ViewReading(name, dependsOn));
        return ViewDiff.Removed(schema, name);
    }

    private static View ViewReading(string name, (string Schema, string Name)[] dependsOn) => new()
    {
        Name = name,
        Body = $"SELECT * FROM source_of_{name}",
        DependsOn = [.. dependsOn.Select(d => new ObjectAddress(d.Schema, d.Name))],
    };

    private TableDiff AddTable(string name, string schema = "app")
        => TableDiff.Added(schema, _sides.Creating(schema, new Table { Name = name }));

    private static int IndexOfCreateView(IReadOnlyList<MigrationAction> plan, string name)
        => plan.ToList().FindIndex(a => a is CreateView v && v.View.Name.Value.Equals(name));

    private static int IndexOfDropView(IReadOnlyList<MigrationAction> plan, string name)
        => plan.ToList().FindIndex(a => a is DropView v && v.View.Name.Value.Equals(name));

    /// <summary>Wraps a single table under a null-kind <c>app</c> schema (the common "only tables changed" case).</summary>
    private IReadOnlyList<MigrationAction> LinearizeTable(TableDiff table) => Linearize(SchemaNode("app", tables: [table]));

    [Fact]
    public void Linearize_TriggerRemoveAndAddUnderOneName_FoldsIntoReplace()
    {
        // Arrange — a structural change diffs as remove + add under one name; the plan states the intent
        // and leaves the mechanism to the dialect.
        var trigger = new Trigger { Name = "users_audit_trg", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Body = "BODY" };
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify, triggers:
        [
            TriggerDiff.Removed("users_audit_trg"),
            TriggerDiff.Added(trigger),
        ]));

        // Assert
        plan.ShouldHaveSingleItem().ShouldBeOfType<ReplaceTrigger>().Trigger.ShouldBe(trigger);
    }

    [Fact]
    public void Linearize_TriggerAddAndRemoveOfDifferentNames_StayDistinct()
    {
        // Arrange — only a same-named pair is a replacement.
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify, triggers:
        [
            TriggerDiff.Removed("old_trg"),
            TriggerDiff.Added(new Trigger { Name = "new_trg", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Body = "BODY" }),
        ]));

        // Assert
        plan.OfType<DropTrigger>().ShouldHaveSingleItem().Trigger.Member.ShouldBe("old_trg");
        plan.OfType<CreateTrigger>().ShouldHaveSingleItem().Trigger.Name.ShouldBe("new_trg");
        plan.OfType<ReplaceTrigger>().ShouldBeEmpty();
    }

    private static int IndexOf<T>(IReadOnlyList<MigrationAction> plan) where T : MigrationAction
    {
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan[i] is T)
            {
                return i;
            }
        }

        return -1;
    }

    // -------------------------------------------------------------------------
    // Empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_EmptyDiff_ProducesNoActions()
        => Linearize().ShouldBeEmpty();

    // -------------------------------------------------------------------------
    // Schema nodes
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddSchema_EmitsCreateSchema()
        => Linearize(SchemaNode("app", ChangeKind.Add))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateSchema>().SchemaName.ShouldBe("app");

    [Fact]
    public void Linearize_RemoveSchema_EmitsDropSchema()
        => Linearize(SchemaNode("app", ChangeKind.Remove))
            .ShouldHaveSingleItem().ShouldBeOfType<DropSchema>().SchemaName.ShouldBe("app");

    [Fact]
    public void Linearize_RemoveSchema_DropsNestedObjectsBeforeTheSchema()
    {
        // Arrange
        // A removed schema drops its contained objects first (rather than relying on a provider-specific
        // DROP SCHEMA CASCADE), then the schema itself. The type-sort orders the table drop ahead of the schema drop.
        var schema = SchemaNode("app", ChangeKind.Remove,
            tables: [TableNode("users", ChangeKind.Remove)]);

        // Act
        var plan = Linearize(schema);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<DropTable>().Table.Name.ShouldBe("users");
        plan[1].ShouldBeOfType<DropSchema>().SchemaName.ShouldBe("app");
    }

    [Fact]
    public void Linearize_RenamedSchema_EmitsRenameSchema_NotCreateOrDrop()
    {
        // Act
        var plan = Linearize(SchemaNode("application", ChangeKind.Modify, renamedFrom: "app"));

        // Assert
        plan.ShouldHaveSingleItem().ShouldBeOfType<RenameSchema>()
            .ShouldSatisfyAllConditions(
                r => r.OldName.ShouldBe("app"),
                r => r.NewName.ShouldBe("application"));
    }

    [Fact]
    public void Linearize_NullKindSchema_EmitsNoSchemaAction_ButEmitsTables()
    {
        // Act
        var plan = LinearizeTable(TableNode("users", ChangeKind.Remove));

        // Assert
        plan.Any(a => a is CreateSchema or DropSchema or RenameSchema).ShouldBeFalse();
        plan.ShouldHaveSingleItem().ShouldBeOfType<DropTable>().Table.Name.ShouldBe("users");
    }

    // -------------------------------------------------------------------------
    // Schema attributes
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_SchemaComment_EmitsSetSchemaComment()
    {
        var plan = Linearize(SchemaNode("app", ChangeKind.Modify, comment: new ValueChange<string>("old", "new")));

        plan.OfType<SetSchemaComment>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                c => c.OldComment.ShouldBe("old"),
                c => c.NewComment.ShouldBe("new"));
    }

    [Fact]
    public void Linearize_SchemaCommentOnNewSchema_EmitsSetSchemaComment()
        => Linearize(SchemaNode("app", ChangeKind.Add, comment: new ValueChange<string>(null, "created")))
            .OfType<SetSchemaComment>().ShouldHaveSingleItem().NewComment.ShouldBe("created");

    [Fact]
    public void Linearize_SchemaGrantAdd_EmitsGrantSchemaUsage()
        => Linearize(SchemaNode("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Add, "reader", null)]))
            .OfType<GrantSchemaUsage>().ShouldHaveSingleItem().Role.ShouldBe("reader");

    [Fact]
    public void Linearize_SchemaGrantRemove_EmitsRevokeSchemaUsage()
        => Linearize(SchemaNode("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, "reader", null)]))
            .OfType<RevokeSchemaUsage>().ShouldHaveSingleItem().Role.ShouldBe("reader");

    // -------------------------------------------------------------------------
    // Table nodes
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddTable_EmitsCreateTableFromDefinition()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Add, definition: new Table { Name = "users" }));

        plan.OfType<CreateTable>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                t => t.SchemaName.ShouldBe("app"),
                t => t.Table.Name.ShouldBe("users"));
    }

    [Fact]
    public void Linearize_RemoveTable_EmitsDropTable()
        => LinearizeTable(TableNode("users", ChangeKind.Remove))
            .ShouldHaveSingleItem().ShouldBeOfType<DropTable>().Table.Name.ShouldBe("users");

    [Fact]
    public void Linearize_RenamedTable_EmitsRenameTable_NotCreateOrDrop()
    {
        // Act
        var plan = LinearizeTable(TableNode("accounts", ChangeKind.Modify, renamedFrom: "users"));

        // Assert
        plan.Any(a => a is CreateTable or DropTable).ShouldBeFalse();
        plan.ShouldHaveSingleItem().ShouldBeOfType<RenameTable>()
            .ShouldSatisfyAllConditions(r => r.Table.Name.ShouldBe("users"), r => r.NewName.ShouldBe("accounts"));
    }

    [Fact]
    public void Linearize_RenamedTable_DropsTargetOldName_AndPrecedeRename()
    {
        // Arrange
        // Drops and revokes sort before RenameTable, so they execute while the table still carries its old name.
        var table = TableNode("accounts", ChangeKind.Modify, renamedFrom: "users",
            primaryKey: [PrimaryKeyDiff.Removed("users_pkey")],
            foreignKeys: [ForeignKeyDiff.Removed("users_org_fk")],
            uniqueConstraints: [UniqueConstraintDiff.Removed("users_email_uq")],
            checks: [CheckConstraintDiff.Removed("users_age_chk")],
            exclusionConstraints: [ExclusionConstraintDiff.Removed("no_overlap")],
            indexes: [IndexDiff.Removed("users_email_ix")],
            triggers: [TriggerDiff.Removed("users_audit_trg")],
            grants: [new GrantChange(ChangeKind.Remove, "reader", TablePrivilege.Select)]);

        // Act
        var plan = LinearizeTable(table);

        // Assert
        plan.OfType<DropPrimaryKey>().ShouldHaveSingleItem().PrimaryKey.Object.ShouldBe("users");
        plan.OfType<DropForeignKey>().ShouldHaveSingleItem().ForeignKey.Object.ShouldBe("users");
        plan.OfType<DropUniqueConstraint>().ShouldHaveSingleItem().Constraint.Object.ShouldBe("users");
        plan.OfType<DropCheckConstraint>().ShouldHaveSingleItem().Constraint.Object.ShouldBe("users");
        plan.OfType<DropExclusionConstraint>().ShouldHaveSingleItem().Constraint.Object.ShouldBe("users");
        plan.OfType<DropIndex>().ShouldHaveSingleItem().Index.Object.ShouldBe("users");
        plan.OfType<DropTrigger>().ShouldHaveSingleItem().Trigger.Object.ShouldBe("users");
        plan.OfType<RevokeTablePrivileges>().ShouldHaveSingleItem().Table.Name.ShouldBe("users");
        plan[^1].ShouldBeOfType<RenameTable>();
    }

    [Fact]
    public void Linearize_RenamedSchema_RenamePrecedesChildDrops()
    {
        // Child diff nodes carry the new schema name, so the schema rename must run before their drops for the
        // schema-qualified names to resolve.
        var table = TableNode("orders", ChangeKind.Modify, schema: "sales",
            foreignKeys: [ForeignKeyDiff.Removed("orders_user_fk")],
            triggers: [TriggerDiff.Removed("orders_audit_trg")]);
        var plan = Linearize(SchemaNode("sales", ChangeKind.Modify, renamedFrom: "shop",
            grants: [new GrantChange(ChangeKind.Remove, "reader")],
            tables: [table]));

        IndexOf<RenameSchema>(plan).ShouldBe(0);
        plan.OfType<DropForeignKey>().ShouldHaveSingleItem().ForeignKey.Schema.ShouldBe("sales");
        plan.OfType<DropTrigger>().ShouldHaveSingleItem().Trigger.Schema.ShouldBe("sales");
        plan.OfType<RevokeSchemaUsage>().ShouldHaveSingleItem().SchemaName.ShouldBe("sales");
    }

    [Fact]
    public void Linearize_RenamedTable_AddsTargetNewName()
    {
        // Arrange
        var table = TableNode("accounts", ChangeKind.Modify, renamedFrom: "users",
            uniqueConstraints: [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "accounts_email_uq", ColumnNames = ["email"] })],
            indexes: [IndexDiff.Added(new TableIndex { Name = "accounts_email_ix", Columns = ["email"] })]);

        // Act
        var plan = LinearizeTable(table);

        // Assert
        plan.OfType<AddUniqueConstraint>().ShouldHaveSingleItem().Table.Name.ShouldBe("accounts");
        plan.OfType<CreateIndex>().ShouldHaveSingleItem().Table.Name.ShouldBe("accounts");
    }

    [Fact]
    public void Linearize_TableComment_EmitsSetTableComment()
        => LinearizeTable(TableNode("users", ChangeKind.Modify, comment: new ValueChange<string>(null, "accounts")))
            .OfType<SetTableComment>().ShouldHaveSingleItem().NewComment.ShouldBe("accounts");

    [Fact]
    public void Linearize_AddTable_DoesNotEmitAddColumn_ButFoldsColumnComments()
    {
        // Arrange
        // Columns of a new table are created inline by CREATE TABLE; only their comments arrive as separate actions.
        var table = TableNode("users", ChangeKind.Add,
            definition: new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int, Comment = "pk" }] },
            columns: [AddedColumn(new Column { Name = "id", Type = SqlType.Int }, comment: new ValueChange<string>(null, "pk"))]);

        // Act
        var plan = LinearizeTable(table);

        // Assert
        plan.OfType<AddColumn>().ShouldBeEmpty();
        plan.OfType<CreateTable>().ShouldHaveSingleItem();
        plan.OfType<SetColumnComment>().ShouldHaveSingleItem().NewComment.ShouldBe("pk");
    }

    [Fact]
    public void Linearize_AddTable_FoldsConstraintsIntoCreateTable_ButKeepsComments()
    {
        // Arrange
        // Every constraint of a new table is created inline by CREATE TABLE (carried on Definition), so the
        // linearizer emits no separate Add* action for it; a constraint comment still arrives on its own.
        var table = TableNode("orders", ChangeKind.Add,
            definition: new Table { Name = "orders" },
            foreignKeys: [ForeignKeyDiff.Added(new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] })],
            uniqueConstraints: [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "orders_code_uq", ColumnNames = ["code"] })],
            checks: [CheckConstraintDiff.Added(new CheckConstraint { Name = "orders_total_chk", Expression = "total >= 0" })],
            exclusionConstraints: [ExclusionConstraintDiff.Added(new ExclusionConstraint { Name = "no_overlap", Elements = [new ExclusionElement("&&", "slot")], Method = "gist" })],
            primaryKey: [PrimaryKeyDiff.CommentChanged("orders_pkey", new ValueChange<string>(null, "the key"))]);

        // Act
        var plan = LinearizeTable(table);

        // Assert
        plan.OfType<CreateTable>().ShouldHaveSingleItem();
        plan.OfType<AddForeignKey>().ShouldBeEmpty();
        plan.OfType<AddUniqueConstraint>().ShouldBeEmpty();
        plan.OfType<AddCheckConstraint>().ShouldBeEmpty();
        plan.OfType<AddExclusionConstraint>().ShouldBeEmpty();
        plan.OfType<AddPrimaryKey>().ShouldBeEmpty();
        // The primary-key comment is not folded away.
        plan.OfType<SetConstraintComment>().ShouldHaveSingleItem().NewComment.ShouldBe("the key");
    }

    // -------------------------------------------------------------------------
    // Column changes (within a modified table)
    // -------------------------------------------------------------------------

    private IReadOnlyList<MigrationAction> LinearizeColumn(ColumnDiff column)
        => LinearizeTable(TableNode("users", ChangeKind.Modify, columns: [column]));

    [Fact]
    public void Linearize_AddColumn_EmitsAddColumnAndComment()
    {
        var plan = LinearizeColumn(AddedColumn(new Column { Name = "email", Type = SqlType.Text }, comment: new ValueChange<string>(null, "contact")));

        plan.OfType<AddColumn>().ShouldHaveSingleItem().Column.Name.ShouldBe("email");
        plan.OfType<SetColumnComment>().ShouldHaveSingleItem().NewComment.ShouldBe("contact");
    }

    [Fact]
    public void Linearize_RemoveColumn_EmitsDropColumn()
        => LinearizeColumn(RemovedColumn(new Column { Name = "email", Type = SqlType.Text }))
            .OfType<DropColumn>().ShouldHaveSingleItem().ColumnName.ShouldBe("email");

    [Fact]
    public void Linearize_RenameColumn_EmitsRenameColumn()
        => LinearizeColumn(ModifiedColumn("email_address", renamedFrom: "email"))
            .OfType<RenameColumn>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(r => r.Column.Member.ShouldBe("email"), r => r.NewName.ShouldBe("email_address"));

    [Fact]
    public void Linearize_ColumnTypeChange_EmitsAlterColumn()
        => LinearizeColumn(ModifiedColumn("id", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt)))
            .OfType<AlterColumn>().ShouldHaveSingleItem()
            .Type.ShouldBe(new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt));

    [Fact]
    public void Linearize_ColumnNullabilityChange_EmitsAlterColumn()
        => LinearizeColumn(ModifiedColumn("email", nullability: new ValueChange<bool>(true, false)))
            .OfType<AlterColumn>().ShouldHaveSingleItem()
            .Nullability.ShouldBe(new ValueChange<bool>(true, false));

    [Fact]
    public void Linearize_ColumnTypeChange_CarriesFinalDefinition()
        => LinearizeColumn(ModifiedColumn("id", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                definition: new Column { Name = "id", Type = SqlType.BigInt, IsNullable = false }))
            .OfType<AlterColumn>().ShouldHaveSingleItem()
            .Column.IsNullable.ShouldBe(false);

    [Fact]
    public void Linearize_ColumnNullabilityChange_CarriesFinalDefinition()
        => LinearizeColumn(ModifiedColumn("email", nullability: new ValueChange<bool>(true, false),
                definition: new Column { Name = "email", Type = SqlType.VarChar(255), IsNullable = false }))
            .OfType<AlterColumn>().ShouldHaveSingleItem()
            .Column.Type.ShouldBe(SqlType.VarChar(255));

    [Fact]
    public void Linearize_ColumnDefaultChange_EmitsSetColumnDefault()
        => LinearizeColumn(ModifiedColumn("status", @default: new ValueChange<SqlDefaultExpression>(null, "'active'")))
            .OfType<SetColumnDefault>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldDefault.ShouldBeNull(), a => a.NewDefault.ShouldBe("'active'"));

    [Fact]
    public void Linearize_ColumnIdentityChange_EmitsAlterIdentitySequence()
    {
        // Arrange
        var identity = new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1));

        // Act
        LinearizeColumn(ModifiedColumn("id", identity: identity))

        // Assert
            .OfType<AlterIdentitySequence>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldOptions.ShouldBeNull(), a => a.NewOptions.ShouldBe(new IdentityOptions(1, 1, 1)));
    }

    [Fact]
    public void Linearize_ColumnComment_EmitsSetColumnComment()
        => LinearizeColumn(ModifiedColumn("id", comment: new ValueChange<string>("old", "new")))
            .OfType<SetColumnComment>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));

    [Fact]
    public void Linearize_ColumnGenerationChange_EmitsSetColumnGenerated()
        => LinearizeColumn(ModifiedColumn("area", generated: new ValueChange<SqlText>(null, "w * h")))
            .OfType<SetColumnGenerated>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.OldExpression.ShouldBeNull(), a => a.NewExpression.ShouldBe("w * h"));

    [Fact]
    public void Linearize_ColumnWithEveryModification_EmitsAllActions()
    {
        // Arrange
        var column = ModifiedColumn("id",
            renamedFrom: "identifier",
            type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
            nullability: new ValueChange<bool>(true, false),
            @default: new ValueChange<SqlDefaultExpression>(null, "0"),
            identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1)),
            comment: new ValueChange<string>(null, "pk"));

        // Act
        var actions = LinearizeColumn(column);

        // Assert
        actions.OfType<RenameColumn>().ShouldHaveSingleItem();
        actions.OfType<AlterColumn>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(a => a.Type.ShouldNotBeNull(), a => a.Nullability.ShouldNotBeNull());
        actions.OfType<SetColumnDefault>().ShouldHaveSingleItem();
        actions.OfType<AlterIdentitySequence>().ShouldHaveSingleItem();
        actions.OfType<SetColumnComment>().ShouldHaveSingleItem();
    }

    // -------------------------------------------------------------------------
    // Constraints, indexes, grants
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddPrimaryKey_EmitsAddPrimaryKey()
    {
        // Arrange
        var pk = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] };
        var constraint = PrimaryKeyDiff.Added(pk);

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey: [constraint]))

        // Assert
            .OfType<AddPrimaryKey>().ShouldHaveSingleItem().PrimaryKey.Name.ShouldBe("users_pkey");
    }

    [Fact]
    public void Linearize_RemovePrimaryKey_EmitsDropPrimaryKey()
    {
        // Arrange
        var constraint = PrimaryKeyDiff.Removed("users_pkey");

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey: [constraint]))

        // Assert
            .OfType<DropPrimaryKey>().ShouldHaveSingleItem().PrimaryKey.Member.ShouldBe("users_pkey");
    }

    [Fact]
    public void Linearize_AddForeignKey_EmitsAddForeignKey()
    {
        // Arrange
        var fk = new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] };
        var constraint = ForeignKeyDiff.Added(fk);

        // Act
        LinearizeTable(TableNode("orders", ChangeKind.Modify, foreignKeys: [constraint]))

        // Assert
            .OfType<AddForeignKey>().ShouldHaveSingleItem().ForeignKey.Name.ShouldBe("orders_user_fk");
    }

    [Fact]
    public void Linearize_RemoveForeignKey_EmitsDropForeignKey()
    {
        // Arrange
        var constraint = ForeignKeyDiff.Removed("orders_user_fk");

        // Act
        LinearizeTable(TableNode("orders", ChangeKind.Modify, foreignKeys: [constraint]))

        // Assert
            .OfType<DropForeignKey>().ShouldHaveSingleItem().ForeignKey.Member.ShouldBe("orders_user_fk");
    }

    [Fact]
    public void Linearize_AddExclusionConstraint_EmitsAddExclusionConstraint()
    {
        // Arrange
        var exclusion = new ExclusionConstraint { Name = "no_overlap", Elements = [new ExclusionElement("&&", "during")], Method = "gist" };
        var constraint = ExclusionConstraintDiff.Added(exclusion);

        // Act
        LinearizeTable(TableNode("bookings", ChangeKind.Modify, exclusionConstraints: [constraint]))

        // Assert
            .OfType<AddExclusionConstraint>().ShouldHaveSingleItem().ExclusionConstraint.Name.ShouldBe("no_overlap");
    }

    [Fact]
    public void Linearize_RemoveExclusionConstraint_EmitsDropExclusionConstraint()
    {
        // Arrange
        var constraint = ExclusionConstraintDiff.Removed("no_overlap");

        // Act
        LinearizeTable(TableNode("bookings", ChangeKind.Modify, exclusionConstraints: [constraint]))

        // Assert
            .OfType<DropExclusionConstraint>().ShouldHaveSingleItem().Constraint.Member.ShouldBe("no_overlap");
    }

    [Fact]
    public void Linearize_AddUniqueConstraint_EmitsAddUniqueConstraint()
    {
        // Arrange
        var unique = new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] };
        var constraint = UniqueConstraintDiff.Added(unique);

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, uniqueConstraints: [constraint]))

        // Assert
            .OfType<AddUniqueConstraint>().ShouldHaveSingleItem().UniqueConstraint.Name.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_RemoveUniqueConstraint_EmitsDropUniqueConstraint()
    {
        // Arrange
        var constraint = UniqueConstraintDiff.Removed("users_email_uq");

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, uniqueConstraints: [constraint]))

        // Assert
            .OfType<DropUniqueConstraint>().ShouldHaveSingleItem().Constraint.Member.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_AddCheckConstraint_EmitsAddCheckConstraint()
    {
        // Arrange
        var check = new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" };
        var constraint = CheckConstraintDiff.Added(check);

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, checks: [constraint]))

        // Assert
            .OfType<AddCheckConstraint>().ShouldHaveSingleItem().CheckConstraint.Name.ShouldBe("users_age_chk");
    }

    [Fact]
    public void Linearize_RemoveCheckConstraint_EmitsDropCheckConstraint()
    {
        // Arrange
        var constraint = CheckConstraintDiff.Removed("users_age_chk");

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, checks: [constraint]))

        // Assert
            .OfType<DropCheckConstraint>().ShouldHaveSingleItem().Constraint.Member.ShouldBe("users_age_chk");
    }

    [Fact]
    public void Linearize_UniqueConstraintCommentChange_EmitsSetConstraintComment()
    {
        // Arrange
        var constraint = UniqueConstraintDiff.CommentChanged("users_email_uq", new ValueChange<string>("old", "new"));

        // Act
        var action = LinearizeTable(TableNode("users", ChangeKind.Modify, uniqueConstraints: [constraint]))

        // Assert
            .OfType<SetConstraintComment>().ShouldHaveSingleItem();
        action.Constraint.Member.ShouldBe("users_email_uq");
        action.OldComment.ShouldBe("old");
        action.NewComment.ShouldBe("new");
    }

    [Fact]
    public void Linearize_PrimaryKeyCommentChange_EmitsSetConstraintComment()
    {
        // Arrange
        var constraint = PrimaryKeyDiff.CommentChanged("users_pkey", new ValueChange<string>(null, "surrogate key"));

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey: [constraint]))

        // Assert
            .OfType<SetConstraintComment>().ShouldHaveSingleItem().NewComment.ShouldBe("surrogate key");
    }

    [Fact]
    public void Linearize_AddIndex_EmitsCreateIndex()
    {
        // Arrange
        var index = IndexDiff.Added(new TableIndex { Name = "users_email_ix", Columns = ["email"] });

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, indexes: [index]))

        // Assert
            .OfType<CreateIndex>().ShouldHaveSingleItem().Index.Name.ShouldBe("users_email_ix");
    }

    [Fact]
    public void Linearize_RemoveIndex_EmitsDropIndex()
    {
        // Arrange
        var index = IndexDiff.Removed("users_email_ix");

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, indexes: [index]))

        // Assert
            .OfType<DropIndex>().ShouldHaveSingleItem().Index.Member.ShouldBe("users_email_ix");
    }

    [Fact]
    public void Linearize_ModifyIndexComment_EmitsSetIndexComment()
    {
        // Arrange
        var index = IndexDiff.CommentChanged("users_email_ix", new ValueChange<string>("old", "new"));

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, indexes: [index]))

        // Assert
            .OfType<SetIndexComment>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));
    }

    [Fact]
    public void Linearize_TableGrantAdd_EmitsGrantTablePrivileges()
    {
        // Arrange
        var grant = new GrantChange(ChangeKind.Add, "reader", TablePrivilege.Select);

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, grants: [grant]))

        // Assert
            .OfType<GrantTablePrivileges>().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(g => g.Role.ShouldBe("reader"), g => g.Privileges.ShouldBe(TablePrivilege.Select));
    }

    [Fact]
    public void Linearize_TableGrantRemove_EmitsRevokeTablePrivileges()
    {
        // Arrange
        var grant = new GrantChange(ChangeKind.Remove, "reader", TablePrivilege.Select);

        // Act
        LinearizeTable(TableNode("users", ChangeKind.Modify, grants: [grant]))

        // Assert
            .OfType<RevokeTablePrivileges>().ShouldHaveSingleItem().Role.ShouldBe("reader");
    }

    // -------------------------------------------------------------------------
    // Ordering — the linearizer sorts every action into a safe dependency order.
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_OrdersCreateSchemaBeforeItsTables()
    {
        var plan = Linearize(SchemaNode("app", ChangeKind.Add,
            tables: [TableNode("users", ChangeKind.Add, definition: new Table { Name = "users" })]));

        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropColumnBeforeAddColumn()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify,
            columns: [AddedColumn(new Column { Name = "new_col", Type = SqlType.Text }), RemovedColumn(new Column { Name = "old_col", Type = SqlType.Text })]));

        IndexOf<DropColumn>(plan).ShouldBeLessThan(IndexOf<AddColumn>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddColumnBeforeAddPrimaryKey()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify,
            columns: [AddedColumn(new Column { Name = "id", Type = SqlType.Int })],
            primaryKey: [PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] })]));

        IndexOf<AddColumn>(plan).ShouldBeLessThan(IndexOf<AddPrimaryKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersConstraintRemovalBeforeAddition_WhenReplacingAPrimaryKey()
    {
        var plan = LinearizeTable(TableNode("users", ChangeKind.Modify, primaryKey:
        [
            PrimaryKeyDiff.Removed("users_pkey"),
            PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pkey", ColumnNames = ["id", "tenant"] }),
        ]));

        IndexOf<DropPrimaryKey>(plan).ShouldBeLessThan(IndexOf<AddPrimaryKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropTableAndDropSchemaLast()
    {
        // Arrange
        var plan = Linearize(
            SchemaNode("new_app", ChangeKind.Add, tables: [TableNode("users", ChangeKind.Add, schema: "new_app", definition: new Table { Name = "users" })]),
            SchemaNode("old_app", ChangeKind.Remove),

            // Act
            SchemaNode("app", tables: [TableNode("stale", ChangeKind.Remove)]));

        // Assert
        // Destructive table/schema drops run after every constructive action.
        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
        IndexOf<CreateTable>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropSchema>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropForeignKeyBeforeDropTable()
    {
        var plan = Linearize(SchemaNode("app", tables:
        [
            TableNode("orders", ChangeKind.Modify, foreignKeys: [ForeignKeyDiff.Removed("orders_user_fk")]),
            TableNode("users", ChangeKind.Remove),
        ]));

        IndexOf<DropForeignKey>(plan).ShouldBeLessThan(IndexOf<DropTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddUniqueConstraintBeforeAddForeignKey()
    {
        // A foreign key may target a unique constraint, so the constraint must be created first.
        var plan = LinearizeTable(TableNode("orders", ChangeKind.Modify,
            uniqueConstraints: [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "orders_code_uq", ColumnNames = ["code"] })],
            foreignKeys: [ForeignKeyDiff.Added(new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] })]));

        IndexOf<AddUniqueConstraint>(plan).ShouldBeLessThan(IndexOf<AddForeignKey>(plan));
    }

    [Fact]
    public void Linearize_OrdersDropForeignKeyBeforeDropUniqueConstraint()
    {
        // Arrange
        // The mirror of the add ordering: a referencing foreign key is dropped before the constraint it targets.
        var plan = LinearizeTable(TableNode("orders", ChangeKind.Modify,
            foreignKeys: [ForeignKeyDiff.Removed("orders_user_fk")],

            // Act
            uniqueConstraints: [UniqueConstraintDiff.Removed("orders_code_uq")]));

        // Assert
        IndexOf<DropForeignKey>(plan).ShouldBeLessThan(IndexOf<DropUniqueConstraint>(plan));
    }

    // -------------------------------------------------------------------------
    // Views — the dependency-aware ordering layered on the fixed type order
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_CreatesViewAfterTheViewItReads_DespiteName()
    {
        // Act
        // "a_top" reads "z_base"; alphabetically a_top sorts first, but it must be created second.
        var plan = Linearize(SchemaNode("app", views: [AddView("a_top", "app", ("app", "z_base")), AddView("z_base")]));

        // Assert
        IndexOfCreateView(plan, "z_base").ShouldBeLessThan(IndexOfCreateView(plan, "a_top"));
    }

    [Fact]
    public void Linearize_CreatesViewsInTransitiveDependencyOrder()
    {
        // Arrange
        // c -> b -> a
        var plan = Linearize(SchemaNode("app", views:

            // Act
            [AddView("c", "app", ("app", "b")), AddView("b", "app", ("app", "a")), AddView("a")]));

        // Assert
        IndexOfCreateView(plan, "a").ShouldBeLessThan(IndexOfCreateView(plan, "b"));
        IndexOfCreateView(plan, "b").ShouldBeLessThan(IndexOfCreateView(plan, "c"));
    }

    [Fact]
    public void Linearize_DropsDependentViewBeforeItsDependency()
    {
        // Act
        // a_top reads z_base; dropping must remove a_top first (the reverse of create order).
        var plan = Linearize(SchemaNode("app", views: [RemoveView("a_top", "app", ("app", "z_base")), RemoveView("z_base")]));

        // Assert
        IndexOfDropView(plan, "a_top").ShouldBeLessThan(IndexOfDropView(plan, "z_base"));
    }

    [Fact]
    public void Linearize_OrdersViewDependenciesAcrossSchemas()
    {
        // Arrange
        // A view in "reporting" reads a view in "core"; the core view must be created first.
        var plan = Linearize(
            SchemaNode("reporting", views: [AddView("summary", "reporting", ("core", "base"))]),

            // Act
            SchemaNode("core", views: [AddView("base", "core")]));

        // Assert
        IndexOfCreateView(plan, "base").ShouldBeLessThan(IndexOfCreateView(plan, "summary"));
    }

    [Fact]
    public void Linearize_OrdersCreateViewAfterCreateTable()
    {
        // Act
        var plan = Linearize(SchemaNode("app", tables: [AddTable("t")], views: [AddView("v", "app", ("app", "t"))]));

        // Assert
        IndexOf<CreateTable>(plan).ShouldBeLessThan(IndexOfCreateView(plan, "v"));
    }

    [Fact]
    public void Linearize_OrdersDropViewBeforeDropTable()
    {
        // Arrange
        var plan = Linearize(SchemaNode("app",
            tables: [TableNode("t", ChangeKind.Remove)],

            // Act
            views: [RemoveView("v", "app", ("app", "t"))]));

        // Assert
        IndexOfDropView(plan, "v").ShouldBeLessThan(IndexOf<DropTable>(plan));
    }

    [Fact]
    public void Linearize_EmitsRenameViewForRenamedView()
    {
        // Act
        var plan = Linearize(SchemaNode("app", views: [ViewDiff.Modified("app", "active") with { RenamedFrom = "legacy" }]));

        // Assert
        var rename = plan.OfType<RenameView>().ShouldHaveSingleItem();
        rename.View.Name.ShouldBe("legacy");
        rename.NewName.ShouldBe("active");
        plan.OfType<CreateView>().ShouldBeEmpty(); // a rename-only change is not a replace
    }

    [Fact]
    public void Linearize_EmitsSetViewCommentForCommentChange()
    {
        var plan = Linearize(SchemaNode("app", views:
            [ViewDiff.Modified("app", "active") with { Comment = new ValueChange<string>("old", "new") }]));

        var comment = plan.OfType<SetViewComment>().ShouldHaveSingleItem();
        comment.View.Name.ShouldBe("active");
        comment.OldComment.ShouldBe("old");
        comment.NewComment.ShouldBe("new");
    }

    [Fact]
    public void Linearize_IndependentViews_KeepStableOrder()
    {
        // Act
        var plan = Linearize(SchemaNode("app", views: [AddView("x"), AddView("y"), AddView("z")]));

        // Assert
        IndexOfCreateView(plan, "x").ShouldBeLessThan(IndexOfCreateView(plan, "y"));
        IndexOfCreateView(plan, "y").ShouldBeLessThan(IndexOfCreateView(plan, "z"));
    }

    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddEnum_EmitsCreateEnumFromDefinition()
    {
        var plan = Linearize(SchemaNode("app", enums:
            [EnumDiff.Added("app", new EnumType { Name = "status", Values = ["a", "b"] })]));

        plan.ShouldHaveSingleItem().ShouldBeOfType<CreateEnum>().Enum.Values.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Linearize_RemoveEnum_EmitsDropEnum()
        => Linearize(SchemaNode("app", enums: [EnumDiff.Removed("app", "status")]))
            .ShouldHaveSingleItem().ShouldBeOfType<DropEnum>().Enum.Name.ShouldBe("status");

    [Fact]
    public void Linearize_RenamedEnum_EmitsRenameEnum_NotCreateOrDrop()
    {
        // Arrange
        var plan = Linearize(SchemaNode("app", enums:

            // Act
            [EnumDiff.Modified("app", "status") with { RenamedFrom = "state" }]));

        // Assert
        plan.ShouldHaveSingleItem().ShouldBeOfType<RenameEnum>()
            .ShouldSatisfyAllConditions(r => r.Enum.Name.ShouldBe("state"), r => r.NewName.ShouldBe("status"));
    }

    [Fact]
    public void Linearize_EnumValueAdditions_EmitOneActionEach_InListOrder()
    {
        // Arrange
        var plan = Linearize(SchemaNode("app", enums:
        [
            EnumDiff.Modified("app", "status") with
            {
                AddedValues = [
                new EnumValueAddition("a", Before: "c"),
                new EnumValueAddition("b", After: "a"),
            ],
            },
        ]));

        // Act
        var additions = plan.OfType<AddEnumValue>().ToList();

        // Assert
        additions.Select(a => (a.Value, a.Before, a.After)).ShouldBe(
            [("a", "c", null), ("b", null, "a")]);
    }

    [Fact]
    public void Linearize_EnumComment_EmitsSetEnumComment()
        => Linearize(SchemaNode("app", enums:
            [EnumDiff.Modified("app", "status") with { Comment = new ValueChange<string>("old", "new") }]))
            .ShouldHaveSingleItem().ShouldBeOfType<SetEnumComment>()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));

    [Fact]
    public void Linearize_EnumRequiringRecreate_EmitsNoValueActions_AndDoesNotThrow()
    {
        // A removal/reorder cannot be planned; the linearizer stays silent and the always-on
        // EnumValueRemovalDiffPolicy fails the run at the workflow level instead.
        var plan = Linearize(SchemaNode("app", enums:
        [
            EnumDiff.Modified("app", "status") with
            {
                Values = new ValueChange<IReadOnlyList<EnumLabel>>(["a", "b"], ["a"]),
            },
        ]));

        plan.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Sequences
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_AddSequence_EmitsCreateSequenceFromDefinition()
    {
        var plan = Linearize(SchemaNode("app", sequences:
            [SequenceDiff.Added("app", new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 100) })]));

        plan.ShouldHaveSingleItem().ShouldBeOfType<CreateSequence>().Sequence.Options.StartWith.ShouldBe(100);
    }

    [Fact]
    public void Linearize_RemoveSequence_EmitsDropSequence()
        => Linearize(SchemaNode("app", sequences: [SequenceDiff.Removed("app", "order_id")]))
            .ShouldHaveSingleItem().ShouldBeOfType<DropSequence>().Sequence.Name.ShouldBe("order_id");

    [Fact]
    public void Linearize_RenamedSequence_EmitsRenameSequence()
        => Linearize(SchemaNode("app", sequences:
            [SequenceDiff.Modified("app", "invoice_id") with { RenamedFrom = "bill_id" }]))
            .ShouldHaveSingleItem().ShouldBeOfType<RenameSequence>()
            .ShouldSatisfyAllConditions(r => r.Sequence.Name.ShouldBe("bill_id"), r => r.NewName.ShouldBe("invoice_id"));

    [Fact]
    public void Linearize_SequenceOptionsChange_EmitsAlterSequence()
    {
        // Arrange
        var options = new ValueChange<SequenceOptions>(
            new SequenceOptions(StartWith: 1), new SequenceOptions(StartWith: 100));

        // Act
        Linearize(SchemaNode("app", sequences: [SequenceDiff.Modified("app", "order_id") with { Options = options }]))

        // Assert
            .ShouldHaveSingleItem().ShouldBeOfType<AlterSequence>()
            .ShouldSatisfyAllConditions(
                a => a.OldOptions.StartWith.ShouldBe(1),
                a => a.NewOptions.StartWith.ShouldBe(100));
    }

    [Fact]
    public void Linearize_SequenceComment_EmitsSetSequenceComment()
        => Linearize(SchemaNode("app", sequences:
            [SequenceDiff.Modified("app", "order_id") with { Comment = new ValueChange<string>(null, "order numbers") }]))
            .ShouldHaveSingleItem().ShouldBeOfType<SetSequenceComment>().NewComment.ShouldBe("order numbers");

    // -------------------------------------------------------------------------
    // Enum/sequence ordering relative to tables
    // -------------------------------------------------------------------------

    [Fact]
    public void Linearize_OrdersCreateEnumAndSequenceBeforeCreateTable()
    {
        // A column may use the enum type and a default may call the sequence, so both exist first.
        var plan = Linearize(SchemaNode("app", ChangeKind.Add,
            tables: [TableNode("users", ChangeKind.Add, definition: new Table { Name = "users" })],
            enums: [EnumDiff.Added("app", new EnumType { Name = "status", Values = ["a"] })],
            sequences: [SequenceDiff.Added("app", new Sequence { Name = "order_id" })]));

        IndexOf<CreateSchema>(plan).ShouldBeLessThan(IndexOf<CreateEnum>(plan));
        IndexOf<CreateEnum>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
        IndexOf<CreateSequence>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    [Fact]
    public void Linearize_OrdersAddEnumValueBeforeColumnChanges()
    {
        // A column being retyped to the enum (or defaulted to a new value) needs the value to exist first.
        var plan = Linearize(SchemaNode("app",
            tables:
            [
                TableNode("users", ChangeKind.Modify, columns:
                [
                    ModifiedColumn("status",
                        type: new ValueChange<SqlType>(SqlType.Text, SqlType.Custom("status")),
                        @default: new ValueChange<SqlDefaultExpression>(null, "'a'")),
                ]),
            ],
            enums: [EnumDiff.Modified("app", "status") with { AddedValues = [new EnumValueAddition("a")] }]));

        IndexOf<AddEnumValue>(plan).ShouldBeLessThan(IndexOf<AlterColumn>(plan));
        IndexOf<AddEnumValue>(plan).ShouldBeLessThan(IndexOf<SetColumnDefault>(plan));
    }

    [Fact]
    public void Linearize_OrdersEnumAndSequenceDropsAfterDropTable_BeforeDropSchema()
    {
        // Arrange
        var plan = Linearize(
            SchemaNode("app",
                tables: [TableNode("users", ChangeKind.Remove)],
                enums: [EnumDiff.Removed("app", "status")],
                sequences: [SequenceDiff.Removed("app", "order_id")]),

            // Act
            SchemaNode("scratch", ChangeKind.Remove));

        // Assert
        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropEnum>(plan));
        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropSequence>(plan));
        IndexOf<DropEnum>(plan).ShouldBeLessThan(IndexOf<DropSchema>(plan));
        IndexOf<DropSequence>(plan).ShouldBeLessThan(IndexOf<DropSchema>(plan));
    }

    [Fact]
    public void Linearize_OrdersRenameEnumBeforeCreateTable()
    {
        // Arrange
        // A new table's columns reference the enum by its new name, so the rename must land first.
        var plan = Linearize(SchemaNode("app",
            tables: [TableNode("users", ChangeKind.Add, definition: new Table { Name = "users" })],

            // Act
            enums: [EnumDiff.Modified("app", "status") with { RenamedFrom = "state" }]));

        // Assert
        IndexOf<RenameEnum>(plan).ShouldBeLessThan(IndexOf<CreateTable>(plan));
    }

    // -------------------------------------------------------------------------
    // Functions and procedures
    // -------------------------------------------------------------------------

    private static readonly Routine _fn = new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "a int", Definition = "RETURNS int LANGUAGE sql AS $$ SELECT 1; $$" };
    private static readonly Routine _proc = new Routine { Name = "p", RoutineKind = RoutineKind.Procedure, Arguments = "", Definition = "LANGUAGE sql AS $$ DELETE FROM app.t; $$" };

    [Fact]
    public void Linearize_AddFunction_EmitsCreateRoutineFromDefinition()
        => Linearize(SchemaNode("app", routines: [RoutineDiff.Added("app", _fn)]))
            .ShouldHaveSingleItem().ShouldBeOfType<CreateRoutine>().Routine.Arguments.ShouldBe("a int");

    [Fact]
    public void Linearize_RemoveRoutine_EmitsDropRoutine()
        => Linearize(SchemaNode("app", routines: [RoutineDiff.Removed("app", "f", RoutineKind.Function)]))
            .ShouldHaveSingleItem().ShouldBeOfType<DropRoutine>().Routine.Name.ShouldBe("f");

    [Fact]
    public void Linearize_RoutineBodyChange_EmitsReplaceRoutine_NotRecreate()
    {
        // Arrange
        // A definition-only change replaces in place, and never masquerades as a create.
        var plan = Linearize(SchemaNode("app", routines:

            // Act
            [RoutineDiff.Modified("app", "f", RoutineKind.Function) with { Definition = _fn }]));

        // Assert
        plan.ShouldHaveSingleItem().ShouldBeOfType<ReplaceRoutine>();
        plan.OfType<RecreateRoutine>().ShouldBeEmpty();
    }

    [Fact]
    public void Linearize_RoutineSignatureChange_EmitsRecreateRoutine()
        => Linearize(SchemaNode("app", routines:
            [RoutineDiff.Modified("app", "f", RoutineKind.Function) with
            {
                Definition = _fn,
                Arguments = new ValueChange<SqlText>("a int", "a int, b text"),
            }]))
            .ShouldHaveSingleItem().ShouldBeOfType<RecreateRoutine>();

    [Fact]
    public void Linearize_RenamedRoutine_EmitsRenameRoutine()
        => Linearize(SchemaNode("app", routines:
            [RoutineDiff.Modified("app", "f", RoutineKind.Function) with { RenamedFrom = "old_f" }]))
            .ShouldHaveSingleItem().ShouldBeOfType<RenameRoutine>()
            .ShouldSatisfyAllConditions(r => r.Routine.Name.ShouldBe("old_f"), r => r.NewName.ShouldBe("f"));

    [Fact]
    public void Linearize_RenameWithSignatureChange_RenamesBeforeRecreating()
    {
        // The recreate targets the final name, so the rename must land first.
        var plan = Linearize(SchemaNode("app", routines:
            [RoutineDiff.Modified("app", "f", RoutineKind.Function) with
            {
                RenamedFrom = "old_f",
                Definition = _fn,
                Arguments = new ValueChange<SqlText>("a int", "a int, b text"),
            }]));

        IndexOf<RenameRoutine>(plan).ShouldBeLessThan(IndexOf<RecreateRoutine>(plan));
    }

    [Fact]
    public void Linearize_RoutineComment_EmitsSetRoutineComment()
        => Linearize(SchemaNode("app", routines:
            [RoutineDiff.Modified("app", "f", RoutineKind.Function) with
            {
                Comment = new ValueChange<string>("old", "new"),
            }]))
            .ShouldHaveSingleItem().ShouldBeOfType<SetRoutineComment>()
            .ShouldSatisfyAllConditions(c => c.OldComment.ShouldBe("old"), c => c.NewComment.ShouldBe("new"));

    [Fact]
    public void Linearize_ProcedureLifecycle_EmitsRoutineActions()
    {
        var plan = Linearize(SchemaNode("app", routines:
        [
            RoutineDiff.Added("app", _proc),
            RoutineDiff.Modified("app", "q", RoutineKind.Procedure) with
            {
                Definition = _proc,
                RenamedFrom = "old_q",
                Arguments = new ValueChange<SqlText>("", "before date"),
            },
            RoutineDiff.Removed("app", "stale", RoutineKind.Procedure),
        ]));

        plan.OfType<CreateRoutine>().ShouldHaveSingleItem();
        plan.OfType<RenameRoutine>().ShouldHaveSingleItem();
        plan.OfType<RecreateRoutine>().ShouldHaveSingleItem();
        plan.OfType<DropRoutine>().ShouldHaveSingleItem().Routine.Name.ShouldBe("stale");
    }

    [Fact]
    public void Linearize_OrdersRoutineCreatesAfterCreateTable_AndBeforeConstraintsAndViews()
    {
        // A routine's signature and body may reference the tables it follows (a rowtype return, a query the
        // engine validates at creation); the constraints, triggers, and views that may call it come after.
        var plan = Linearize(SchemaNode("app", ChangeKind.Add,
            tables: [TableNode("users", ChangeKind.Add, definition: new Table { Name = "users" })],
            enums: [EnumDiff.Added("app", new EnumType { Name = "status", Values = ["a"] })],
            views: [ViewDiff.Added("app", new View { Name = "v", Body = "SELECT 1" })],
            routines:
            [
                RoutineDiff.Added("app", _fn),
                RoutineDiff.Added("app", _proc),
            ]));

        IndexOf<CreateEnum>(plan).ShouldBeLessThan(IndexOf<CreateRoutine>(plan));
        IndexOf<CreateTable>(plan).ShouldBeLessThan(IndexOf<CreateRoutine>(plan));
        IndexOf<CreateRoutine>(plan).ShouldBeLessThan(IndexOf<CreateView>(plan));
    }

    [Fact]
    public void Linearize_OrdersRoutineDropsAfterDropTable_BeforeDropEnum()
    {
        // Arrange
        var plan = Linearize(SchemaNode("app",
            tables: [TableNode("users", ChangeKind.Remove)],
            enums: [EnumDiff.Removed("app", "status")],

            // Act
            routines: [RoutineDiff.Removed("app", "f", RoutineKind.Function)]));

        // Assert
        IndexOf<DropTable>(plan).ShouldBeLessThan(IndexOf<DropRoutine>(plan));
        IndexOf<DropRoutine>(plan).ShouldBeLessThan(IndexOf<DropEnum>(plan));
    }

    [Fact]
    public void Linearize_OrdersViewsAroundRoutines()
    {
        // A view may call a routine: views are created after routines and dropped before them.
        var plan = Linearize(SchemaNode("app",
            views: [AddView("v"), RemoveView("stale_v")],
            routines:
            [
                RoutineDiff.Added("app", _fn),
                RoutineDiff.Removed("app", "stale_f", RoutineKind.Function),
            ]));

        IndexOf<CreateRoutine>(plan).ShouldBeLessThan(IndexOfCreateView(plan, "v"));
        IndexOfDropView(plan, "stale_v").ShouldBeLessThan(IndexOf<DropRoutine>(plan));
    }
}
