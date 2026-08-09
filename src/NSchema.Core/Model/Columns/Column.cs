using System.Diagnostics;

namespace NSchema.Model.Columns;

/// <summary>
/// Represents a column within a database table.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Column : ObjectMember, IEquatable<Column>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.Column;

    /// <summary>
    /// The SQL data type of the column.
    /// </summary>
    public required SqlType Type { get; set; }

    /// <summary>
    /// A boolean value indicating whether the column allows NULL values.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// A boolean value indicating whether the column is an identity column.
    /// </summary>
    public bool IsIdentity { get; set; }

    /// <summary>
    /// An optional default expression for the column.
    /// </summary>
    public SqlDefaultExpression? DefaultExpression { get; set; }

    /// <summary>
    /// An optional set of options for identity columns, such as seed and increment values.
    /// </summary>
    public IdentityOptions? IdentityOptions { get; set; }

    /// <summary>
    /// An optional expression for a generated column; mutually exclusive with a default.
    /// </summary>
    public SqlText? GeneratedExpression { get; set; }

    /// <summary>
    /// Whether a generated column's value is written to storage rather than computed on read.
    /// Ignored when <see cref="GeneratedExpression"/> is null.
    /// </summary>
    public bool IsStored { get; set; }

    /// <summary>
    /// Whether this column is the table's row identifier for merge replication.
    /// </summary>
    public bool IsRowGuid { get; set; }

    /// <summary>
    /// The name of the constraint carrying this column's default, where the engine names one.
    /// </summary>
    public SqlIdentifier? DefaultConstraintName { get; set; }

    /// <summary>
    /// The objects the column's expressions call.
    /// An unqualified reference resolves against <paramref name="schema"/>.
    /// </summary>
    public IReadOnlyList<ObjectAddress> References(SqlIdentifier schema)
    {
        var result = new List<ObjectAddress>();

        // A typed xml column cannot exist before the collection it validates against.
        if (Type.Xml is { } xml)
        {
            result.Add(xml.Collection);
        }
        if (GeneratedExpression is { } generated)
        {
            result.AddRange(Services.ExpressionDependencyScanner.CallSites(generated.Value, schema));
        }
        if (DefaultExpression is { } fallback)
        {
            result.AddRange(Services.ExpressionDependencyScanner.CallSites(fallback.Value, schema));
        }
        return [.. result.Distinct()];
    }

    /// <inheritdoc/>
    public override Column Clone() => new()
    {
        Name = Name,
        Type = Type,
        IsNullable = IsNullable,
        IsIdentity = IsIdentity,
        DefaultExpression = DefaultExpression,
        IdentityOptions = IdentityOptions,
        GeneratedExpression = GeneratedExpression,
        IsStored = IsStored,
        IsRowGuid = IsRowGuid,
        DefaultConstraintName = DefaultConstraintName,
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the parent and the comment are excluded.
    /// </summary>
    public bool Equals(Column? other) =>
        other is not null
        && Name == other.Name
        && Type == other.Type
        && IsNullable == other.IsNullable
        && IsIdentity == other.IsIdentity
        && Equals(DefaultExpression, other.DefaultExpression)
        && Equals(IdentityOptions, other.IdentityOptions)
        && Equals(GeneratedExpression, other.GeneratedExpression)
        && IsStored == other.IsStored
        && IsRowGuid == other.IsRowGuid
        && DefaultConstraintName == other.DefaultConstraintName;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Column other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Name, Type, IsNullable, IsIdentity, DefaultExpression, IdentityOptions, GeneratedExpression,
            HashCode.Combine(IsStored, IsRowGuid, DefaultConstraintName));

    private string DebuggerDisplay =>
        $"{Name} {Type}" +
        (IsNullable ? " NULL" : " NOT NULL") +
        (IsIdentity ? " IDENTITY" : "") +
        (DefaultExpression == null ? "" : $" DEFAULT {DefaultExpression}") +
        (GeneratedExpression == null ? "" : $" GENERATED AS {GeneratedExpression}");
}
