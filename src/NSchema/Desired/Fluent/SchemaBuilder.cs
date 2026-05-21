using NSchema.Domain.Schema;

namespace NSchema.Desired.Fluent;

public sealed class SchemaBuilder
{
    private readonly string _name;
    private readonly List<TableBuilder> _tables = [];
    private string? _previousName;

    internal SchemaBuilder(string name) => _name = name;

    public TableBuilder Table(string name)
    {
        var builder = new TableBuilder(name);
        _tables.Add(builder);
        return builder;
    }

    public SchemaBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }

    internal Schema Build() =>
        new(_name, _tables.Select(t => t.Build()).ToList(), _previousName);
}
