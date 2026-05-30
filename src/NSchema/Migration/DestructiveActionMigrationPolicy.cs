using Microsoft.Extensions.Options;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// A migration policy that checks for destructive actions and applies the configured policy.
/// Non-fatal outcomes are returned as Info/Warning diagnostics so the pipeline can surface them without aborting.
/// </summary>
internal sealed class DestructiveActionMigrationPolicy(IOptions<MigrationOptions> options) : IMigrationPolicy
{
    public IEnumerable<PolicyError> Validate(MigrationPlan plan)
    {
        var destructiveActions = plan.Actions.Where(a => a.IsDestructive).ToList();
        if (destructiveActions.Count == 0)
        {
            return [];
        }

        var typeString = string.Join(", ", destructiveActions.Select(a => a.GetType().Name).Distinct());

        return options.Value.DestructiveActionPolicy switch
        {
            DestructiveActionPolicy.Allow => [new PolicyError(
                nameof(DestructiveActionMigrationPolicy),
                $"Allowing destructive actions in migration plan: {typeString}.",
                PolicySeverity.Info
            )],
            DestructiveActionPolicy.Warn => [new PolicyError(
                nameof(DestructiveActionMigrationPolicy),
                $"Migration plan contains destructive actions: {typeString}.",
                PolicySeverity.Warning
                )],
            _ => [new PolicyError(
                nameof(DestructiveActionMigrationPolicy),
                $"Destructive actions blocked by policy: {typeString}."
            )]
        };
    }
}
