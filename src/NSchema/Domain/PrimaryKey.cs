namespace NSchema.Domain;

public record PrimaryKey(string Name, IReadOnlyList<string> ColumnNames);
