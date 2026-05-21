namespace NSchema.Schema.Fluent;

public sealed class ColumnBuilder
{
    private readonly string _name;
    private readonly SqlType _type;
    private bool _isNullable = true;
    private bool _isIdentity;
    private IdentityOptions? _identityOptions;
    private string? _defaultExpression;
    private string? _previousName;
    private string? _comment;

    internal ColumnBuilder(string name, SqlType type)
    {
        _name = name;
        _type = type;
    }

    public ColumnBuilder NotNull() { _isNullable = false; return this; }
    public ColumnBuilder Nullable() { _isNullable = true; return this; }

    public ColumnBuilder Identity(long? startWith = null, long? minValue = null, long? incrementBy = null)
    {
        _isIdentity = true;
        if (startWith.HasValue || minValue.HasValue || incrementBy.HasValue)
            _identityOptions = new IdentityOptions(startWith, minValue, incrementBy);
        return this;
    }

    public ColumnBuilder Default(string expression) { _defaultExpression = expression; return this; }
    public ColumnBuilder Comment(string? comment) { _comment = comment; return this; }
    public ColumnBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }

    internal Column Build() =>
        new(_name, _type, _isNullable, _isIdentity, _defaultExpression, _previousName, _comment, _identityOptions);
}
