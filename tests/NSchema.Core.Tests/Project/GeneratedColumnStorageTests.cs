using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

/// <summary>
/// Whether a generated column is written to storage or computed on read.
/// </summary>
/// <remarks>
/// Every engine used to be told to store one, because NSQL had only <c>STORED</c> to say and each dialect emitted its
/// own storage keyword unconditionally. A column declared to compute on read came back stored — same values, written
/// on every update, and indexable where the original was not.
/// </remarks>
public sealed class GeneratedColumnStorageTests
{
    private static Database SchemaWith(bool stored) => new()
    {
        Schemas = [new Schema
        {
            Name = "app",
            Tables =
            [
                new Table
                {
                    Name = "orders",
                    Columns =
                    [
                        new Column { Name = "qty", Type = SqlType.Int },
                        new Column { Name = "doubled", Type = SqlType.Int, GeneratedExpression = "qty * 2", IsStored = stored },
                    ],
                },
            ],
        }],
    };

    [Theory]
    [InlineData(true, "STORED")]
    [InlineData(false, "VIRTUAL")]
    public void Storage_IsWritten(bool stored, string keyword)
    {
        // Act
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(SchemaWith(stored), declareSchemas: false));

        // Assert
        ddl.ShouldContain($"GENERATED ALWAYS AS (qty * 2) {keyword}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Storage_SurvivesTheRoundTrip(bool stored)
    {
        // Arrange
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(SchemaWith(stored), declareSchemas: false));

        // Act
        var reparsed = new TestNsqlParser(ddl).Parse().Database;

        // Assert
        reparsed.Schemas.Single().Tables.Single().Columns
            .Single(c => c.Name == "doubled").IsStored.ShouldBe(stored);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Storage_SurvivesACloneAndComparesAsItself(bool stored)
    {
        // Import partitions a database into one file per object, cloning each on the way, so a field the clone does
        // not copy is lost between introspecting a schema and writing it out — which is exactly how this was missed:
        // introspection was right, the writer was right, and the value never reached it.

        // Arrange
        var column = new Column { Name = "doubled", Type = SqlType.Int, GeneratedExpression = "qty * 2", IsStored = stored };

        // Act
        var clone = column.Clone();

        // Assert
        clone.IsStored.ShouldBe(stored);
        clone.ShouldBe(column);

        // And storage is part of what makes two columns the same column, or a change to it would compare equal and
        // never reach a plan.
        clone.IsStored = !stored;
        clone.ShouldNotBe(column);
    }
}
