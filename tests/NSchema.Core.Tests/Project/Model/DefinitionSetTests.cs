using NSchema.Model;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;

namespace NSchema.Tests.Project.Model;

public sealed class DefinitionSetTests
{
    private static readonly SqlIdentifier _app = new("app");

    private static ObjectAddress ViewAddress(string name) => ObjectAddress.View(_app, name);
    private static ObjectAddress RoutineAddress(string name) => ObjectAddress.Routine(_app, name);
    private static MemberAddress TriggerAddress(string table, string name) => MemberAddress.Trigger(_app, table, name);

    /// <summary>A database holding one body-bearing object of each kind.</summary>
    private static Database BodyBearing() => new()
    {
        Schemas =
        [
            new Schema
            {
                Name = _app,
                Views = [new View { Name = "active", Body = "SELECT id FROM users" }],
                Routines = [new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "a int", Definition = "RETURNS int AS $$ SELECT a $$" }],
                Tables = [new Table
                {
                    Name = "users",
                    Triggers = [new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Body = "CALL audit()" }],
                }],
            },
        ],
    };

    [Fact]
    public void Definitions_CoversEveryBodyBearingKind()
    {
        // Act
        var definitions = BodyBearing().Definitions();

        // Assert
        definitions.Views.ShouldHaveSingleItem().ShouldBe(new ViewDefinition(ViewAddress("active"), "SELECT id FROM users"));
        definitions.Routines.ShouldHaveSingleItem().ShouldBe(new RoutineDefinition(RoutineAddress("f"), "a int", "RETURNS int AS $$ SELECT a $$"));
        definitions.Triggers.ShouldHaveSingleItem().ShouldBe(new TriggerDefinition(TriggerAddress("users", "audit"), null, null, "CALL audit()"));
    }

    [Fact]
    public void WithDefinitions_SubstitutesTheRecordedSpellings()
    {
        // Arrange — the recorded spellings differ from what the database carries.
        var database = BodyBearing();
        var declared = new DefinitionSet(
            [new ViewDefinition(ViewAddress("active"), "SELECT users.id FROM app.users")],
            [new RoutineDefinition(RoutineAddress("f"), "a integer", "RETURNS integer AS $x$ SELECT a $x$")],
            [new TriggerDefinition(TriggerAddress("users", "audit"), "(true)", null, "CALL app.audit()")]);

        // Act
        var spelled = database.WithDefinitions(declared);

        // Assert
        var schema = spelled.Schemas.ShouldHaveSingleItem();
        schema.Views[0].Body.ShouldBe(new SqlText("SELECT users.id FROM app.users"));
        schema.Routines[0].Arguments.ShouldBe(new SqlText("a integer"));
        schema.Routines[0].Definition.ShouldBe(new SqlText("RETURNS integer AS $x$ SELECT a $x$"));
        schema.Tables[0].Triggers[0].When.ShouldBe(new SqlText("(true)"));
        schema.Tables[0].Triggers[0].Body.ShouldBe(new SqlText("CALL app.audit()"));
    }

    [Fact]
    public void WithDefinitions_LeavesTheSourceDatabaseAlone()
    {
        // Arrange
        var database = BodyBearing();

        // Act
        database.WithDefinitions(new DefinitionSet([new ViewDefinition(ViewAddress("active"), "SELECT 2")]));

        // Assert
        database.Schemas[0].Views[0].Body.ShouldBe(new SqlText("SELECT id FROM users"));
    }

    [Fact]
    public void WithDefinitions_ObjectWithoutAnEntry_KeepsItsOwnText()
    {
        // Act — an entry for another view touches nothing else.
        var spelled = BodyBearing().WithDefinitions(new DefinitionSet([new ViewDefinition(ViewAddress("other"), "SELECT 2")]));

        // Assert
        spelled.Schemas[0].Views[0].Body.ShouldBe(new SqlText("SELECT id FROM users"));
    }

    [Fact]
    public void WithDefinitions_Empty_ReturnsTheSameInstance()
    {
        var database = BodyBearing();
        database.WithDefinitions(DefinitionSet.Empty).ShouldBeSameAs(database);
    }

    [Fact]
    public void ScopedTo_KeepsTheDefinitionsTheScopeCovers()
    {
        // Arrange
        var set = new DefinitionSet(
            [new ViewDefinition(ViewAddress("active"), "SELECT 1")],
            [new RoutineDefinition(ObjectAddress.Routine("billing", "f"), "a int", "SELECT 1")],
            [new TriggerDefinition(TriggerAddress("users", "audit"), null, null, "CALL audit()")]);

        // Act
        var covered = set.ScopedTo(PlanningScope.To(DatabaseAddress.Schema(_app)));

        // Assert — the trigger rides its schema like any other member.
        covered.Views.ShouldHaveSingleItem();
        covered.Routines.ShouldBeEmpty();
        covered.Triggers.ShouldHaveSingleItem();
    }

    [Fact]
    public void Union_MergesDistinct()
    {
        // Arrange
        var left = new DefinitionSet([new ViewDefinition(ViewAddress("a"), "SELECT 1")]);
        var right = new DefinitionSet([new ViewDefinition(ViewAddress("b"), "SELECT 2")]);

        // Act
        var union = left.Union(right);

        // Assert
        union.Views.Select(v => v.Address.Name).ShouldBe([new SqlIdentifier("a"), new SqlIdentifier("b")]);
    }

    [Fact]
    public void Except_RemovesByValue()
    {
        // Arrange
        var set = new DefinitionSet([new ViewDefinition(ViewAddress("a"), "SELECT 1"), new ViewDefinition(ViewAddress("b"), "SELECT 2")]);

        // Act
        var remaining = set.Except(new DefinitionSet([new ViewDefinition(ViewAddress("a"), "SELECT 1")]));

        // Assert
        remaining.Views.ShouldHaveSingleItem().Address.Name.ShouldBe(new SqlIdentifier("b"));
    }

    [Fact]
    public void RestrictedTo_Identities_KeepsManagedObjects_AndTriggersUnderThem()
    {
        // Arrange — the routine is unmanaged; the second trigger's table is unmanaged.
        var set = new DefinitionSet(
            [new ViewDefinition(ViewAddress("active"), "SELECT 1")],
            [new RoutineDefinition(RoutineAddress("f"), "a int", "SELECT 1")],
            [
                new TriggerDefinition(TriggerAddress("users", "audit"), null, null, "CALL audit()"),
                new TriggerDefinition(TriggerAddress("other", "audit"), null, null, "CALL audit()"),
            ]);
        var managed = new IdentitySet(SchemaObjects: [ViewAddress("active"), ObjectAddress.Table(_app, "users")]);

        // Act
        var restricted = set.RestrictedTo(managed);

        // Assert
        restricted.Views.ShouldHaveSingleItem();
        restricted.Routines.ShouldBeEmpty();
        restricted.Triggers.ShouldHaveSingleItem().Address.ShouldBe(TriggerAddress("users", "audit"));
    }

    [Fact]
    public void RestrictedTo_Definitions_MatchesByAddress_NotByText()
    {
        // Arrange — the counterpart spells the body differently; the address alone decides.
        var set = new DefinitionSet([new ViewDefinition(ViewAddress("a"), "SELECT 1"), new ViewDefinition(ViewAddress("b"), "SELECT 2")]);
        var other = new DefinitionSet([new ViewDefinition(ViewAddress("a"), "SELECT 999")]);

        // Act
        var restricted = set.RestrictedTo(other);

        // Assert
        restricted.Views.ShouldHaveSingleItem().ShouldBe(new ViewDefinition(ViewAddress("a"), "SELECT 1"));
    }
}
