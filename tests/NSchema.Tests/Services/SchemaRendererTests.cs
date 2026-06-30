using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Views;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class SchemaRendererTests
{
    [Fact]
    public void Render_EmptySchema_ReportsEmpty()
    {
        SchemaRenderer.Render(new DatabaseSchema([])).ShouldBe("Schema is empty.");
    }

    [Fact]
    public void Render_RendersSchemaTableAndColumns()
    {
        var users = new Table(
            "users",
            PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
            Columns:
            [
                new Column("id", SqlType.Int),
                new Column("email", SqlType.Text, IsNullable: true),
            ]);
        var schema = new DatabaseSchema([new SchemaDefinition("app", Tables: [users])]);

        var output = SchemaRenderer.Render(schema);

        output.ShouldContain("schema app");
        output.ShouldContain("table users");
        output.ShouldContain("id int not null");
        output.ShouldContain("email text null");
        output.ShouldContain("primary key users_pkey (id)");
    }

    [Fact]
    public void Render_RendersUniqueAndCheckConstraints()
    {
        var users = new Table(
            "users",
            Columns: [new Column("email", SqlType.Text), new Column("age", SqlType.Int)],
            UniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"], Comment: "external code")],
            CheckConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]);
        var schema = new DatabaseSchema([new SchemaDefinition("app", Tables: [users])]);

        var output = SchemaRenderer.Render(schema);

        output.ShouldContain("unique users_email_uq (email) (\"external code\")");
        output.ShouldContain("check users_age_chk (age >= 0)");
    }

    [Fact]
    public void Render_RendersViewWithCommentAndReadsLines()
    {
        var view = new View("active_users", "SELECT id FROM app.users", Comment: "active users",
            DependsOn: [new ViewDependency("app", "users")]);
        var schema = new DatabaseSchema([new SchemaDefinition("app", Views: [view])]);

        var output = SchemaRenderer.Render(schema);

        output.ShouldContain("view active_users (\"active users\")");
        output.ShouldContain("reads app.users");
    }

    [Fact]
    public void Render_RendersEveryReadOfAViewWithMultipleDependencies()
    {
        var view = new View("user_orders", "SELECT * FROM app.users u JOIN app.orders o ON o.user_id = u.id",
            DependsOn: [new ViewDependency("app", "users"), new ViewDependency("app", "orders")]);
        var schema = new DatabaseSchema([new SchemaDefinition("app", Views: [view])]);

        var output = SchemaRenderer.Render(schema);

        output.ShouldContain("view user_orders");
        output.ShouldContain("reads app.users");
        output.ShouldContain("reads app.orders");
    }

    [Fact]
    public void Render_ViewWithoutDependencies_EmitsNoReadsLines()
    {
        var schema = new DatabaseSchema([new SchemaDefinition("app", Views: [new View("constants", "SELECT 1")])]);

        var output = SchemaRenderer.Render(schema);

        output.ShouldContain("view constants");
        output.ShouldNotContain("reads");
    }
}
