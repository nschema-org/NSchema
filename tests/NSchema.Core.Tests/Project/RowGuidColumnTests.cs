using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

/// <summary>
/// The row-guid marker a table carries for merge replication.
/// </summary>
/// <remarks>
/// It constrains nothing and changes no value, which is exactly why losing it was invisible: the column round-tripped
/// with the same type, the same default and the same nullability, and stopped being the column a merge agent looks
/// for.
/// </remarks>
public sealed class RowGuidColumnTests
{
    private static Database DatabaseWith(bool rowGuid) => new()
    {
        Schemas = [new Schema
        {
            Name = "app",
            Tables =
            [
                new Table
                {
                    Name = "orders",
                    Columns = [new Column { Name = "rowguid", Type = SqlType.Text, IsRowGuid = rowGuid }],
                },
            ],
        }],
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RowGuid_SurvivesTheRoundTrip(bool rowGuid)
    {
        // Arrange
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(DatabaseWith(rowGuid), declareSchemas: false));

        // Act
        var reparsed = new TestNsqlParser(ddl).Parse().Database;

        // Assert
        ddl.Contains("ROWGUIDCOL").ShouldBe(rowGuid);
        reparsed.Schemas.Single().Tables.Single().Columns.Single().IsRowGuid.ShouldBe(rowGuid);
    }

    [Fact]
    public void RowGuid_SurvivesACloneAndComparesAsItself()
    {
        // Import clones every object on its way to a file, so a field the clone forgets never reaches the writer —
        // which is how the storage flag was lost before this one.

        // Arrange
        var column = new Column { Name = "rowguid", Type = SqlType.Text, IsRowGuid = true };

        // Act
        var clone = column.Clone();

        // Assert
        clone.IsRowGuid.ShouldBeTrue();
        clone.ShouldBe(column);

        clone.IsRowGuid = false;
        clone.ShouldNotBe(column);
    }
}
