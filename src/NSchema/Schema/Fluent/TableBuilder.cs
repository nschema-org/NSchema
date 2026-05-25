namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides a fluent interface for building a database table schema.
/// </summary>
public sealed class TableBuilder
{
    private readonly string _name;
    private readonly List<ColumnBuilder> _columns = [];
    private readonly List<ForeignKeyBuilder> _foreignKeys = [];
    private readonly List<IndexBuilder> _indexes = [];
    private readonly List<TableGrant> _grants = [];
    private PrimaryKey? _primaryKey;
    private string? _oldName;
    private string? _comment;

    internal string Name => _name;
    internal bool IsDropped { get; private set; }

    internal TableBuilder(string name) => _name = name;

    /// <summary>
    /// Marks the table for dropping. This indicates that the table should be removed from the database when the migration is applied.
    /// </summary>
    /// <returns>The current <see cref="TableBuilder"/> instance, allowing for method chaining.</returns>
    /// <remarks>Explicit drops are applied even for partial schemas.</remarks>
    public TableBuilder Dropped() { IsDropped = true; return this; }

    /// <summary>
    /// Adds a new column to the table with the specified name and SQL data type.
    /// </summary>
    /// <param name="name">The name of the column to add to the table.</param>
    /// <param name="type">The SQL data type of the column being added to the table.</param>
    /// <returns>A <see cref="ColumnBuilder"/> instance that can be used to configure the column.</returns>
    public ColumnBuilder Column(string name, SqlType type)
    {
        var builder = new ColumnBuilder(this, name, type);
        _columns.Add(builder);
        return builder;
    }

    /// <summary>
    /// Adds a new column to the table with the specified name and SQL data type.
    /// </summary>
    /// <param name="name">The name of the column to add to the table.</param>
    /// <param name="type">The SQL data type of the column being added to the table.</param>
    /// <param name="configure">A delegate that can be used to configure the column.</param>
    /// <returns>The current <see cref="TableBuilder"/> instance, allowing for method chaining.</returns>
    public TableBuilder Column(string name, SqlType type, Action<ColumnBuilder> configure)
    {
        var builder = new ColumnBuilder(this, name, type);
        _columns.Add(builder);
        configure.Invoke(builder);
        return this;
    }

    /// <summary>
    /// Defines a primary key constraint.
    /// </summary>
    /// <param name="name">The name of the primary key constraint to define for the table.</param>
    /// <param name="columnNames">A list of column names that make up the primary key constraint for the table.</param>
    /// <returns>The current <see cref="TableBuilder"/> instance, allowing for method chaining.</returns>
    public TableBuilder PrimaryKey(string name, IReadOnlyList<string> columnNames)
    {
        _primaryKey = new PrimaryKey(name, columnNames);
        return this;
    }

    /// <summary>
    /// Defines a foreign key constraint.
    /// </summary>
    /// <param name="name">The name of the foreign key constraint to define for the table.</param>
    /// <param name="columnNames">A list of column names in the current table that make up the foreign key constraint.</param>
    /// <param name="referencedSchema">The name of the schema that contains the referenced table for the foreign key constraint.</param>
    /// <param name="referencedTable">The name of the table that is referenced by the foreign key constraint.</param>
    /// <param name="referencedColumnNames">A list of column names in the referenced table that make up the foreign key constraint.</param>
    /// <returns>A <see cref="ForeignKeyBuilder"/> instance that can be used to configure the foreign key.</returns>
    public ForeignKeyBuilder ForeignKey(string name, IReadOnlyList<string> columnNames, string referencedSchema, string referencedTable, IReadOnlyList<string> referencedColumnNames)
    {
        var builder = new ForeignKeyBuilder(name, columnNames, referencedSchema, referencedTable, referencedColumnNames);
        _foreignKeys.Add(builder);
        return builder;
    }

    /// <summary>
    /// Defines a foreign key constraint.
    /// </summary>
    /// <param name="name">The name of the foreign key constraint to define for the table.</param>
    /// <param name="columnNames">A list of column names in the current table that make up the foreign key constraint.</param>
    /// <param name="referencedSchema">The name of the schema that contains the referenced table for the foreign key constraint.</param>
    /// <param name="referencedTable">The name of the table that is referenced by the foreign key constraint.</param>
    /// <param name="referencedColumnNames">A list of column names in the referenced table that make up the foreign key constraint.</param>
    /// <param name="configure">A delegate that can be used to configure the foreign key.</param>
    /// <returns>A <see cref="ForeignKeyBuilder"/> instance that can be used to configure the foreign key.</returns>
    public TableBuilder ForeignKey(string name, IReadOnlyList<string> columnNames, string referencedSchema, string referencedTable, IReadOnlyList<string> referencedColumnNames, Action<ForeignKeyBuilder> configure)
    {
        var builder = new ForeignKeyBuilder(name, columnNames, referencedSchema, referencedTable, referencedColumnNames);
        _foreignKeys.Add(builder);
        configure.Invoke(builder);
        return this;
    }

    /// <summary>
    /// Defines an index on the table.
    /// </summary>
    /// <param name="name">The name of the index to define.</param>
    /// <param name="columnNames">A list of column names that make up the index .</param>
    /// <returns>A <see cref="IndexBuilder"/> instance that can be used to configure the index's properties.</returns>
    public IndexBuilder Index(string name, IReadOnlyList<string> columnNames)
    {
        var builder = new IndexBuilder(name, columnNames);
        _indexes.Add(builder);
        return builder;
    }

    /// <summary>
    /// Defines an index on the table.
    /// </summary>
    /// <param name="name">The name of the index to define.</param>
    /// <param name="columnNames">A list of column names that make up the index .</param>
    /// <param name="configure">A delegate that can be used to configure the index.</param>
    /// <returns>A <see cref="IndexBuilder"/> instance that can be used to configure the index's properties.</returns>
    public TableBuilder Index(string name, IReadOnlyList<string> columnNames, Action<IndexBuilder> configure)
    {
        var builder = new IndexBuilder(name, columnNames);
        _indexes.Add(builder);
        configure.Invoke(builder);
        return this;
    }

    /// <summary>
    /// Adds an optional comment or description to the table.
    /// </summary>
    /// <param name="comment">The comment or description to associate with the table.</param>
    /// <returns>The current <see cref="TableBuilder"/> instance, allowing for method chaining.</returns>
    public TableBuilder Comment(string? comment)
    {
        _comment = comment;
        return this;
    }

    /// <summary>
    /// Specifies that the table was previously named with the given name.
    /// </summary>
    /// <param name="oldName">The previous name of the table before it was renamed to the current name.</param>
    /// <returns>The current <see cref="TableBuilder"/> instance, allowing for method chaining.</returns>
    public TableBuilder RenamedFrom(string oldName)
    {
        _oldName = oldName;
        return this;
    }

    /// <summary>
    /// Grants the specified role access to the table with the specified privileges.
    /// </summary>
    /// <param name="role">The name of the role to grant access to the table.</param>
    /// <param name="privileges">The privileges to grant to the specified role for the table.</param>
    /// <returns>The current <see cref="TableBuilder"/> instance, allowing for method chaining.</returns>
    public TableBuilder Grant(string role, TablePrivilege privileges)
    {
        _grants.Add(new TableGrant(role, privileges));
        return this;
    }

    internal Table Build() => new(_name,
        _oldName,
        _primaryKey,
        _comment,
        _columns.Select(c => c.Build()).ToList(),
        _foreignKeys.Select(f => f.Build()).ToList(),
        _indexes.Select(i => i.Build()).ToList(),
        _grants
    );
}
