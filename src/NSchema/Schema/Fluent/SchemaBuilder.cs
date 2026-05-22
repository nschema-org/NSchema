namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides a fluent interface for building a database schema definition.
/// </summary>
public sealed class SchemaBuilder
{
    private readonly string _name;
    private readonly List<TableBuilder> _tables = [];
    private readonly List<string> _droppedTables = [];
    private readonly List<SchemaGrant> _grants = [];
    private string? _oldName;
    private bool _isPartial;
    private string? _comment;

    internal SchemaBuilder(string name) => _name = name;

    /// <summary>
    /// Adds a new table to the schema with the specified name and returns a builder for configuring the table's properties and columns.
    /// </summary>
    /// <param name="name">The name of the table to add to the schema.</param>
    /// <returns>A <see cref="TableBuilder"/> instance that can be used to configure the table's properties and columns.</returns>
    public TableBuilder Table(string name)
    {
        var builder = new TableBuilder(name);
        _tables.Add(builder);
        return builder;
    }

    /// <summary>
    /// Adds an optional comment or description to the schema.
    /// </summary>
    /// <param name="comment">The comment or description to associate with the schema.</param>
    /// <returns>The current <see cref="SchemaBuilder"/> instance, allowing for method chaining.</returns>
    public SchemaBuilder Comment(string? comment)
    {
        _comment = comment;
        return this;
    }

    /// <summary>
    /// Specifies that the schema was previously named with the given name.
    /// </summary>
    /// <param name="oldName">The previous name of the schema before it was renamed to the current name.</param>
    /// <returns>The current <see cref="SchemaBuilder"/> instance, allowing for method chaining.</returns>
    /// <remarks>This is used to indicate that the schema has been renamed from a previous name, which can be important for generating accurate migration scripts when the schema definition changes.</remarks>
    public SchemaBuilder RenamedFrom(string oldName)
    {
        _oldName = oldName;
        return this;
    }

    /// <summary>
    /// Grants the specified role access to the schema.
    /// </summary>
    /// <param name="role">The name of the role to grant access to the schema.</param>
    /// <returns>The current <see cref="SchemaBuilder"/> instance, allowing for method chaining.</returns>
    public SchemaBuilder Grant(string role) { _grants.Add(new SchemaGrant(role)); return this; }

    /// <summary>
    /// Marks the schema as partial, indicating that any tables in the target database that aren't in the desired schema definition should be left unchanged rather than being dropped.
    /// </summary>
    /// <returns>The current <see cref="SchemaBuilder"/> instance, allowing for method chaining.</returns>
    /// <remarks>
    /// This is useful when you want to define only a subset of the tables in a schema and don't want NSchema to drop any existing tables that aren't included in the definition.
    /// </remarks>
    public SchemaBuilder AsPartial() { _isPartial = true; return this; }

    /// <summary>
    /// Marks the table with the specified name for dropping. This indicates that the table should be removed from the database when the migration is applied.
    /// </summary>
    /// <param name="name">The name of the table to drop from the schema.</param>
    /// <returns>The current <see cref="SchemaBuilder"/> instance, allowing for method chaining.</returns>
    /// <remarks>
    /// This is used to indicate that a table should be dropped from the database when the migration is applied, even for partial schemas.
    /// </remarks>
    public SchemaBuilder DropTable(string name) { _droppedTables.Add(name); return this; }

    internal SchemaDefinition Build() =>
        new(_name, _oldName, _isPartial, _comment, _tables.Select(t => t.Build()).ToList(), _droppedTables, _grants);
}
