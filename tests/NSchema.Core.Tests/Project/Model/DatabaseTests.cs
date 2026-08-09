using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Types;
using NSchema.Model.Views;
using NSchema.Model.XmlSchemaCollections;
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
        var identities = new IdentitySet(SchemaObjects: [ObjectAddress.Table("app", "users")]);

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
        var filtered = Db(Schema("app", Table("users"))).FilteredTo(new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema("other")]));

        // Assert
        filtered.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void FilteredTo_KeepsANamedSchemaAsItsOwn()
    {
        // Act
        var filtered = Db(Schema("app", Table("users"))).FilteredTo(
            new IdentitySet(DatabaseObjects: [DatabaseAddress.Schema("app")], SchemaObjects: [ObjectAddress.Table("app", "users")]));

        // Assert
        filtered.Schemas.ShouldHaveSingleItem().IsImplicit.ShouldBeFalse();
    }

    [Fact]
    public void ScopedTo_RestrictsSchemas()
    {
        var result = Sample().ScopedTo(PlanningScope.To(DatabaseAddress.Schema("app")));

        result.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public void ScopedTo_IsCaseSensitive()
    {
        var schema = new Database { Schemas = [new Schema { Name = "App" }] };

        var result = schema.ScopedTo(PlanningScope.To(DatabaseAddress.Schema("app")));

        result.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_NamesNotPresent_AreIgnored()
    {
        var result = Sample().ScopedTo(PlanningScope.To(DatabaseAddress.Schema("app"), DatabaseAddress.Schema("does-not-exist")));

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
                SchemaRenames: [new SchemaRenameDirective(DatabaseAddress.Schema(sales), DatabaseAddress.Schema(core))],
                ObjectRenames:
                [
                    new ObjectRenameDirective(ObjectAddress.Table(sales, "old"), "current"),
                    new ObjectRenameDirective(ObjectAddress.Table("audit", "stale"), "fresh"),
                ]));

        var filtered = project.ScopedTo(PlanningScope.To(DatabaseAddress.Schema(core))).Directives;

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
                new ObjectRenameDirective(ObjectAddress.Table(app, "customers"), "users"),
                new ObjectRenameDirective(ObjectAddress.Table(app, "stale"), "fresh"),
            ],
            MemberRenames: [new MemberRenameDirective(new MemberAddress(app, "users", "mail"), "email")],
            ChangeScripts:
            [
                new ChangeScript("backfill", "UPDATE 1;", new ChangeTarget(app, "users", "email", ChangeTrigger.AddColumn)),
                new ChangeScript("other", "UPDATE 2;", new ChangeTarget(app, "orders", "total", ChangeTrigger.AddColumn)),
            ],
            DeploymentScripts: [new DeploymentScript("seed", "SELECT 1;", app, DeploymentPhase.Pre)]);

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

    /// <summary>
    /// Every schema-level kind has to reach <see cref="Database.Identities"/>, because that set is both what
    /// the planner adopts and what it filters the observed database down to. A kind missing from it is
    /// invisible on the current side, so an object that plainly exists is planned as a create — and the apply
    /// collides with it.
    /// </summary>
    [Fact]
    public void Identities_CoverEverySchemaLevelKind()
    {
        // Arrange — one of everything, so a kind added later fails here until it is carried.
        var database = Db(new Schema
        {
            Name = "app",
            Tables = [Table("users")],
            Views = [new View { Name = "active_users", Body = "select 1" }],
            Enums = [new EnumType { Name = "status", Values = ["on"] }],
            Sequences = [new Sequence { Name = "user_id" }],
            Routines = [new Routine { Name = "touch", RoutineKind = RoutineKind.Procedure, Arguments = "", Definition = "begin end" }],
            Domains = [new DomainType { Name = "email", DataType = SqlType.Text }],
            CompositeTypes = [new CompositeType { Name = "address", Fields = [new CompositeField("street", SqlType.Text)] }],
            NativeTypes = [new NativeType { Name = "geography" }],
            XmlSchemaCollections = [new XmlSchemaCollection { Name = "survey", Body = "<xsd:schema/>" }],
        });

        // Act
        var identities = database.Identities();

        // Assert
        identities.SchemaObjects
            .Select(o => o.Kind)
            .OfType<SchemaObjectKind>()
            .ShouldBe(Enum.GetValues<SchemaObjectKind>(), ignoreOrder: true);
    }
}
