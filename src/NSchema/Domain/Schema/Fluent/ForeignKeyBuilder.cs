using NSchema.Domain.Schema;

namespace NSchema.Desired.Fluent;

public sealed class ForeignKeyBuilder
{
    private readonly string _name;
    private readonly IReadOnlyList<string> _columnNames;
    private readonly string _referencedSchema;
    private readonly string _referencedTable;
    private readonly IReadOnlyList<string> _referencedColumnNames;
    private ReferentialAction _onDelete = ReferentialAction.NoAction;
    private ReferentialAction _onUpdate = ReferentialAction.NoAction;

    internal ForeignKeyBuilder(
        string name,
        IReadOnlyList<string> columnNames,
        string referencedSchema,
        string referencedTable,
        IReadOnlyList<string> referencedColumnNames)
    {
        _name = name;
        _columnNames = columnNames;
        _referencedSchema = referencedSchema;
        _referencedTable = referencedTable;
        _referencedColumnNames = referencedColumnNames;
    }

    public ForeignKeyBuilder OnDelete(ReferentialAction action) { _onDelete = action; return this; }
    public ForeignKeyBuilder OnUpdate(ReferentialAction action) { _onUpdate = action; return this; }

    internal ForeignKey Build() =>
        new(_name, _columnNames, _referencedSchema, _referencedTable, _referencedColumnNames, _onDelete, _onUpdate);
}
