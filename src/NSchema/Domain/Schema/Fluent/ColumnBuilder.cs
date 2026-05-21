using NSchema.Domain.Schema;

namespace NSchema.Desired.Fluent;

public sealed class ColumnBuilder
{
    private readonly string _name;
    private readonly SqlType _type;
    private bool _isNullable = true;
    private bool _isIdentity;
    private string? _defaultExpression;
    private string? _previousName;

    internal ColumnBuilder(string name, SqlType type)
    {
        _name = name;
        _type = type;
    }

    public ColumnBuilder NotNull() { _isNullable = false; return this; }
    public ColumnBuilder Nullable() { _isNullable = true; return this; }
    public ColumnBuilder Identity() { _isIdentity = true; return this; }
    public ColumnBuilder Default(string expression) { _defaultExpression = expression; return this; }
    public ColumnBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }

    internal Column Build() =>
        new(_name, _type, _isNullable, _isIdentity, _defaultExpression, _previousName);
}
