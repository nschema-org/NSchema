using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
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
using NSchema.Model.Views;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using DatabaseComparer = NSchema.Diff.Domain.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="DatabaseComparer"/>. Demonstrates Verify diffing a complex
/// object graph: the comparer's whole <c>MigrationDiff</c> tree is serialized and pinned, so a change
/// to the projection (a new field, a reordering, a different <c>ChangeKind</c>) surfaces as a readable
/// diff. The per-element assertions in <see cref="DatabaseComparerTests"/> stay as the precise spec.
/// </summary>
public sealed class DatabaseComparerSnapshotTests
{
    private readonly DatabaseComparer _sut = new(NullLogger<DatabaseComparer>.Instance, new SqlEquivalence());

    [Fact]
    public Task Compare_RichSchemas_ProjectsFullDiffTree()
    {
        // Current: an "app" schema with a users table, three views, and a soon-to-be-dropped "scratch" schema that
        // carries its own table, view, enum and sequence (so its removal exercises the contained-object drops).
        var current = new Database
        {
            Schemas = [
            new Schema { Name = "app",
                Tables = [
                    new Table { Name = "users",
                        PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] },
                        Columns = [
                            new Column { Name = "id", Type = SqlType.Int },
                            new Column { Name = "email", Type = SqlType.VarChar(100) },
                            new Column { Name = "legacy_flag", Type = SqlType.Boolean },
                        ] },
                ],
                Views = [
                    View("active_users", "SELECT id FROM app.users WHERE active"),
                    View("legacy_report", "SELECT * FROM app.users"),
                    View("old_summary", "SELECT count(*) FROM app.users"),
                ],
                Enums = [
                    new EnumType { Name = "order_status", Values = ["pending", "shipped"] },
                    new EnumType { Name = "importance", Values = ["low", "high"] },
                    new EnumType { Name = "stale_enum", Values = ["x"] },
                ],
                Sequences = [
                    new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1, IncrementBy: 1) },
                    new Sequence { Name = "stale_seq" },
                ],
                Routines = [
                    new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric", Definition = "RETURNS numeric AS $$ SELECT amount * 1.1 $$" },
                    new Routine { Name = "score", RoutineKind = RoutineKind.Function, Arguments = "user_id bigint", Definition = "RETURNS numeric AS $$ SELECT 1 $$" },
                    new Routine { Name = "stale_fn", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int AS $$ SELECT 0 $$" },
                ],
                Domains = [
                    new DomainType { Name = "code", DataType = SqlType.Text },
                    new DomainType { Name = "stale_domain", DataType = SqlType.Int },
                ],
                CompositeTypes = [
                    new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int), new CompositeField("old_field", SqlType.Text)] },
                    new CompositeType { Name = "stale_type", Fields = [new CompositeField("a", SqlType.Int)] },
                ] },
            new Schema { Name = "scratch",
                Tables = [
                    new Table { Name = "temp_data",
                        PrimaryKey = new PrimaryKey { Name = "temp_data_pkey", ColumnNames = ["id"] },
                        Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "payload", Type = SqlType.Text }] },
                ],
                Views = [View("temp_summary", "SELECT count(*) FROM scratch.temp_data")],
                Enums = [new EnumType { Name = "temp_status", Values = ["draft"] }],
                Sequences = [new Sequence { Name = "temp_seq" }] },
        ],
            Extensions = [
            new Extension { Name = "citext" },
            new Extension { Name = "postgis", Version = "3.3" },
            new Extension { Name = "legacy_ext" },
        ],
        };

        // Desired: id widened, email renamed + retyped, legacy_flag dropped, a new index, a new unique
        // constraint, a new check constraint, and a new "reporting" schema. Views: active_users' body changes
        // (a replace), legacy_report is renamed to report, old_summary is dropped, and user_emails is added
        // (reading another view, so it carries a dependency).
        var desired = new Database
        {
            Schemas = [
            new Schema { Name = "app",
                Tables = [
                    new Table { Name = "users",
                        PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] },
                        Columns = [
                            new Column { Name = "id", Type = SqlType.BigInt },
                            new Column { Name = "email_address", Type = SqlType.Text },
                            new Column { Name = "email_upper", Type = SqlType.Text, IsNullable = true, GeneratedExpression = "upper(email_address)" },
                        ],
                        UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email_address"] }],
                        CheckConstraints = [new CheckConstraint { Name = "users_id_chk", Expression = "id > 0" }],
                        ExclusionConstraints = [new ExclusionConstraint { Name = "users_span_excl",
                            Elements = [new ExclusionElement("&&", Expression: "int4range(0, id)")], Method = "gist" }],
                        // A covering, expression, descending index exercising the richer index grammar.
                        Indexes = [new TableIndex { Name = "users_email_ix",
                            Columns = [new IndexColumn("email_address", Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: "lower(email_address)")],
                            IsUnique = true, Method = "btree", Include = ["id"] }] },
                ],
                Views = [
                    View("active_users", "SELECT id, email_address FROM app.users WHERE active"),
                    View("report", "SELECT * FROM app.users"),
                    View("user_emails", "SELECT email_address FROM app.active_users"),
                ],
                // Enums: a value appended, a rename, a drop, and an addition.
                Enums = [
                    new EnumType { Name = "order_status", Values = ["pending", "shipped", "delivered"] },
                    new EnumType { Name = "priority", Values = ["low", "high"] },
                    new EnumType { Name = "severity", Values = ["info", "error"] },
                ],
                // Sequences: an options change, a drop, and an addition.
                Sequences = [
                    new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true) },
                    new Sequence { Name = "batch_id" },
                ],
                // Routines: a body replace, a signature change (recreate), an addition, and a procedure.
                Routines = [
                    new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric", Definition = "RETURNS numeric AS $$ SELECT amount * 1.2 $$" },
                    new Routine { Name = "score", RoutineKind = RoutineKind.Function, Arguments = "user_id bigint, weight numeric", Definition = "RETURNS numeric AS $$ SELECT 1 $$" },
                    new Routine { Name = "brand_new", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int AS $$ SELECT 42 $$" },
                    new Routine { Name = "archive", RoutineKind = RoutineKind.Procedure, Arguments = "before date", Definition = "LANGUAGE sql AS $$ DELETE $$" },
                ],
                // Domains: code's base type changes (recreate), stale_domain is dropped, postal_code is added.
                Domains = [
                    new DomainType { Name = "code", DataType = SqlType.VarChar(8) },
                    new DomainType { Name = "postal_code", DataType = SqlType.Text, NotNull = true },
                ],
                // Composite types: address retypes a field + adds one + drops one (all in place), stale_type
                // is dropped, and coords is added.
                CompositeTypes = [
                    new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.VarChar(120)), new CompositeField("zip", SqlType.Int), new CompositeField("country", SqlType.Text)] },
                    new CompositeType { Name = "coords", Fields = [new CompositeField("lat", SqlType.Decimal(9, 6)), new CompositeField("lng", SqlType.Decimal(9, 6))] },
                ] },
            new Schema { Name = "reporting", Comment = "analytics" },
        ],
            // Extensions: citext unchanged, postgis version bump, legacy_ext dropped, vector added.
            Extensions = [
            new Extension { Name = "citext" },
            new Extension { Name = "postgis", Version = "3.4" },
            new Extension { Name = "vector", Comment = "embeddings" },
        ],
        };

        // The renames and drops the comment narrates arrive as directives, addressing current reality.
        var directives = new ProjectDirectives(
            MemberRenames: [new MemberRenameDirective(new MemberAddress("app", "users", "email"), "email_address")],
            ObjectRenames:
            [
                new ObjectRenameDirective(new ObjectAddress("app", "legacy_report") with { Kind = ObjectKind.View }, "report"),
                new ObjectRenameDirective(new ObjectAddress("app", "importance") with { Kind = ObjectKind.Enum }, "priority"),
            ]);

        return Verify(Compare(current, desired, directives));
    }

    // Builds a view with its dependencies derived from the body, exactly as the DDL parser would.
    private static View View(string name, string body) =>
        new View { Name = name, Body = body, DependsOn = ViewDependencyExtractor.Extract(body, "app") };

    private DatabaseDiff Compare(Database current, Database desired, ProjectDirectives? directives = null)
    {
        var effective = directives ?? ProjectDirectives.Empty;
        var aligned = DatabaseAligner.Align(current, desired, effective);
        return _sut.Compare(aligned.Require(), desired);
    }
}
