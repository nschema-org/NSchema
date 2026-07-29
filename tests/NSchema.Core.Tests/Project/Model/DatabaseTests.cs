using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Project.Model;

public sealed class DatabaseTests
{
    private static Database Db(params Schema[] schemas) => new Database { Schemas = [.. schemas] };

    private static Schema Schema(string name, params Table[] tables) => new Schema { Name = name, Tables = [.. tables] };

    private static Table Table(string name) => new Table { Name = name };

    private static Database Sample() => new Database
    {
        Schemas = [new Schema { Name = "app" }, new Schema { Name = "audit" }, new Schema { Name = "legacy" }],
    };

    [Fact]
    public void FilteredTo_KeepsASchemaTheSetDoesNotName_AsAContainerForWhatItDoes()
    {
        // Arrange — the object is managed, the schema holding it is not.
        var database = Db(Schema("app", Table("users")));
        var identities = new IdentitySet(Objects: [new ObjectAddress("app", "users", ObjectKind.Table)]);

        // Act
        var filtered = database.FilteredTo(identities);

        // Assert
        var schema = filtered.Schemas.ShouldHaveSingleItem();
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
        schema.IsImplicit.ShouldBeTrue();
    }

    [Fact]
    public void FilteredTo_DropsASchemaTheSetNamesNothingIn()
    {
        // Act
        var filtered = Db(Schema("app", Table("users"))).FilteredTo(new IdentitySet(Schemas: [new SchemaAddress("other")]));

        // Assert
        filtered.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void FilteredTo_KeepsANamedSchemaAsItsOwn()
    {
        // Act
        var filtered = Db(Schema("app", Table("users"))).FilteredTo(
            new IdentitySet(Schemas: [new SchemaAddress("app")], Objects: [new ObjectAddress("app", "users", ObjectKind.Table)]));

        // Assert
        filtered.Schemas.ShouldHaveSingleItem().IsImplicit.ShouldBeFalse();
    }

    [Fact]
    public void ScopedTo_RestrictsSchemas()
    {
        var result = Sample().ScopedTo(PlanningScope.To(new SchemaAddress("app")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void ScopedTo_IsCaseSensitive()
    {
        var schema = new Database { Schemas = [new Schema { Name = "App" }] };

        var result = schema.ScopedTo(PlanningScope.To(new SchemaAddress("app")));

        result.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_NamesNotPresent_AreIgnored()
    {
        var result = Sample().ScopedTo(PlanningScope.To(new SchemaAddress("app"), new SchemaAddress("does-not-exist")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void ScopedTo_ObjectTargeted_KeepsTheContainerAndTheTargetAlone()
    {
        // A targeted object cannot live outside its schema, so the container stays in the tree — filtered
        // to the target, with every other schema gone.
        var database = Db(Schema("app", Table("users"), Table("orders")), Schema("audit", Table("log")));

        var result = database.ScopedTo(PlanningScope.To([new ObjectAddress("app", "users")]));

        var app = result.Schemas.ShouldHaveSingleItem();
        app.Name.ShouldBe("app");
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_RestrictsDirectivesToInScopeSchemas()
    {
        // Directives address current reality, so a schema rename keeps its object directives in scope through
        // either side; unrelated schemas' directives drop out.
        SqlIdentifier sales = "sales";
        SqlIdentifier core = "core";
        var project = new ProjectDefinition(
            new Database { Schemas = [new Schema { Name = core }, new Schema { Name = "audit" }] },
            new ProjectDirectives(
                SchemaRenames: [new SchemaRenameDirective(new SchemaAddress(sales), new SchemaAddress(core))],
                ObjectRenames:
                [
                    new ObjectRenameDirective(new ObjectAddress(sales, "old") with { Kind = ObjectKind.Table }, "current"),
                    new ObjectRenameDirective(new ObjectAddress("audit", "stale") with { Kind = ObjectKind.Table }, "fresh"),
                ]));

        var filtered = project.ScopedTo(PlanningScope.To(new SchemaAddress(core))).Directives;

        filtered.SchemaRenames.ShouldHaveSingleItem(); // kept — its To side is in scope
        filtered.ObjectRenames.ShouldHaveSingleItem().From.Schema.ShouldBe(sales); // resolves through the rename
    }

    [Fact]
    public void ScopedTo_ObjectTargeted_DirectivesFollowTheContainmentRule()
    {
        // Arrange
        // A change script rides the table it prepares; a deployment script is a schema-level facet, below
        // the schema and no object, so only a whole-schema scope carries it. Renames stay through either side.
        SqlIdentifier app = "app";
        var users = new ObjectAddress(app, "users");
        var directives = new ProjectDirectives(
            ObjectRenames:
            [
                new ObjectRenameDirective(new ObjectAddress(app, "customers") with { Kind = ObjectKind.Table }, "users"),
                new ObjectRenameDirective(new ObjectAddress(app, "stale") with { Kind = ObjectKind.Table }, "fresh"),
            ],
            MemberRenames: [new MemberRenameDirective(new MemberAddress(app, "users", "mail"), "email")],
            ChangeScripts:
            [
                new ChangeScript("backfill", "UPDATE 1;", new ChangeTarget(app, "users", "email", ChangeTrigger.AddColumn)),
                new ChangeScript("other", "UPDATE 2;", new ChangeTarget(app, "orders", "total", ChangeTrigger.AddColumn)),
            ],
            DeploymentScripts: [new DeploymentScript("seed", "SELECT 1;", new SchemaAddress(app), DeploymentPhase.Pre)]);

        // Act
        var filtered = directives.ScopedTo(PlanningScope.To([users]));

        // Assert
        filtered.ObjectRenames.ShouldHaveSingleItem().To.ShouldBe("users"); // kept through its target side
        filtered.MemberRenames.ShouldHaveSingleItem(); // its owner is the target
        filtered.ChangeScripts.ShouldHaveSingleItem().Name.ShouldBe("backfill");
        filtered.DeploymentScripts.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_AllScope_ReturnsEverything()
    {
        // Act
        var schema = Sample();

        // Assert
        schema.ScopedTo(PlanningScope.All).ShouldBe(schema);
    }

    [Fact]
    public void ScopedTo_EmptyScope_NormalizesToAll()
    {
        // Act
        var schema = Sample();

        // Assert
        schema.ScopedTo(PlanningScope.To()).ShouldBe(schema);
    }
}
