namespace NSchema.Domain;

public record Column(
    string Name,
    SqlType Type,
    bool IsNullable = true,
    bool IsIdentity = false,
    string? DefaultExpression = null,
    string? PreviousName = null
);
