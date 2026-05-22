using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents a column within a database table.
/// </summary>
/// <param name="Name">The name of the column.</param>
/// <param name="Type">The SQL data type of the column.</param>
/// <param name="IsNullable">A boolean value indicating whether the column allows NULL values.</param>
/// <param name="IsIdentity">A boolean value indicating whether the column is an identity column.</param>
/// <param name="DefaultExpression">An optional default expression for the column.</param>
/// <param name="OldName">The previous name of the column, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the column.</param>
/// <param name="IdentityOptions">An optional set of options for identity columns, such as seed and increment values.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record Column(
    string Name,
    SqlType Type,
    bool IsNullable,
    bool IsIdentity,
    string? DefaultExpression,
    string? OldName,
    string? Comment,
    IdentityOptions? IdentityOptions
)
{
    private string DebuggerDisplay =>
        $"{Name} {Type}" +
        (IsNullable ? " NULL" : " NOT NULL") +
        (IsIdentity ? " IDENTITY" : "") +
        (DefaultExpression == null ? "" : $" DEFAULT {DefaultExpression}");
}
