namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides a fluent API for configuring an index on a database table.
/// </summary>
public sealed class IndexBuilder
{
    private readonly string _name;
    private readonly IReadOnlyList<string> _columnNames;
    private bool _isUnique;
    private string? _comment;
    private string? _predicate;

    internal IndexBuilder(string name, IReadOnlyList<string> columnNames)
    {
        _name = name;
        _columnNames = columnNames;
    }

    /// <summary>
    /// Specifies that the index being defined should enforce uniqueness on the indexed columns.
    /// </summary>
    /// <returns>>The current <see cref="IndexBuilder"/> instance, allowing for method chaining.</returns>
    public IndexBuilder Unique() { _isUnique = true; return this; }

    /// <summary>
    /// Adds an optional comment or description to the index being defined.
    /// </summary>
    /// <param name="comment">The comment or description to associate with the index.</param>
    /// <returns>The current <see cref="IndexBuilder"/> instance, allowing for method chaining.</returns>
    public IndexBuilder Comment(string? comment) { _comment = comment; return this; }

    /// <summary>
    /// Specifies a predicate for a partial index.
    /// </summary>
    /// <param name="predicate">The predicate SQL expression that defines the condition for the partial index.</param>
    /// <returns>The current <see cref="IndexBuilder"/> instance, allowing for method chaining.</returns>
    public IndexBuilder Where(string predicate) { _predicate = predicate; return this; }

    internal TableIndex Build() => new(_name, _columnNames, _isUnique, _comment, _predicate);
}
