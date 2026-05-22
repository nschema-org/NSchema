namespace NSchema.Policies;

/// <summary>
/// Represents an error that occurs when a database schema violates a specific policy.
/// </summary>
/// <param name="PolicyName">The name of the policy that was violated.</param>
/// <param name="Message">A descriptive message providing details about the violation of the policy.</param>
public record PolicyError(string PolicyName, string Message);
