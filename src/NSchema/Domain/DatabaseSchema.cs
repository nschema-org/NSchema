namespace NSchema.Domain;

public record DatabaseSchema(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null
);
