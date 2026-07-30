using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Tables;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Helpers;

public static class TestData
{
    public static readonly MigrationAction DestructiveAction = new DropTable(new ObjectAddress("identity", "users"));
    public static readonly MigrationAction NonDestructiveAction = new CreateSchema("identity");

    /// <summary>A diff dropping the <c>identity.users</c> table.</summary>
    public static readonly DatabaseDiff DestructiveDiff = DiffWithDroppedTables("users");

    /// <summary>A diff that only adds a schema.</summary>
    public static readonly DatabaseDiff NonDestructiveDiff = new(
        [SchemaDiff.Added("identity") with
        {
            Grants = [],
            Tables = [],
        }]);

    /// <summary>Builds a diff that drops the named tables from the <c>identity</c> schema.</summary>
    public static DatabaseDiff DiffWithDroppedTables(params string[] tableNames) => new(
        [SchemaDiff.Containing("identity") with
        {
            Grants = [],
            Tables = [.. tableNames.Select(name => TableDiff.Removed("identity", name) with
            {
                Columns = [],
                Grants = [],
                Indexes = [],
                PrimaryKeys = [],
            })],
        }]);

    /// <summary>
    /// A schema exercising every domain feature (identity, facets, comments, foreign keys,
    /// indexes, grants), for serializer round-trip and snapshot coverage.
    /// Shared so the state and document serializers are pinned against the same input.
    /// </summary>
    public static Database RichSchema() => new Database
    {
        Schemas = [
            new Schema {
                Name = "app",

                Tables = [
                    new Table {
                        Name = "users",
                        PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"], Comment = "surrogate key" },

                        Columns = [
                            new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true,
                                IdentityOptions = new IdentityOptions(1, 1, 1) },
                            new Column { Name = "name", Type = SqlType.VarChar(255), Comment = "display name" },
                            new Column { Name = "balance", Type = SqlType.Decimal(18, 2), IsNullable = true, DefaultExpression = "0" },
                            new Column { Name = "code", Type = SqlType.Char(8) },
                            new Column { Name = "metadata", Type = SqlType.Custom("jsonb"), IsNullable = true },
                            new Column { Name = "name_upper", Type = SqlType.Text, IsNullable = true, GeneratedExpression = "upper(name)" },
                        ],
                        ForeignKeys = [
                            new ForeignKey { Name = "users_org_fk", ColumnNames = ["org_id"], References = new ObjectAddress("app", "orgs"), ReferencedColumnNames = ["id"],
                                OnDelete = ReferentialAction.Cascade, OnUpdate = ReferentialAction.SetNull, Comment = "owning org" },
                        ],
                        UniqueConstraints = [
                            new UniqueConstraint { Name = "users_code_uq", ColumnNames = ["code"], Comment = "external code" },
                        ],
                        CheckConstraints = [
                            new CheckConstraint { Name = "users_balance_chk", Expression = "balance >= 0", Comment = "no overdraft" },
                        ],
                        ExclusionConstraints = [
                            new ExclusionConstraint { Name = "users_code_excl",
                                Elements = [new ExclusionElement("=", "code"), new ExclusionElement("&&", Expression: "int4range(0, balance)")],
                                Method = "gist", Predicate = "balance > 0", Comment = "no overlap" },
                        ],
                        Indexes = [
                            new TableIndex { Name = "users_name_ix", Columns = ["name"], IsUnique = true,
                                Predicate = "name IS NOT NULL", Comment = "unique names" },
                            new TableIndex { Name = "users_balance_ix",
                                Columns = [new IndexColumn("balance", Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: "lower(name)")],
                                Method = "btree", Include = ["code"], Comment = "covering balance index" },
                        ],
                        Grants = [new TableGrant("readers", TablePrivilege.All)],
                        Triggers = [
                            new Trigger { Name = "users_audit", Timing = TriggerTiming.After,
                                Events = TriggerEvent.Insert | TriggerEvent.Update, Function = new RoutineReference("app", "log_change"),
                                Level = TriggerLevel.Row, UpdateOfColumns = ["name", "balance"],
                                When = "new.balance > 0", Comment = "audit row changes" },
                            new Trigger { Name = "users_stamp", Timing = TriggerTiming.Before, Events = TriggerEvent.Update,
                                Function = new RoutineReference("app", "touch_updated_at") },
                            // An inline-body (SQL Server-style) trigger: no function, a multi-statement body that
                            // carries its own ';' (so it exercises the dollar-quoted round-trip).
                            new Trigger { Name = "users_guard", Timing = TriggerTiming.InsteadOf, Events = TriggerEvent.Delete,
                                Body = "BEGIN\n  INSERT INTO app.audit (msg) VALUES ('blocked');\n  RETURN;\nEND", Comment = "block deletes" },
                        ], Comment = "all users" },
                ],
                Grants = [new SchemaGrant("app_role")],
                Views = [
                    View("active_users", "SELECT id, name FROM app.users WHERE balance > 0", comment: "currently active users"),
                    View("user_directory", "SELECT name FROM app.active_users"),
                    MaterializedView("daily_balances", "SELECT name, balance FROM app.users",
                        comment: "balances rollup",
                        indexes: [new TableIndex { Name = "daily_balances_name_ix", Columns = ["name"], IsUnique = true, Comment = "by name" }]),
                ],
                Enums = [
                    new EnumType { Name = "order_status", Values = ["pending", "shipped", "delivered"], Comment = "order lifecycle" },
                    new EnumType { Name = "priority", Values = ["low", "high"] },
                ],
                Sequences = [
                    new Sequence { Name = "invoice_id" },
                    new Sequence { Name = "order_id",
                        Options = new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true), Comment = "order numbers" },
                ],
                Routines = [
                    new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric, rate numeric",
                        Definition = "RETURNS numeric LANGUAGE sql AS $$\n  SELECT amount * (1 + rate);\n$$", Comment = "adds tax" },
                    new Routine { Name = "normalize_code", RoutineKind = RoutineKind.Function, Arguments = "code text DEFAULT 'N/A'",
                        Definition = "RETURNS text LANGUAGE sql AS $body$ SELECT upper(code || ';suffix'); $body$" },
                    new Routine { Name = "archive_users", RoutineKind = RoutineKind.Procedure, Arguments = "",
                        Definition = "LANGUAGE sql AS $$\n  DELETE FROM app.users WHERE name <> 'a;b';\n$$", Comment = "archival job" },
                ],
                Domains = [
                    new DomainType { Name = "typeid", DataType = SqlType.Text, Comment = "unique id as text" },
                    new DomainType { Name = "positive_amount", DataType = SqlType.Decimal(18, 2), Default = "0", NotNull = true,
                        Checks = [new CheckConstraint { Name = "positive_amount_chk", Expression = "VALUE >= 0" }] },
                ],
                CompositeTypes = [
                    new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)], Comment = "a postal address" },
                    new CompositeType { Name = "money_amount", Fields = [new CompositeField("amount", SqlType.Decimal(18, 2)), new CompositeField("currency", SqlType.Text)] },
                ], Comment = "application schema" },
        ],
        Extensions = [
            new Extension { Name = "citext" },
            new Extension { Name = "postgis", Version = "3.4", Comment = "spatial types" },
            new Extension { Name = "uuid-ossp", Comment = "uuid generation" },
        ],
    };

    /// <summary>
    /// Directives exercising every directive statement against <see cref="RichSchema"/>: a rename of every
    /// renameable kind (addressing current reality — the schema's current name is <c>legacy_app</c>).
    /// Shared so the writer round-trip pins the whole grammar.
    /// </summary>
    public static ProjectDirectives RichDirectives() => new(
        SchemaRenames: [new SchemaRenameDirective(DatabaseAddress.Schema("legacy_app"), DatabaseAddress.Schema("app"))],
        ObjectRenames:
        [
            new ObjectRenameDirective(ObjectAddress.Table("legacy_app", "members"), "users"),
            new ObjectRenameDirective(ObjectAddress.View("legacy_app", "legacy_directory"), "user_directory"),
            new ObjectRenameDirective(ObjectAddress.Enum("legacy_app", "importance"), "priority"),
            new ObjectRenameDirective(ObjectAddress.Sequence("legacy_app", "bill_id"), "invoice_id"),
            new ObjectRenameDirective(ObjectAddress.Routine("legacy_app", "clean_code"), "normalize_code"),
            new ObjectRenameDirective(ObjectAddress.Domain("legacy_app", "legacy_id"), "typeid"),
            new ObjectRenameDirective(ObjectAddress.CompositeType("legacy_app", "legacy_address"), "address"),
        ],
        MemberRenames: [new MemberRenameDirective(new MemberAddress("legacy_app", "members", "short_code"), "code")]);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null) =>
        new View { Name = name, Body = body, DependsOn = ViewDependencyExtractor.Extract(body, "app"), Comment = comment };

    /// <summary>Builds a materialized view (optionally with indexes), dependencies derived from its body.</summary>
    private static View MaterializedView(string name, string body, string? comment = null, ObjectMemberCollection<TableIndex>? indexes = null) =>
        new View { Name = name, Body = body, DependsOn = ViewDependencyExtractor.Extract(body, "app"), IsMaterialized = true, Indexes = indexes ?? [], Comment = comment };
}
