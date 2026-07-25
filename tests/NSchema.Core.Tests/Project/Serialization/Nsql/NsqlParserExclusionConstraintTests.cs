using NSchema.Model.Constraints;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for exclusion constraints:
/// <c>CONSTRAINT n EXCLUDE [USING method] (element WITH operator, …) [WHERE (predicate)]</c>.
/// </summary>
public sealed class NsqlParserExclusionConstraintTests
{
    private static ExclusionConstraint ParseExclusion(string constraint) =>
        new TestNsqlParser($"CREATE TABLE app.bookings (room int, during int, {constraint});").Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().ExclusionConstraints.ShouldHaveSingleItem();

    [Fact]
    public void Parse_SimpleExclusion_CapturesElementsAndMethod()
    {
        // Act
        var exclusion = ParseExclusion("CONSTRAINT no_overlap EXCLUDE USING gist (room WITH =, during WITH &&)");

        // Assert
        exclusion.Name.ShouldBe("no_overlap");
        exclusion.Method.ShouldBe("gist");
        exclusion.Elements.Count.ShouldBe(2);
        exclusion.Elements[0].Column.ShouldBe("room");
        exclusion.Elements[0].Operator.ShouldBe("=");
        exclusion.Elements[1].Column.ShouldBe("during");
        exclusion.Elements[1].Operator.ShouldBe("&&");
    }

    [Fact]
    public void Parse_NoMethod_IsNull()
        => ParseExclusion("CONSTRAINT x EXCLUDE (room WITH =)").Method.ShouldBeNull();

    [Fact]
    public void Parse_Predicate_IsCaptured()
        => ParseExclusion("CONSTRAINT x EXCLUDE USING gist (during WITH &&) WHERE (room > 0)")
            .Predicate.ShouldBe("room > 0");

    [Fact]
    public void Parse_ExpressionElement_IsCaptured()
    {
        // Act
        var element = ParseExclusion("CONSTRAINT x EXCLUDE USING gist ((int4range(room, during)) WITH &&)")

        // Assert
            .Elements.ShouldHaveSingleItem();
        element.Column.ShouldBeNull();
        element.Expression.ShouldBe("int4range(room, during)");
        element.Operator.ShouldBe("&&");
    }

    [Fact]
    public void Parse_WithDocComment_AttachesComment()
        => ParseExclusion("--- no double-booking\n  CONSTRAINT no_overlap EXCLUDE USING gist (during WITH &&)")
            .Comment.ShouldBe("no double-booking");

    [Fact]
    public void Parse_Exclusion_RoundTripsThroughWriter()
    {
        var schema = new TestNsqlParser(
            "CREATE TABLE app.bookings (room int, during int, " +
            "CONSTRAINT no_overlap EXCLUDE USING gist (room WITH =, during WITH &&) WHERE (room > 0));").Parse().Database;

        var exclusion = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().ExclusionConstraints.ShouldHaveSingleItem();
        exclusion.Name.ShouldBe("no_overlap");
        exclusion.Method.ShouldBe("gist");
        exclusion.Predicate.ShouldBe("room > 0");
        exclusion.Elements.Select(e => (Column: e.Column?.Value, e.Operator)).ShouldBe([("room", "="), ("during", "&&")]);
    }
}
