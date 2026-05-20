namespace NSchema.Domain.Schema;

public record PrimaryKey(string Name, IReadOnlyList<string> ColumnNames);
