namespace NSchema.Validation;

public sealed class PolicyViolationException(IReadOnlyList<PolicyError> errors)
    : Exception($"Policy violated with {errors.Count} error(s).")
{
    public IReadOnlyList<PolicyError> Errors { get; } = errors;
}
