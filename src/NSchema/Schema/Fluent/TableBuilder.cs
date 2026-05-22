namespace NSchema.Schema.Fluent;

public sealed class TableBuilder
{
    private readonly string _name;
    private readonly List<ColumnBuilder> _columns = [];
    private readonly List<ForeignKeyBuilder> _foreignKeys = [];
    private readonly List<IndexBuilder> _indexes = [];
    private readonly List<TableGrant> _grants = [];
    private PrimaryKey? _primaryKey;
    private string? _previousName;
    private string? _comment;

    internal TableBuilder(string name) => _name = name;

    public ColumnBuilder Column(string name, SqlType type)
    {
        var builder = new ColumnBuilder(this, name, type);
        _columns.Add(builder);
        return builder;
    }

    public TableBuilder PrimaryKey(string name, IReadOnlyList<string> columnNames)
    {
        _primaryKey = new PrimaryKey(name, columnNames);
        return this;
    }

    public ForeignKeyBuilder ForeignKey(string name, IReadOnlyList<string> columnNames, string referencedSchema, string referencedTable, IReadOnlyList<string> referencedColumnNames)
    {
        var builder = new ForeignKeyBuilder(name, columnNames, referencedSchema, referencedTable, referencedColumnNames);
        _foreignKeys.Add(builder);
        return builder;
    }

    public IndexBuilder Index(string name, IReadOnlyList<string> columnNames)
    {
        var builder = new IndexBuilder(name, columnNames);
        _indexes.Add(builder);
        return builder;
    }

    public TableBuilder Comment(string? comment) { _comment = comment; return this; }
    public TableBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }
    public TableBuilder Grant(string role, TablePrivilege privileges) { _grants.Add(new TableGrant(role, privileges)); return this; }

    internal Table Build() =>
        new(_name,
            _previousName,
            _primaryKey,
            _comment, _columns.Select(c => c.Build()).ToList(), _foreignKeys.Select(f => f.Build()).ToList(), _indexes.Select(i => i.Build()).ToList(), _grants);
}
