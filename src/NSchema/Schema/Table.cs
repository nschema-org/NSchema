namespace NSchema.Schema;

public record Table(
    string Name,
    IReadOnlyList<Column> Columns,
    PrimaryKey? PrimaryKey = null,
    IReadOnlyList<ForeignKey>? ForeignKeys = null,
    IReadOnlyList<TableIndex>? Indexes = null,
    string? PreviousName = null
);
