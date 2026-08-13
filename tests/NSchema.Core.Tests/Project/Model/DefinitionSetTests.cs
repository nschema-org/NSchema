using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Indexes;
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
    private static MemberAddress CheckAddress(string table, string name) => MemberAddress.CheckConstraint(_app, table, name);
    private static MemberAddress ColumnAddress(string table, string name) => MemberAddress.Column(_app, table, name);
    private static MemberAddress IndexAddress(string owner, string name) => MemberAddress.Index(_app, owner, name);
    private static MemberAddress ExclusionAddress(string table, string name) => MemberAddress.ExclusionConstraint(_app, table, name);
    private static ObjectAddress DomainAddress(string name) => ObjectAddress.Domain(_app, name);

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
                    Columns =
                    {
                        new Column { Name = "label", Type = SqlType.Text, DefaultExpression = "'a' || 'b'" },
                        new Column { Name = "total", Type = SqlType.Text, GeneratedExpression = "qty * price" },
                        new Column { Name = "plain", Type = SqlType.Text },
                    },
                    Triggers = [new Trigger { Name = "audit", Timing = TriggerTiming.After, Events = TriggerEvent.Insert, Body = "CALL audit()" }],
                    CheckConstraints = { new CheckConstraint { Name = "ck_balance", Expression = "balance >= 10" } },
                    Indexes =
                    {
                        new TableIndex { Name = "ix_active", Columns = ["label"], Predicate = "active = 1" },
                        new TableIndex { Name = "ix_all", Columns = ["label"] },
                    },
                    ExclusionConstraints =
                    {
                        new ExclusionConstraint { Name = "ex_slot", Elements = [new ExclusionElement("&&", "label")], Predicate = "active = 1" },
                    },
                }],
                Domains =
                {
                    new DomainType { Name = "score", DataType = SqlType.Text, Default = "'a' || 'b'",
                        Checks = { new CheckConstraint { Name = "ck_score", Expression = "VALUE >= 0" } } },
                },
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
        // A domain's checks are recorded alongside a table's.
        definitions.Checks.ShouldBe([
            new CheckConstraintDefinition(CheckAddress("users", "ck_balance"), "balance >= 10"),
            new CheckConstraintDefinition(CheckAddress("score", "ck_score"), "VALUE >= 0"),
        ]);

        // Only the filtered index; an unfiltered one has no predicate to disagree about.
        definitions.Indexes.ShouldHaveSingleItem().ShouldBe(new IndexPredicateDefinition(IndexAddress("users", "ix_active"), "active = 1"));
        definitions.Exclusions.ShouldHaveSingleItem().ShouldBe(new ExclusionConstraintDefinition(ExclusionAddress("users", "ex_slot"), "active = 1"));
        definitions.Domains.ShouldHaveSingleItem().ShouldBe(new DomainDefinition(DomainAddress("score"), "'a' || 'b'"));

        // Only the two columns carrying an expression; a column with neither has no spelling to record.
        definitions.Columns.ShouldBe([
            new ColumnExpressionDefinition(ColumnAddress("users", "label"), "'a' || 'b'"),
            new ColumnExpressionDefinition(ColumnAddress("users", "total"), Generated: "qty * price"),
        ]);
    }

    [Fact]
    public void WithDefinitions_SubstitutesTheRecordedSpellings()
    {
        // Arrange — the recorded spellings differ from what the database carries.
        var database = BodyBearing();
        var declared = new DefinitionSet(
            [new ViewDefinition(ViewAddress("active"), "SELECT users.id FROM app.users")],
            [new RoutineDefinition(RoutineAddress("f"), "a integer", "RETURNS integer AS $x$ SELECT a $x$")],
            [new TriggerDefinition(TriggerAddress("users", "audit"), "(true)", null, "CALL app.audit()")])
        {
            // What SQL Server hands back for an author's 'balance >= 10'.
            Checks =
            [
                new CheckConstraintDefinition(CheckAddress("users", "ck_balance"), "([balance]>=(10))"),
                new CheckConstraintDefinition(CheckAddress("score", "ck_score"), "((VALUE >= 0))"),
            ],
            Columns =
            [
                new ColumnExpressionDefinition(ColumnAddress("users", "label"), "('a' || 'b')"),
                new ColumnExpressionDefinition(ColumnAddress("users", "total"), Generated: "((qty * price))"),
            ],
            Indexes = [new IndexPredicateDefinition(IndexAddress("users", "ix_active"), "([active]=(1))")],
            Exclusions = [new ExclusionConstraintDefinition(ExclusionAddress("users", "ex_slot"), "([active]=(1))")],
            Domains = [new DomainDefinition(DomainAddress("score"), "('a' || 'b')")],
        };

        // Act
        var spelled = database.WithDefinitions(declared);

        // Assert
        var schema = spelled.Schemas.ShouldHaveSingleItem();
        schema.Views[0].Body.ShouldBe(new SqlText("SELECT users.id FROM app.users"));
        schema.Routines[0].Arguments.ShouldBe(new SqlText("a integer"));
        schema.Routines[0].Definition.ShouldBe(new SqlText("RETURNS integer AS $x$ SELECT a $x$"));
        schema.Tables[0].Triggers[0].When.ShouldBe(new SqlText("(true)"));
        schema.Tables[0].Triggers[0].Body.ShouldBe(new SqlText("CALL app.audit()"));
        schema.Tables[0].CheckConstraints[0].Expression.ShouldBe(new SqlText("([balance]>=(10))"));
        schema.Tables[0].Columns[0].DefaultExpression.ShouldBe(new SqlDefaultExpression("('a' || 'b')"));
        schema.Tables[0].Columns[1].GeneratedExpression.ShouldBe(new SqlText("((qty * price))"));
        schema.Tables[0].Indexes[0].Predicate.ShouldBe(new SqlText("([active]=(1))"));
        schema.Tables[0].ExclusionConstraints[0].Predicate.ShouldBe(new SqlText("([active]=(1))"));
        schema.Domains[0].Default.ShouldBe(new SqlDefaultExpression("('a' || 'b')"));
        schema.Domains[0].Checks[0].Expression.ShouldBe(new SqlText("((VALUE >= 0))"));
    }

    [Fact]
    public void WithDefinitions_AnExpressionTheEngineNoLongerReports_IsNotRestored()
    {
        // Arrange — the default was dropped in the database, so the recorded spelling must not put it back:
        // that is drift to report, not a spelling to reconcile.
        var database = BodyBearing();
        database.Schemas[0].Tables[0].Columns[0].DefaultExpression = null;
        var declared = new DefinitionSet
        {
            Columns = [new ColumnExpressionDefinition(ColumnAddress("users", "label"), "('a' || 'b')")],
        };

        // Act
        var spelled = database.WithDefinitions(declared);

        // Assert
        spelled.Schemas[0].Tables[0].Columns[0].DefaultExpression.ShouldBeNull();
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

    // Every set operation has to carry every kind. They are written out one kind per line, so a kind added later
    // is dropped by whichever operation nobody remembered — silently, because the result is still a valid set and
    // the loss only shows up as an object that will not settle.
    [Theory]
    [InlineData("Union")]
    [InlineData("Except")]
    [InlineData("Intersect")]
    [InlineData("RestrictedTo")]
    public void SetOperations_CarryEveryKind(string operation)
    {
        // Arrange — one operand holding a definition of every kind, and one holding nothing, so an operation that
        // drops a kind differs from one that keeps it whichever way round it combines them.
        var populated = BodyBearing().Definitions();
        var empty = DefinitionSet.Empty;

        // Act
        var result = operation switch
        {
            "Union" => populated.Union(empty),
            "Except" => populated.Except(empty),
            "Intersect" => populated.Intersect(populated),
            _ => populated.RestrictedTo(populated),
        };

        // Assert
        result.Views.ShouldNotBeEmpty($"{operation} dropped the views.");
        result.Routines.ShouldNotBeEmpty($"{operation} dropped the routines.");
        result.Triggers.ShouldNotBeEmpty($"{operation} dropped the triggers.");
        result.Checks.ShouldNotBeEmpty($"{operation} dropped the checks.");
        result.Columns.ShouldNotBeEmpty($"{operation} dropped the columns.");
        result.Indexes.ShouldNotBeEmpty($"{operation} dropped the indexes.");
        result.Exclusions.ShouldNotBeEmpty($"{operation} dropped the exclusions.");
        result.Domains.ShouldNotBeEmpty($"{operation} dropped the domains.");
    }
}
