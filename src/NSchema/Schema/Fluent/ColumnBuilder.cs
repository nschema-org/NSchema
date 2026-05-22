namespace NSchema.Schema.Fluent;

public sealed class ColumnBuilder
{
    private readonly string _name;
    private readonly SqlType _type;
    private readonly TableBuilder _table;
    private bool _isNullable = true;
    private bool _isIdentity;
    private IdentityOptions? _identityOptions;
    private string? _defaultExpression;
    private string? _previousName;
    private string? _comment;

    internal ColumnBuilder(TableBuilder table, string name, SqlType type)
    {
        _table = table;
        _name = name;
        _type = type;
    }

    public TableBuilder PrimaryKey(string name)
    {
        _isNullable = false;
        return _table.PrimaryKey(name, [_name]);
    }

    public ColumnBuilder NotNull() { _isNullable = false; return this; }
    public ColumnBuilder Nullable() { _isNullable = true; return this; }

    public ColumnBuilder Identity(long startWith = 1, long minValue = 1, long incrementBy = 1)
    {
        _isIdentity = true;
        _identityOptions = new IdentityOptions(startWith, minValue, incrementBy);
        return this;
    }

    public ColumnBuilder Default(string expression) { _defaultExpression = expression; return this; }
    public ColumnBuilder Comment(string? comment) { _comment = comment; return this; }
    public ColumnBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }

    internal Column Build() =>
        new(_name, _type, _isNullable, _isIdentity, _defaultExpression, _previousName, _comment, _identityOptions);
}
