namespace NSchema.Domain;

public record ForeignKey(
    string Name,
    IReadOnlyList<string> ColumnNames,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumnNames,
    ReferentialAction OnDelete = ReferentialAction.NoAction,
    ReferentialAction OnUpdate = ReferentialAction.NoAction
);
