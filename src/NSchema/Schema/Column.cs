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
    /// <summary>
    /// Creates a new <see cref="Column"/> with the given options, defaulting unspecified members.
    /// </summary>
    /// <param name="name">The name of the column.</param>
    /// <param name="type">The SQL data type of the column.</param>
    /// <param name="isNullable">A boolean value indicating whether the column allows NULL values.</param>
    /// <param name="isIdentity">A boolean value indicating whether the column is an identity column.</param>
    /// <param name="defaultExpression">An optional default expression for the column.</param>
    /// <param name="oldName">The previous name of the column, if it has been renamed.</param>
    /// <param name="comment">An optional comment or description for the column.</param>
    /// <param name="identityOptions">An optional set of options for identity columns, such as seed and increment values.</param>
    public static Column Create(
        string name,
        SqlType type,
        bool isNullable = false,
        bool isIdentity = false,
        string? defaultExpression = null,
        string? oldName = null,
        string? comment = null,
        IdentityOptions? identityOptions = null
    ) => new(name, type, isNullable, isIdentity, defaultExpression, oldName, comment, identityOptions);

    private string DebuggerDisplay =>
        $"{Name} {Type}" +
        (IsNullable ? " NULL" : " NOT NULL") +
        (IsIdentity ? " IDENTITY" : "") +
        (DefaultExpression == null ? "" : $" DEFAULT {DefaultExpression}");
}
