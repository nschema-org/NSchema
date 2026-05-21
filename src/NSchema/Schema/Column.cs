namespace NSchema.Schema;

public record Column(
    string Name,
    SqlType Type,
    bool IsNullable = true,
    bool IsIdentity = false,
    string? DefaultExpression = null,
    string? PreviousName = null,
    string? Comment = null,
    IdentityOptions? IdentityOptions = null
);
