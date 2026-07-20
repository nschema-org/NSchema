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
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

/// <summary>
/// Snapshot coverage for <see cref="SchemaRenderer"/>.
/// </summary>
public sealed class SchemaRendererSnapshotTests
{
    /// <summary>Builds a view with its read dependencies (the schema is "app" for each), as the parser would derive them.</summary>
    private static View View(string name, string body, string? comment = null, params string[] reads) =>
        new() { Name = name, Body = body, Comment = comment, DependsOn = [.. reads.Select(r => new ObjectAddress("app", r))] };

    /// <summary>
    /// A schema exercising schema comments and grants, identity/default/nullable/commented columns,
    /// a primary key, a foreign key, unique and partial indexes, table grants, and views (including a
    /// view that reads another view).
    /// </summary>
    private static Database RichSchema()
    {
        var users = new Table
        {
            Name = "users",
            Comment = "all users",
            PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] },
            Columns =
            [
                new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) },
                new Column { Name = "email", Type = SqlType.VarChar(255), Comment = "contact address" },
                new Column { Name = "status", Type = SqlType.Text, IsNullable = true, DefaultExpression = "'active'" },
                new Column { Name = "email_upper", Type = SqlType.Text, IsNullable = true, GeneratedExpression = "upper(email)" },
            ],
            Indexes =
            [
                new TableIndex { Name = "users_email_ix", Columns = ["email"], IsUnique = true },
                new TableIndex { Name = "users_active_ix", Columns = ["status"], Predicate = "status = 'active'" },
                new TableIndex
                {
                    Name = "users_email_low_ix",
                    Columns =
                    [
                        new IndexColumn("status", Sort: IndexSort.Descending, Nulls: IndexNulls.Last),
                        new IndexColumn(Expression: "lower(email)"),
                    ],
                    Method = "btree",
                    Include = ["id"],
                },
            ],
            ExclusionConstraints =
            [
                new ExclusionConstraint
                {
                    Name = "users_span_excl",
                    Elements = [new ExclusionElement("&&", Expression: "int4range(0, id)")],
                    Method = "gist",
                },
            ],
            Grants = [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)],
            Triggers =
            [
                new Trigger
                {
                    Name = "users_audit",
                    Timing = TriggerTiming.After,
                    Events = TriggerEvent.Insert | TriggerEvent.Update,
                    Function = new RoutineReference("app", "log_change"),
                    Level = TriggerLevel.Row,
                    UpdateOfColumns = ["email"],
                    Comment = "audit changes",
                },
            ],
        };

        var orders = new Table
        {
            Name = "orders",
            Columns = [new Column { Name = "id", Type = SqlType.BigInt }, new Column { Name = "user_id", Type = SqlType.BigInt }],
            ForeignKeys =
            [
                new ForeignKey
                {
                    Name = "orders_user_fk",
                    ColumnNames = ["user_id"],
                    References = new ObjectAddress("app", "users"),
                    ReferencedColumnNames = ["id"],
                },
            ],
        };

        return new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Comment = "application schema",
                    Tables = [users, orders],
                    Grants = [new SchemaGrant("readers")],
                    Views =
                    [
                        View("active_users", "SELECT id, email FROM app.users WHERE status = 'active'", "currently active users", "users"),
                        View("user_orders", "SELECT u.email, o.id FROM app.active_users u JOIN app.orders o ON o.user_id = u.id", null, "active_users", "orders"),
                        new View
                        {
                            Name = "order_totals",
                            Body = "SELECT user_id, count(*) FROM app.orders GROUP BY user_id",
                            Comment = "per-user order counts",
                            DependsOn = [new ObjectAddress("app", "orders")],
                            IsMaterialized = true,
                            Indexes = [new TableIndex { Name = "order_totals_user_ix", Columns = ["user_id"], IsUnique = true }],
                        },
                    ],
                    Enums =
                    [
                        new EnumType { Name = "order_status", Values = ["pending", "shipped", "delivered"], Comment = "order lifecycle" },
                        new EnumType { Name = "priority", Values = ["low", "high"] },
                    ],
                    Domains =
                    [
                        new DomainType { Name = "typeid", DataType = SqlType.Text, Comment = "unique id as text" },
                        new DomainType
                        {
                            Name = "positive_amount",
                            DataType = SqlType.Decimal(18, 2),
                            Default = "0",
                            NotNull = true,
                            Checks = [new CheckConstraint { Name = "positive_amount_chk", Expression = "VALUE >= 0" }],
                        },
                    ],
                    CompositeTypes =
                    [
                        new CompositeType
                        {
                            Name = "address",
                            Fields = [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)],
                            Comment = "a postal address",
                        },
                        new CompositeType
                        {
                            Name = "money_amount",
                            Fields = [new CompositeField("amount", SqlType.Decimal(18, 2)), new CompositeField("currency", SqlType.Text)],
                        },
                    ],
                    Sequences =
                    [
                        new Sequence
                        {
                            Name = "order_id",
                            Options = new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, Cache: 10, Cycle: true),
                            Comment = "order numbers",
                        },
                        new Sequence { Name = "invoice_id" },
                    ],
                    Routines =
                    [
                        new Routine
                        {
                            Name = "add_tax",
                            RoutineKind = RoutineKind.Function,
                            Arguments = "amount numeric, rate numeric",
                            Definition = "RETURNS numeric LANGUAGE sql AS $$ SELECT amount * (1 + rate) $$",
                            Comment = "adds tax",
                        },
                        new Routine
                        {
                            Name = "archive_users",
                            RoutineKind = RoutineKind.Procedure,
                            Arguments = "before date",
                            Definition = "LANGUAGE sql AS $$ DELETE FROM app.users $$",
                        },
                    ],
                },
            ],
            Extensions =
            [
                new Extension { Name = "citext" },
                new Extension { Name = "postgis", Version = "3.4", Comment = "spatial types" },
            ],
        };
    }

    [Fact]
    public Task Render_RichSchema() => Verify(SchemaRenderer.Render(RichSchema()));

    [Fact]
    public Task Render_EmptySchema() => Verify(SchemaRenderer.Render(new Database()));
}
