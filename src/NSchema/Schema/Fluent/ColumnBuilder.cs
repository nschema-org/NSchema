namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides a fluent API for configuring a column in a database schema.
/// </summary>
public sealed class ColumnBuilder
{
    private readonly string _name;
    private readonly SqlType _type;
    private readonly TableBuilder _table;
    private bool _isNullable = true;
    private bool _isIdentity;
    private IdentityOptions? _identityOptions;
    private string? _defaultExpression;
    private string? _oldName;
    private string? _comment;

    internal ColumnBuilder(TableBuilder table, string name, SqlType type)
    {
        _table = table;
        _name = name;
        _type = type;
    }

    /// <summary>
    /// Defines a primary key constraint for the column being configured.
    /// </summary>
    /// <param name="name">The name of the primary key constraint to define for the column.</param>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder PrimaryKey(string name)
    {
        _isNullable = false;
        _table.PrimaryKey(name, [_name]);
        return this;
    }

    /// <summary>
    /// Specifies whether the column being configured allows NULL values or not.
    /// </summary>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder NotNull()
    {
        _isNullable = false;
        return this;
    }

    /// <summary>
    /// Specifies that the column being configured allows NULL values.
    /// </summary>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder Nullable()
    {
        _isNullable = true;
        return this;
    }

    /// <summary>
    /// Specifies that the column being configured is an identity column.
    /// </summary>
    /// <param name="startWith">The starting value for the identity column. Defaults to 1.</param>
    /// <param name="minValue">The minimum value for the identity column. Defaults to 1.</param>
    /// <param name="incrementBy">The increment value for the identity column. Defaults to 1.</param>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder Identity(long startWith = 1, long minValue = 1, long incrementBy = 1)
    {
        _isIdentity = true;
        _identityOptions = new IdentityOptions(startWith, minValue, incrementBy);
        return this;
    }

    /// <summary>
    /// Specifies a default expression for the column being configured.
    /// </summary>
    /// <param name="expression">The SQL expression to use as the default value for the column.</param>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder Default(string expression)
    {
        _defaultExpression = expression;
        return this;
    }

    /// <summary>
    /// Adds an optional comment or description to the column being configured.
    /// </summary>
    /// <param name="comment">The comment or description to associate with the column.</param>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder Comment(string? comment)
    {
        _comment = comment;
        return this;
    }

    /// <summary>
    /// Specifies that the column being configured was previously named with the given name.
    /// </summary>
    /// <param name="oldName">The previous name of the column before it was renamed to the current name.</param>
    /// <returns>The current <see cref="ColumnBuilder"/> instance.</returns>
    public ColumnBuilder RenamedFrom(string oldName)
    {
        _oldName = oldName;
        return this;
    }

    internal Column Build() => new(_name, _type, _isNullable, _isIdentity, _defaultExpression, _oldName, _comment, _identityOptions);
}
