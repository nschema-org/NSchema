namespace NSchema.Policies;

/// <summary>
/// Represents an exception that is thrown when a database schema violates one or more policies.
/// </summary>
/// <param name="errors">The collection of policy errors that describe the specific violations that occurred.</param>
public sealed class PolicyViolationException(IReadOnlyList<PolicyError> errors)
    : Exception($"Policy violated with {errors.Count} error(s).")
{
    /// <summary>
    /// Gets the collection of policy errors that describe the specific violations that occurred.
    /// </summary>
    public IReadOnlyList<PolicyError> Errors { get; } = errors;
}
