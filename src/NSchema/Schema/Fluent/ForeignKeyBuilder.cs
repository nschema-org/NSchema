namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides a fluent API for configuring a foreign key constraint in a database schema.
/// </summary>
public sealed class ForeignKeyBuilder
{
    private readonly string _name;
    private readonly IReadOnlyList<string> _columnNames;
    private readonly string _referencedSchema;
    private readonly string _referencedTable;
    private readonly IReadOnlyList<string> _referencedColumnNames;
    private ReferentialAction _onDelete = ReferentialAction.NoAction;
    private ReferentialAction _onUpdate = ReferentialAction.NoAction;

    internal ForeignKeyBuilder(
        string name,
        IReadOnlyList<string> columnNames,
        string referencedSchema,
        string referencedTable,
        IReadOnlyList<string> referencedColumnNames)
    {
        _name = name;
        _columnNames = columnNames;
        _referencedSchema = referencedSchema;
        _referencedTable = referencedTable;
        _referencedColumnNames = referencedColumnNames;
    }

    /// <summary>
    /// Specifies the referential action to take when a referenced row is deleted or updated.
    /// </summary>
    /// <param name="action">The referential action to take when a referenced row is deleted or updated.</param>
    /// <returns>The current <see cref="ForeignKeyBuilder"/> instance, allowing for method chaining.</returns>
    public ForeignKeyBuilder OnDelete(ReferentialAction action) { _onDelete = action; return this; }

    /// <summary>
    /// Specifies the referential action to take when a referenced row is updated.
    /// </summary>
    /// <param name="action">The referential action to take when a referenced row is updated.</param>
    /// <returns>The current <see cref="ForeignKeyBuilder"/> instance, allowing for method chaining.</returns>
    public ForeignKeyBuilder OnUpdate(ReferentialAction action) { _onUpdate = action; return this; }

    internal ForeignKey Build() => new(_name, _columnNames, _referencedSchema, _referencedTable, _referencedColumnNames, _onDelete, _onUpdate);
}
