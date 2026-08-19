using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Triggers;
using NSchema.Diff.Domain.Views;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="DiffDocument"/>.
/// </summary>
public sealed class DiffDocumentSnapshotTests
{

    /// <summary>
    /// A diff exercising add/modify/remove across schemas, tables, columns, indexes, constraints, and grants.
    /// </summary>
    private static DatabaseDiff RichDiff()
    {
        var addedTable = TableDiff.Added("app", new Table { Name = "users" }) with
        {
            Comment = new ValueChange<string>(null, "all users"),
            Columns =
            [
                ColumnDiff.Added(new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true,
                    IdentityOptions = new IdentityOptions(1, 1, 1) }),
                ColumnDiff.Added(new Column { Name = "name", Type = SqlType.VarChar(255), DefaultExpression = "'anonymous'" }),
            ],
            Grants = [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes = [IndexDiff.Added(new TableIndex { Name = "users_name_ix", Columns = ["name"], IsUnique = true })],
            PrimaryKeys = [PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] })],
            UniqueConstraints = [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })],
            Checks = [CheckConstraintDiff.Added(new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" })],
        };

        var modifiedTable = TableDiff.Modified("app", "orders") with
        {
            RenamedFrom = "purchases",
            Columns = [
                ColumnDiff.Modified(new Column { Name = "total", Type = SqlType.BigInt })
                    with
                    {
                        Type = new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                        Nullability = new ValueChange<bool>(true, false),
                    },
                ColumnDiff.Modified(new Column { Name = "total_label", Type = SqlType.Text })
                    with { Generated = new ValueChange<SqlText>(null, "total::text") },
                ColumnDiff.Added(new Column { Name = "amount", Type = SqlType.Int, GeneratedExpression = "total * 100" }),
                ColumnDiff.Removed(new Column { Name = "legacy_flag", Type = SqlType.Boolean }),
            ],
            Grants = [new GrantChange(ChangeKind.Remove, "writers", TablePrivilege.Insert)],
            Indexes = [],
            PrimaryKeys = [PrimaryKeyDiff.CommentChanged("orders_pkey", new ValueChange<string>("old note", "new note"))],
            ForeignKeys = [ForeignKeyDiff.Removed("orders_user_fk")],
            UniqueConstraints = [UniqueConstraintDiff.Removed("orders_code_uq")],
            Checks = [CheckConstraintDiff.Removed("orders_total_chk")],
            ExclusionConstraints = [
                ExclusionConstraintDiff.Added(new ExclusionConstraint { Name = "orders_slot_excl", Elements = [new ExclusionElement("&&", "slot")], Method = "gist" }),
                ExclusionConstraintDiff.Removed("orders_old_excl"),
            ],
        };

        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Added("reporting") with
                {
                    Comment = new ValueChange<string>(null, "analytics"),
                    Grants = [new GrantChange(ChangeKind.Add, "analyst", null)],
                    Tables = [],
                },
                SchemaDiff.Containing("app") with
                {
                    Grants = [],
                    Tables = [addedTable, modifiedTable],
                },
                SchemaDiff.Removed("scratch") with
                {
                    Grants = [],
                    Tables = [],
                },
            ]);
    }

    /// <summary>
    /// A diff exercising every view change kind: an added view, a body replacement, a comment-only change, a
    /// rename, and a removal.
    /// </summary>
    private static DatabaseDiff ViewChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    Views = [
                    ViewDiff.Added("app", new View { Name = "active_users", Body = "SELECT id FROM app.users WHERE active" }) with { Comment = new ValueChange<string>(null, "currently active users") },
                    ViewDiff.Modified("app", "daily_totals") with { Definition = new View { Name = "daily_totals", Body = "SELECT date, sum(amount) FROM app.sales GROUP BY date" } },
                    ViewDiff.Modified("app", "summary") with
                    {
                        Comment = new ValueChange<string>("old summary", "new summary"),
                    },
                    ViewDiff.Modified("app", "report") with { RenamedFrom = "legacy_report" },
                    ViewDiff.Removed("app", "stale_view"),
                    // Materialized views: an add (with index on the definition) and an in-place index change.
                    ViewDiff.Added("app", new View { Name = "mv_sales", Body = "SELECT date, sum(amount) FROM app.sales GROUP BY date", IsMaterialized = true }) with
                    { Comment = new ValueChange<string>(null, "sales rollup"),
                        IsMaterialized = true,
                    },
                    ViewDiff.Modified("app", "mv_active") with
                    {
                        IsMaterialized = true,
                        Indexes = [
                            IndexDiff.Added(new TableIndex { Name = "mv_active_ix", Columns = ["id"] }),
                            IndexDiff.Removed("mv_active_old_ix"),
                        ],
                    },
                    // A plain → materialized conversion (a recreate carrying the materialization flip).
                    ViewDiff.Modified("app", "hourly_totals") with
                    {
                        Definition = new View { Name = "hourly_totals", Body = "SELECT date_trunc('hour', at), sum(amount) FROM app.sales GROUP BY 1", IsMaterialized = true },
                        IsMaterialized = true,
                        Materialized = new ValueChange<bool>(false, true),
                        RequiresRecreate = true,
                    },
                ],
                },
            ]);
    }

    /// <summary>
    /// A diff exercising every enum change kind: an added enum, anchored value additions, a removal/reorder
    /// (requiring a manual recreate), a comment-only change, a rename, and a removal.
    /// </summary>
    private static DatabaseDiff EnumChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    Enums = [
                    EnumDiff.Added("app", new EnumType { Name = "order_status", Values = ["pending", "shipped", "delivered"] })
                        with { Comment = new ValueChange<string>(null, "order lifecycle") },
                    EnumDiff.Modified("app", "priority") with
                    {
                        AddedValues = [
                            new EnumValueAddition("lowest", Before: "low"),
                            new EnumValueAddition("medium", After: "low"),
                            new EnumValueAddition("highest"),
                        ],
                        Values = new ValueChange<IReadOnlyList<EnumLabel>>(["low", "high"], ["lowest", "low", "medium", "high", "highest"]),
                    },
                    EnumDiff.Modified("app", "severity") with
                    {
                        Values = new ValueChange<IReadOnlyList<EnumLabel>>(["info", "warn", "error"], ["warn", "error"]),
                    },
                    EnumDiff.Modified("app", "kind") with { Comment = new ValueChange<string>("old note", "new note") },
                    EnumDiff.Modified("app", "status") with { RenamedFrom = "state" },
                    EnumDiff.Removed("app", "stale_enum"),
                ],
                },
            ]);
    }

    /// <summary>
    /// A diff exercising every sequence change kind: an added sequence (with and without options), an options
    /// change, a comment-only change, a rename, and a removal.
    /// </summary>
    private static DatabaseDiff SequenceChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    Sequences = [
                    SequenceDiff.Added("app", new Sequence { Name = "order_id",
                            Options = new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MaxValue: 999999, Cache: 10, Cycle: true) })
                        with { Comment = new ValueChange<string>(null, "order numbers") },
                    SequenceDiff.Added("app", new Sequence { Name = "invoice_id" }),
                    SequenceDiff.Modified("app", "ticket_id") with
                    {
                        Options = new ValueChange<SequenceOptions>(
                            new SequenceOptions(StartWith: 1, IncrementBy: 1),
                            new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true)),
                    },
                    SequenceDiff.Modified("app", "audit_id") with
                    {
                        Comment = new ValueChange<string>("old note", "new note"),
                    },
                    SequenceDiff.Modified("app", "batch_id") with { RenamedFrom = "job_id" },
                    SequenceDiff.Removed("app", "stale_seq"),
                ],
                },
            ]);
    }

    [Fact]
    public Task From_RichDiff() => Verify(DiffDocument.From(RichDiff()));

    [Fact]
    public Task From_ViewChanges() => Verify(DiffDocument.From(ViewChangesDiff()));

    /// <summary>
    /// A diff exercising every function change kind: an add (showing arguments), a body-only replace, a
    /// signature change (recreate), a rename, a comment-only change, and a removal — plus a procedure variant.
    /// </summary>
    private static DatabaseDiff RoutineChangesDiff()
    {
        var addTax = new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric, rate numeric", Definition = "RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$" };
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    Routines = [
                        RoutineDiff.Added("app", addTax) with { Comment = new ValueChange<string>(null, "adds tax") },
                        RoutineDiff.Modified("app", "normalize", RoutineKind.Function) with { Definition = new Routine { Name = "normalize", RoutineKind = RoutineKind.Function, Arguments = "code text", Definition = "RETURNS text AS $$ SELECT lower(code) $$" } },
                        RoutineDiff.Modified("app", "score", RoutineKind.Function) with
                        {
                            Definition = new Routine { Name = "score", RoutineKind = RoutineKind.Function, Arguments = "user_id bigint, weight numeric", Definition = "RETURNS numeric AS $$ SELECT 1 $$" },
                            Arguments = new ValueChange<SqlText>("user_id bigint", "user_id bigint, weight numeric"),
                        },
                        RoutineDiff.Modified("app", "renamed_fn", RoutineKind.Function) with { RenamedFrom = "old_fn" },
                        RoutineDiff.Modified("app", "noted", RoutineKind.Function) with
                        {
                            Comment = new ValueChange<string>("old note", "new note"),
                        },
                        RoutineDiff.Removed("app", "stale_fn", RoutineKind.Function),
                        RoutineDiff.Added("app", new Routine { Name = "archive", RoutineKind = RoutineKind.Procedure, Arguments = "before date", Definition = "LANGUAGE sql AS $$ DELETE $$" }),
                        RoutineDiff.Modified("app", "cleanup", RoutineKind.Procedure) with
                        {
                            Definition = new Routine { Name = "cleanup", RoutineKind = RoutineKind.Procedure, Arguments = "", Definition = "LANGUAGE sql AS $$ TRUNCATE $$" },
                            Arguments = new ValueChange<SqlText>("batch int", ""),
                        },
                        RoutineDiff.Removed("app", "stale_proc", RoutineKind.Procedure),
                    ],
                },
            ]);
    }

    /// <summary>
    /// A diff exercising every extension change kind: an add (showing version), a bare add, a version change, a
    /// comment-only change, and a removal — all at the root, since extensions are database-global.
    /// </summary>
    private static DatabaseDiff ExtensionChangesDiff()
    {
        return new DatabaseDiff(
            Extensions:
            [
                ExtensionDiff.Added(new Extension { Name = "postgis", Version = "3.4" })
                        with { Comment = new ValueChange<string>(null, "spatial types") },
                ExtensionDiff.Added(new Extension { Name = "citext" }),
                ExtensionDiff.Modified("vector") with { Version = new ValueChange<string>("0.6.0", "0.7.0") },
                ExtensionDiff.Modified("hstore") with { Comment = new ValueChange<string>("old note", "new note") },
                ExtensionDiff.Removed("legacy_ext"),
            ]);
    }

    /// <summary>
    /// A diff exercising trigger changes on a table: an add, a comment-only modify, and a removal.
    /// </summary>
    private static DatabaseDiff TriggerChangesDiff()
    {
        var audit = new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert | TriggerEvent.Update, Function = new RoutineReference("app", "log"), Level = TriggerLevel.Row };
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    Tables = [
                    TableDiff.Modified("app", "users") with
                    {
                        Triggers = [
                        TriggerDiff.Added(audit),
                        TriggerDiff.CommentChanged("noted", new ValueChange<string>("old note", "new note")),
                        TriggerDiff.Removed("stale_trg"),
                    ],
                    },
                ],
                },
            ]);
    }

    /// <summary>
    /// A diff exercising domain changes: an add, a base-type change (recreate), a default change, a not-null
    /// change, a check add + drop, a rename, a comment-only change, and a removal.
    /// </summary>
    private static DatabaseDiff DomainChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    Domains = [
                    DomainDiff.Added("app", new DomainType { Name = "typeid", DataType = SqlType.Text, NotNull = true })
                        with { Comment = new ValueChange<string>(null, "id as text") },
                    DomainDiff.Modified("app", "code") with
                    {
                        Definition = new DomainType { Name = "code", DataType = SqlType.VarChar(8) },
                        DataType = new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(8)),
                    },
                    DomainDiff.Modified("app", "amount") with
                    {
                        Default = new ValueChange<SqlDefaultExpression>(null, "0"),
                        NotNull = new ValueChange<bool>(false, true),
                        Checks = [CheckConstraintDiff.Added(new CheckConstraint { Name = "amount_pos", Expression = "VALUE >= 0" })],
                    },
                    DomainDiff.Modified("app", "email") with { Checks = [CheckConstraintDiff.Removed("email_fmt")] },
                    DomainDiff.Modified("app", "renamed_d") with { RenamedFrom = "old_d" },
                    DomainDiff.Modified("app", "noted") with { Comment = new ValueChange<string>("old", "new") },
                    DomainDiff.Removed("app", "stale_d"),
                ],
                },
            ]);
    }

    [Fact]
    public Task From_EnumChanges() => Verify(DiffDocument.From(EnumChangesDiff()));

    private static DatabaseDiff CompositeTypeChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                SchemaDiff.Containing("app") with
                {
                    CompositeTypes = [
                    CompositeTypeDiff.Added("app", new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)] })
                        with { Comment = new ValueChange<string>(null, "a postal address") },
                    CompositeTypeDiff.Modified("app", "money") with
                    {
                        Fields = [
                        CompositeFieldDiff.Added(new CompositeField("currency", SqlType.Text)),
                        CompositeFieldDiff.TypeChanged("amount", new ValueChange<SqlType>(SqlType.Int, SqlType.Decimal(18, 2))),
                        CompositeFieldDiff.Removed("legacy"),
                    ],
                    },
                    CompositeTypeDiff.Modified("app", "renamed_t") with { RenamedFrom = "old_t" },
                    CompositeTypeDiff.Modified("app", "noted") with { Comment = new ValueChange<string>("old", "new") },
                    CompositeTypeDiff.Removed("app", "stale_t"),
                ],
                },
            ]);
    }

    [Fact]
    public Task From_DomainChanges() => Verify(DiffDocument.From(DomainChangesDiff()));

    [Fact]
    public Task From_CompositeTypeChanges() => Verify(DiffDocument.From(CompositeTypeChangesDiff()));

    [Fact]
    public Task From_ExtensionChanges() => Verify(DiffDocument.From(ExtensionChangesDiff()));

    [Fact]
    public Task From_TriggerChanges() => Verify(DiffDocument.From(TriggerChangesDiff()));

    [Fact]
    public Task From_RoutineChanges() => Verify(DiffDocument.From(RoutineChangesDiff()));

    [Fact]
    public Task From_SequenceChanges() => Verify(DiffDocument.From(SequenceChangesDiff()));

    /// <summary>
    /// A diff whose changes carry matched data migrations: a required column add backed by a named backfill, a
    /// type change backed by an anonymous migration, and a unique-constraint add backed by a named de-dupe.
    /// </summary>
    private static DatabaseDiff DataMigrationAnnotationsDiff()
    {
        var backfill = ChangeScript("backfill_emails", ChangeTrigger.AddColumn, "email");
        var retype = ChangeScript("retype_totals", ChangeTrigger.AlterColumnType, "total");
        var dedupe = ChangeScript("dedupe_emails", ChangeTrigger.AddConstraint, "users_email_uq");
        var email = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text }) with { MigrationScript = backfill };
        var total = ColumnDiff.Modified(new Column { Name = "total", Type = SqlType.Int })
            with
        { Type = new ValueChange<SqlType>(SqlType.Text, SqlType.Int), MigrationScript = retype };
        var uq = UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }) with { MigrationScript = dedupe };
        var table = TableDiff.Modified("app", "users") with
        {
            Columns = [email, total],
            UniqueConstraints = [uq],
        };
        return new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [table] }]);
    }

    private static ChangeScript ChangeScript(string name, ChangeTrigger trigger, string member) =>
        new(name, $"-- {name}", new ChangeTarget("app", "users", member, trigger));

    [Fact]
    public Task From_DataMigrationAnnotations() => Verify(DiffDocument.From(DataMigrationAnnotationsDiff()));

    /// <summary>
    /// A diff whose root scripts list carries every event kind: deployment bookends and a matched change event.
    /// </summary>
    private static DatabaseDiff ScriptsDiff()
    {
        var backfill = new ChangeScript("backfill_emails", "UPDATE app.users SET email = '';",
            new ChangeTarget("app", "users", "email", ChangeTrigger.AddColumn));
        var email = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text }) with { MigrationScript = backfill };
        var table = TableDiff.Modified("app", "users") with { Columns = [email] };
        return new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [table] }])
        {
            DeploymentScripts =
            [
                new DeploymentScript("seed_roles", "INSERT INTO roles VALUES ('admin');", null, DeploymentPhase.Pre),
                new DeploymentScript("refresh_views", "REFRESH MATERIALIZED VIEW app.stats;", null, DeploymentPhase.Post) { RunCondition = RunCondition.Once },
            ],
        };
    }

    [Fact]
    public Task From_Scripts() => Verify(DiffDocument.From(ScriptsDiff()));

    [Fact]
    public Task From_EmptyDiff() => Verify(DiffDocument.From(new DatabaseDiff([])));
}
