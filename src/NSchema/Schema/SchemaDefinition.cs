namespace NSchema.Schema;

public record SchemaDefinition(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null,
    bool IsPartial = false,
    IReadOnlyList<string>? DroppedTables = null,
    string? Comment = null
);
