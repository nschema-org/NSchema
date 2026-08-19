namespace NSchema.Model;

/// <summary>
/// The declared spellings of a column's expressions.
/// </summary>
/// <param name="Address">The column's address.</param>
/// <param name="Default">The declared default expression, where the column has one.</param>
/// <param name="Generated">The declared generation expression, where the column has one.</param>
public sealed record ColumnExpressionDefinition(
    MemberAddress Address,
    SqlDefaultExpression? Default = null,
    SqlText? Generated = null
);
