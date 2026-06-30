using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.CompositeTypes;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

/// <summary>
/// Snapshot coverage for <see cref="SchemaRenderer"/>.
/// </summary>
public sealed class SchemaRendererSnapshotTests
{
    /// <summary>Builds a view with its read dependencies (the schema is "app" for each), as the parser would derive them.</summary>
    private static View View(string name, string body, string? comment = null, params string[] reads) =>
        new(name, body, null, comment, [.. reads.Select(r => new ViewDependency("app", r))]);

    /// <summary>
    /// A schema exercising schema comments and grants, identity/default/nullable/commented columns,
    /// a primary key, a foreign key, unique and partial indexes, table grants, and views (including a
    /// view that reads another view).
    /// </summary>
    private static DatabaseSchema RichSchema()
    {
        var users = new Table(
            "users",
            Comment: "all users",
            PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
            Columns:
            [
                new Column("id", SqlType.BigInt, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1)),
                new Column("email", SqlType.VarChar(255), Comment: "contact address"),
                new Column("status", SqlType.Text, IsNullable: true, DefaultExpression: "'active'"),
                new Column("email_upper", SqlType.Text, IsNullable: true, GeneratedExpression: "upper(email)"),
            ],
            Indexes:
            [
                new TableIndex("users_email_ix", ["email"], IsUnique: true),
                new TableIndex("users_active_ix", ["status"], Predicate: "status = 'active'"),
                new TableIndex("users_email_low_ix",
                    [new IndexColumn("status", Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn("lower(email)", IsExpression: true)],
                    Method: "btree", Include: ["id"]),
            ],
            ExclusionConstraints:
            [
                new ExclusionConstraint("users_span_excl", [new ExclusionElement("int4range(0, id)", "&&", IsExpression: true)], Method: "gist"),
            ],
            Grants: [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)],
            Triggers:
            [
                new Trigger("users_audit", TriggerTiming.After, TriggerEvent.Insert | TriggerEvent.Update,
                    "app.log_change", TriggerLevel.Row, UpdateOfColumns: ["email"], Comment: "audit changes"),
            ]);

        var orders = new Table(
            "orders",
            Columns: [new Column("id", SqlType.BigInt), new Column("user_id", SqlType.BigInt)],
            ForeignKeys:
            [
                new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"]),
            ]);

        return new DatabaseSchema(
        [
            new SchemaDefinition(
                "app",
                Comment: "application schema",
                Tables: [users, orders],
                Grants: [new SchemaGrant("readers")],
                Views:
                [
                    View("active_users", "SELECT id, email FROM app.users WHERE status = 'active'", "currently active users", "users"),
                    View("user_orders", "SELECT u.email, o.id FROM app.active_users u JOIN app.orders o ON o.user_id = u.id", null, "active_users", "orders"),
                    new View("order_totals", "SELECT user_id, count(*) FROM app.orders GROUP BY user_id",
                        null, "per-user order counts", [new ViewDependency("app", "orders")],
                        IsMaterialized: true,
                        Indexes: [new TableIndex("order_totals_user_ix", ["user_id"], IsUnique: true)]),
                ],
                Enums:
                [
                    new EnumType("order_status", ["pending", "shipped", "delivered"], Comment: "order lifecycle"),
                    new EnumType("priority", ["low", "high"]),
                ],
                Domains:
                [
                    new Domain("typeid", SqlType.Text, Comment: "unique id as text"),
                    new Domain("positive_amount", SqlType.Decimal(18, 2), Default: "0", NotNull: true,
                        Checks: [new CheckConstraint("positive_amount_chk", "VALUE >= 0")]),
                ],
                CompositeTypes:
                [
                    new CompositeType("address", [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)],
                        Comment: "a postal address"),
                    new CompositeType("money_amount", [new CompositeField("amount", SqlType.Decimal(18, 2)), new CompositeField("currency", SqlType.Text)]),
                ],
                Sequences:
                [
                    new Sequence("order_id",
                        new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, Cache: 10, Cycle: true),
                        Comment: "order numbers"),
                    new Sequence("invoice_id"),
                ],
                Routines:
                [
                    new Routine("add_tax", RoutineKind.Function, "amount numeric, rate numeric",
                        "RETURNS numeric LANGUAGE sql AS $$ SELECT amount * (1 + rate) $$", Comment: "adds tax"),
                    new Routine("archive_users", RoutineKind.Procedure, "before date", "LANGUAGE sql AS $$ DELETE FROM app.users $$"),
                ]),
        ],
        Extensions:
        [
            new Extension("citext"),
            new Extension("postgis", Version: "3.4", Comment: "spatial types"),
        ]);
    }

    [Fact]
    public Task Render_RichSchema() => Verify(SchemaRenderer.Render(RichSchema()));

    [Fact]
    public Task Render_EmptySchema() => Verify(SchemaRenderer.Render(new DatabaseSchema([])));
}
