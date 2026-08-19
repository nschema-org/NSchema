using NSchema.Model.Columns;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for stored generated columns: <c>GENERATED ALWAYS AS (expr) STORED</c>.
/// </summary>
public sealed class NsqlParserGeneratedColumnTests
{
    private static Column ParseColumn(string body) =>
        new TestNsqlParser($"CREATE TABLE app.t ({body});").Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Columns.ShouldHaveSingleItem();

    [Fact]
    public void Parse_Generated_CapturesExpression()
        => ParseColumn("full_name text GENERATED ALWAYS AS (first || ' ' || last) STORED")
            .GeneratedExpression.ShouldBe("first || ' ' || last");

    [Fact]
    public void Parse_PlainColumn_HasNoGeneration()
        => ParseColumn("a int").GeneratedExpression.ShouldBeNull();

    [Fact]
    public void Parse_GeneratedNotNull_CapturesBoth()
    {
        // Act
        var column = ParseColumn("area int NOT NULL GENERATED ALWAYS AS (w * h) STORED");

        // Assert
        column.IsNullable.ShouldBeFalse();
        column.GeneratedExpression.ShouldBe("w * h");
    }

    [Fact]
    public void Parse_Generated_RoundTripsThroughWriter()
    {
        var schema = new TestNsqlParser("CREATE TABLE app.t (w int, h int, area int GENERATED ALWAYS AS (w * h) STORED);").Parse().Database;
        var column = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Columns.Last();
        column.Name.ShouldBe("area");
        column.GeneratedExpression.ShouldBe("w * h");
    }
}
