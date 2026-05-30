namespace NSchema.Policies;

/// <summary>
/// Represents a finding from a policy check against a schema or migration plan.
/// </summary>
/// <param name="PolicyName">The name of the policy that produced this finding.</param>
/// <param name="Message">A descriptive message about the finding.</param>
/// <param name="Severity">The severity of the finding. Defaults to <see cref="PolicySeverity.Error"/>.</param>
public record PolicyError(string PolicyName, string Message, PolicySeverity Severity = PolicySeverity.Error);
