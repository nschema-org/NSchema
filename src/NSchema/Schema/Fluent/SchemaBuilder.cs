namespace NSchema.Schema.Fluent;

public sealed class SchemaBuilder
{
    private readonly string _name;
    private readonly List<TableBuilder> _tables = [];
    private readonly List<string> _droppedTables = [];
    private string? _previousName;
    private bool _isPartial;
    private string? _comment;

    internal SchemaBuilder(string name) => _name = name;

    public TableBuilder Table(string name)
    {
        var builder = new TableBuilder(name);
        _tables.Add(builder);
        return builder;
    }

    public SchemaBuilder Comment(string? comment) { _comment = comment; return this; }
    public SchemaBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }

    public SchemaBuilder AsPartial() { _isPartial = true; return this; }

    public SchemaBuilder DropTable(string name) { _droppedTables.Add(name); return this; }

    internal SchemaDefinition Build() =>
        new(_name, _tables.Select(t => t.Build()).ToList(), _previousName, _isPartial,
            _droppedTables.Count > 0 ? _droppedTables : null, _comment);
}
