using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class SchemaRendererTests
{
    [Fact]
    public void Render_EmptySchema_ReportsEmpty()
    {
        SchemaRenderer.Render(new Database()).ShouldBe("Schema is empty.");
    }

    [Fact]
    public void Render_RendersSchemaTableAndColumns()
    {
        var users = new Table
        {
            Name = "users",
            PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] },
            Columns =
            [
                new Column { Name = "id", Type = SqlType.Int },
                new Column { Name = "email", Type = SqlType.Text, IsNullable = true },
            ],
        };
        var database = new Database { Schemas = [new Schema { Name = "app", Tables = [users] }] };

        var output = SchemaRenderer.Render(database);

        output.ShouldContain("schema app");
        output.ShouldContain("table users");
        output.ShouldContain("id int not null");
        output.ShouldContain("email text null");
        output.ShouldContain("primary key users_pkey (id)");
    }

    [Fact]
    public void Render_RendersUniqueAndCheckConstraints()
    {
        var users = new Table
        {
            Name = "users",
            Columns = [new Column { Name = "email", Type = SqlType.Text }, new Column { Name = "age", Type = SqlType.Int }],
            UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"], Comment = "external code" }],
            CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
        };
        var database = new Database { Schemas = [new Schema { Name = "app", Tables = [users] }] };

        var output = SchemaRenderer.Render(database);

        output.ShouldContain("unique users_email_uq (email) (\"external code\")");
        output.ShouldContain("check users_age_chk (age >= 0)");
    }

    [Fact]
    public void Render_RendersViewWithCommentAndReadsLines()
    {
        var view = new View
        {
            Name = "active_users",
            Body = "SELECT id FROM app.users",
            Comment = "active users",
            DependsOn = [new ObjectAddress("app", "users")],
        };
        var database = new Database { Schemas = [new Schema { Name = "app", Views = [view] }] };

        var output = SchemaRenderer.Render(database);

        output.ShouldContain("view active_users (\"active users\")");
        output.ShouldContain("reads app.users");
    }

    [Fact]
    public void Render_RendersEveryReadOfAViewWithMultipleDependencies()
    {
        var view = new View
        {
            Name = "user_orders",
            Body = "SELECT * FROM app.users u JOIN app.orders o ON o.user_id = u.id",
            DependsOn = [new ObjectAddress("app", "users"), new ObjectAddress("app", "orders")],
        };
        var database = new Database { Schemas = [new Schema { Name = "app", Views = [view] }] };

        var output = SchemaRenderer.Render(database);

        output.ShouldContain("view user_orders");
        output.ShouldContain("reads app.users");
        output.ShouldContain("reads app.orders");
    }

    [Fact]
    public void Render_ViewWithoutDependencies_EmitsNoReadsLines()
    {
        var database = new Database { Schemas = [new Schema { Name = "app", Views = [new View { Name = "constants", Body = "SELECT 1" }] }] };

        var output = SchemaRenderer.Render(database);

        output.ShouldContain("view constants");
        output.ShouldNotContain("reads");
    }
}
