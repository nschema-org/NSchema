using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Services.Reporting;

namespace NSchema.Tests.Services;

public sealed class DatabaseRendererTests
{
    [Fact]
    public void Render_EmptySchema_ReportsEmpty()
    {
        DatabaseRenderer.Render(new Database()).ShouldBe("Schema is empty.");
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

        var output = DatabaseRenderer.Render(database);

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

        var output = DatabaseRenderer.Render(database);

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

        var output = DatabaseRenderer.Render(database);

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

        var output = DatabaseRenderer.Render(database);

        output.ShouldContain("view user_orders");
        output.ShouldContain("reads app.users");
        output.ShouldContain("reads app.orders");
    }

    [Fact]
    public void Render_ViewWithoutDependencies_EmitsNoReadsLines()
    {
        var database = new Database { Schemas = [new Schema { Name = "app", Views = [new View { Name = "constants", Body = "SELECT 1" }] }] };

        var output = DatabaseRenderer.Render(database);

        output.ShouldContain("view constants");
        output.ShouldNotContain("reads");
    }

    /// <summary>A schema holding two tables, one of which is the only thing managed.</summary>
    private static Database PartlyManaged() => new()
    {
        Schemas =
        [
            new Schema
            {
                Name = "app",
                Tables =
                [
                    new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
                    new Table { Name = "legacy_audit", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
                ],
            },
        ],
    };

    [Fact]
    public void Render_WithoutAManagedSet_MarksNothing()
    {
        // A live database has no managed set of its own, so nothing about management is claimed.
        var output = DatabaseRenderer.Render(PartlyManaged());

        output.ShouldNotContain("unmanaged");
        output.ShouldNotContain("Managed:");
    }

    [Fact]
    public void Render_WithAManagedSet_MarksWhatIsOutsideIt()
    {
        // Arrange
        var managed = new IdentitySet(
            DatabaseObjects: [DatabaseAddress.Schema("app")],
            SchemaObjects: [ObjectAddress.Table("app", "users")]);

        // Act
        var output = DatabaseRenderer.Render(PartlyManaged(), managed);

        // Assert — only what NSchema does not manage carries a mark, and members never do.
        output.ShouldContain("table legacy_audit [unmanaged]");
        output.ShouldContain("table users\n");
        output.ShouldContain("schema app\n");
        output.ShouldContain("id int not null\n");
        output.ShouldContain("Managed: 2 of 3 recorded objects.");
    }

    [Fact]
    public void Render_ManagedObjectInAnUnmanagedSchema_MarksOnlyTheSchema()
    {
        // Arrange — a project can write into a schema it never declared, so the container stays unmanaged while
        // what it holds does not.
        var managed = new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")]);

        // Act
        var output = DatabaseRenderer.Render(PartlyManaged(), managed);

        // Assert
        output.ShouldContain("schema app [unmanaged]");
        output.ShouldContain("table users\n");
        output.ShouldContain("Managed: 1 of 3 recorded objects.");
    }
}
