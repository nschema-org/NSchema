using NSchema.Domain.Migration;

namespace NSchema.Migration.Comparison;

internal sealed class InstructionSet
{
    public List<SchemaInstruction> PreScripts { get; } = [];
    public List<SchemaInstruction> ForeignKeyDrops { get; } = [];
    public List<SchemaInstruction> IndexDrops { get; } = [];
    public List<SchemaInstruction> PrimaryKeyDrops { get; } = [];
    public List<SchemaInstruction> SchemaRenames { get; } = [];
    public List<SchemaInstruction> SchemaCreates { get; } = [];
    public List<SchemaInstruction> TableRenames { get; } = [];
    public List<SchemaInstruction> TableCreates { get; } = [];
    public List<SchemaInstruction> ColumnDrops { get; } = [];
    public List<SchemaInstruction> ColumnRenames { get; } = [];
    public List<SchemaInstruction> ColumnAdds { get; } = [];
    public List<SchemaInstruction> ColumnAlters { get; } = [];
    public List<SchemaInstruction> PrimaryKeyAdds { get; } = [];
    public List<SchemaInstruction> ForeignKeyAdds { get; } = [];
    public List<SchemaInstruction> IndexAdds { get; } = [];
    public List<SchemaInstruction> TableDrops { get; } = [];
    public List<SchemaInstruction> SchemaDrops { get; } = [];
    public List<SchemaInstruction> PostScripts { get; } = [];

    public IReadOnlyList<SchemaInstruction> ToList() =>
    [
        ..PreScripts,
        ..ForeignKeyDrops,
        ..IndexDrops,
        ..PrimaryKeyDrops,
        ..SchemaRenames,
        ..SchemaCreates,
        ..TableRenames,
        ..TableCreates,
        ..ColumnDrops,
        ..ColumnRenames,
        ..ColumnAdds,
        ..ColumnAlters,
        ..PrimaryKeyAdds,
        ..ForeignKeyAdds,
        ..IndexAdds,
        ..TableDrops,
        ..SchemaDrops,
        ..PostScripts,
    ];
}
