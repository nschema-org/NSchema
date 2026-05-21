namespace NSchema.Schema;

public record SchemaDefinition(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null
);
