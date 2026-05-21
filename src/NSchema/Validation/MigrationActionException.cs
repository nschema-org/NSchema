namespace NSchema.Validation;

public sealed class MigrationActionException(IReadOnlyList<MigrationActionError> errors)
    : Exception($"Migration action validation failed with {errors.Count} error(s).")
{
    public IReadOnlyList<MigrationActionError> Errors { get; } = errors;
}
