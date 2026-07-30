using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Enums;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Plan.Domain.Services;

namespace NSchema.Tests.Plan;

/// <summary>
/// Snapshot coverage for <see cref="PlanLinearizer"/>. Ordering is the linearizer's whole
/// contract, and it reads most clearly as a flat ordered list — so this pins the emitted action sequence
/// for a diff that touches schemas, tables, columns, indexes, and constraints. The fine-grained mapping
/// and priority assertions stay in <see cref="PlanLinearizerTests"/>.
///
/// Each action is projected as <c>{ Type, Action }</c> because the action list is polymorphic: without the
/// concrete type name many records are ambiguous (e.g. CreateSchema vs CreateTable both show just a schema).
/// </summary>
public sealed class PlanLinearizerSnapshotTests
{
    private readonly PlanLinearizer _linearizer = new();
    private readonly MigrationSides _sides = new();

    [Fact]
    public Task Linearize_RichDiff_OrdersActionsSafely()
    {
        // Arrange
        // A new schema; a newly-added table (columns, PK and constraints carried inline on Definition, with a
        // separate index and grant); a modified table (add/drop/retype columns, new index, dropped FK); two added
        // views (one reading the other), a renamed view, and a dropped view; a dropped schema carrying its own
        // table, view, enum and sequence (all dropped before the schema). Enough cross-kind work to exercise the
        // priority ordering and the view dependency sort.
        var newTable = TableDiff.Added("app", new Table
        {
            Name = "users",
            PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] },
            Columns = [
                    new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) },
                    new Column { Name = "name", Type = SqlType.VarChar(255) },
                ],
            UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
        }) with
        {
            Columns = [],
            Grants = [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes = [IndexDiff.Added(new TableIndex { Name = "users_name_ix", Columns = ["name"], IsUnique = true })],
            UniqueConstraints = [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })],
        };

        var modifiedTable = TableDiff.Modified("app", "orders") with
        {
            RenamedFrom = "purchases",
            Columns = [
                ColumnDiff.Modified(new Column { Name = "total", Type = SqlType.BigInt }) with
                {
                    Type = new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability = new ValueChange<bool>(true, false),
                },
                ColumnDiff.Added(new Column { Name = "notes", Type = SqlType.Text, IsNullable = true }),
                ColumnDiff.Modified(new Column { Name = "total_label", Type = SqlType.Text })
                    with { Generated = new ValueChange<SqlText>(null, "total::text") },
                ColumnDiff.Removed(new Column { Name = "legacy_flag", Type = SqlType.Boolean }),
            ],
            Grants = [],
            Indexes = [IndexDiff.Added(new TableIndex { Name = "orders_total_ix", Columns = [new IndexColumn("total", Sort: IndexSort.Descending)], Method = "btree", Include = ["code"] })],
            ForeignKeys = [ForeignKeyDiff.Removed("orders_user_fk")],
            UniqueConstraints = [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "orders_code_uq", ColumnNames = ["code"] })],
            Checks = [CheckConstraintDiff.Added(new CheckConstraint { Name = "orders_total_chk", Expression = "total >= 0" })],
            ExclusionConstraints = [ExclusionConstraintDiff.Added(new ExclusionConstraint { Name = "orders_slot_excl", Elements = [new ExclusionElement("&&", "slot")], Method = "gist" })],
        };

        // Listed dependent-first on purpose: the dependency sort must reorder them so user_summary (which
        // reads active_users) is created after it.
        var views = new ViewDiff[]
        {
            ViewDiff.Added("app", _sides.Creating("app", new View { Name = "user_summary", Body = "SELECT * FROM app.active_users", DependsOn = [new ObjectAddress("app", "active_users")] })),
            ViewDiff.Added("app", _sides.Creating("app", new View { Name = "active_users", Body = "SELECT * FROM app.users", DependsOn = [new ObjectAddress("app", "users")] })),
            ViewDiff.Modified("app", "report") with { RenamedFrom = "legacy_report" },
            ViewDiff.Removed("app", "stale_view"),
        };

        // Enums and sequences: additions (created before tables), an anchored value addition, a rename,
        // an options change, and drops (after tables, before the schema drop).
        var enums = new EnumDiff[]
        {
            EnumDiff.Added("app", new EnumType { Name = "order_status", Values = ["pending", "shipped"] }),
            EnumDiff.Modified("app", "priority") with
            {
                RenamedFrom = "importance",
                AddedValues = [new EnumValueAddition("medium", After: "low")],
            },
            EnumDiff.Removed("app", "stale_enum"),
        };
        var sequences = new SequenceDiff[]
        {
            SequenceDiff.Added("app", new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 100) }),
            SequenceDiff.Modified("app", "ticket_id") with
            {
                Options = new ValueChange<SequenceOptions>(new SequenceOptions(StartWith: 1), new SequenceOptions(StartWith: 1000)),
            },
            SequenceDiff.Removed("app", "stale_seq"),
        };

        // Routines: an add, a rename + signature change (rename then recreate), drops, and a procedure.
        var routines = new RoutineDiff[]
        {
            RoutineDiff.Added("app", new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric", Definition = "RETURNS numeric AS $$ SELECT amount $$" }),
            RoutineDiff.Modified("app", "score", RoutineKind.Function) with
            {
                RenamedFrom = "old_score",
                Definition = new Routine { Name = "score", RoutineKind = RoutineKind.Function, Arguments = "user_id bigint, weight numeric", Definition = "RETURNS numeric AS $$ SELECT 1 $$" },
                Arguments = new ValueChange<SqlText>("user_id bigint", "user_id bigint, weight numeric"),
            },
            RoutineDiff.Removed("app", "stale_fn", RoutineKind.Function),
            RoutineDiff.Added("app", new Routine { Name = "archive", RoutineKind = RoutineKind.Procedure, Arguments = "before date", Definition = "LANGUAGE sql AS $$ DELETE $$" }),
            RoutineDiff.Removed("app", "stale_proc", RoutineKind.Procedure),
        };

        var diff = new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Added("reporting") with
                {
                    Grants = [],
                    Tables = [],
                },
                SchemaDiff.Containing("app") with
                {
                    Grants = [],
                    Tables = [newTable, modifiedTable],
                    Views = views,
                    Enums = enums,
                    Sequences = sequences,
                    Routines = routines,
                },
                SchemaDiff.Removed("scratch") with
                {
                    Grants = [],
                    Tables = [TableDiff.Removed("scratch", "temp_data")],
                    Views = [ViewDiff.Removed("scratch", "temp_view")],
                    Enums = [EnumDiff.Removed("scratch", "temp_status")],
                    Sequences = [SequenceDiff.Removed("scratch", "temp_seq")],
                },
            ]);

        // Act
        var plan = _linearizer.Linearize(diff, _sides.Dependencies, DialectCapabilities.Standard);

        // Assert
        return Verify(plan.Select(a => new { Type = a.GetType().Name, Action = a }));
    }
}
