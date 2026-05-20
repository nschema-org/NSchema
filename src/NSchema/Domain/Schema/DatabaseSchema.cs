namespace NSchema.Domain.Schema;

public record DatabaseSchema(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null
);
