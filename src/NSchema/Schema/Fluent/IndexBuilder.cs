namespace NSchema.Schema.Fluent;

public sealed class IndexBuilder
{
    private readonly string _name;
    private readonly IReadOnlyList<string> _columnNames;
    private bool _isUnique;

    internal IndexBuilder(string name, IReadOnlyList<string> columnNames)
    {
        _name = name;
        _columnNames = columnNames;
    }

    public IndexBuilder Unique() { _isUnique = true; return this; }

    internal TableIndex Build() => new(_name, _columnNames, _isUnique);
}
