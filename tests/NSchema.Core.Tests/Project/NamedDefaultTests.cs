using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project;

/// <summary>
/// The name of the constraint carrying a column's default, where an engine names one.
/// </summary>
/// <remarks>
/// Losing it is not losing the default — the column still defaults to the same thing. What is lost is the ability to
/// refer to it: an engine that names its own invents something like <c>DF__Departmen__Modif__37A5467C</c>, which
/// cannot be predicted, so dropping or replacing the default afterwards needs a lookup rather than a statement.
/// </remarks>
public sealed class NamedDefaultTests
{
    private static Database DatabaseWith(string? constraintName) => new()
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
                        new Column
                        {
                            Name = "created",
                            Type = SqlType.Text,
                            DefaultExpression = "now()",
                            DefaultConstraintName = constraintName is null ? null : new SqlIdentifier(constraintName),
                        },
                    ],
                },
            ],
        }],
    };

    [Fact]
    public void Name_SurvivesTheRoundTrip()
    {
        // Arrange
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(DatabaseWith("df_orders_created"), declareSchemas: false));

        // Act
        var reparsed = new TestNsqlParser(ddl).Parse().Database;

        // Assert
        ddl.ShouldContain("CONSTRAINT df_orders_created DEFAULT now()");
        reparsed.Schemas.Single().Tables.Single().Columns.Single()
            .DefaultConstraintName.ShouldBe(new SqlIdentifier("df_orders_created"));
    }

    [Fact]
    public void NoName_WritesAPlainDefault()
    {
        // An unnamed default is the common case and must not grow a CONSTRAINT clause with nothing to put in it.

        // Act
        var ddl = NsqlWriter.Write(SyntaxBuilder.Build(DatabaseWith(null), declareSchemas: false));

        // Assert
        ddl.ShouldContain("DEFAULT now()");
        ddl.ShouldNotContain("CONSTRAINT");
    }

    [Fact]
    public void Name_SurvivesACloneAndComparesAsItself()
    {
        // Arrange
        var column = new Column { Name = "created", Type = SqlType.Text, DefaultExpression = "now()", DefaultConstraintName = new SqlIdentifier("df_x") };

        // Act
        var clone = column.Clone();

        // Assert
        clone.DefaultConstraintName.ShouldBe(new SqlIdentifier("df_x"));
        clone.ShouldBe(column);

        clone.DefaultConstraintName = new SqlIdentifier("df_y");
        clone.ShouldNotBe(column);
    }
}
