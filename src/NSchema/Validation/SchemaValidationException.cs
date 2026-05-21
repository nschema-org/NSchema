namespace NSchema.Validation;

public sealed class SchemaValidationException(IReadOnlyList<SchemaValidationError> errors)
    : Exception($"Schema validation failed with {errors.Count} error(s).")
{
    public IReadOnlyList<SchemaValidationError> Errors { get; } = errors;
}
