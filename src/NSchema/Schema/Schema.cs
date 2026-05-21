namespace NSchema.Schema;

public record Schema(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null
);
