namespace NSchema.Domain;

public record TableIndex(string Name, IReadOnlyList<string> ColumnNames, bool IsUnique = false);
