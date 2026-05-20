namespace NSchema.Domain.Schema;

public record TableIndex(string Name, IReadOnlyList<string> ColumnNames, bool IsUnique = false);
