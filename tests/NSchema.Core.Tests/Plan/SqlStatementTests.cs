using NSchema.Model;
using NSchema.Plan.Domain;

namespace NSchema.Tests.Plan;

public sealed class SqlStatementTests
{
    [Fact]
    public void SqlStatement_KeepsTheConstructorAlreadyBuiltProvidersCall()
    {
        // A record's parameters are its constructor, so adding one deletes the arity that every provider already
        // compiled against calls — and an optional parameter is a compile-time convenience, not a second overload.
        // Providers construct SqlStatement directly, so the deleted arity surfaces as a MissingMethodException at
        // apply time, in providers NSchema does not ship and cannot rebuild. Anything new here goes on as a
        // property.
        var constructor = typeof(SqlStatement).GetConstructor([typeof(SqlText), typeof(bool)]);

        constructor.ShouldNotBeNull(
            "SqlStatement(SqlText, bool) is the arity built providers call; new members must be properties.");
    }

    [Fact]
    public void SqlStatement_CarriesItsActionThroughAWithExpression()
    {
        // Arrange
        var statement = new SqlStatement("SELECT 1");

        // Act
        var stamped = statement with { Action = "CreateTable" };

        // Assert
        statement.Action.ShouldBeNull();
        stamped.Action.ShouldBe("CreateTable");
        stamped.Sql.ShouldBe(statement.Sql);
    }
}
