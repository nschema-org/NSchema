using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// A migration policy that checks for destructive actions in a migration plan and applies the configured policy (allow, warn, or error) accordingly.
/// </summary>
/// <param name="logger">The logger to log warnings about destructive actions when the policy is set to warn.</param>
/// <param name="options">The migration options that contain the configured policy for handling destructive actions.</param>
internal sealed class DestructiveActionMigrationPolicy(ILogger<DestructiveActionMigrationPolicy> logger, IOptions<MigrationOptions> options) : IMigrationPolicy
{
    public IEnumerable<PolicyError> Validate(MigrationPlan plan)
    {
        foreach (var action in plan.Actions.Where(a => a.IsDestructive))
        {
            switch (options.Value.DestructiveActionPolicy)
            {
                case DestructiveActionPolicy.Allow:
                    // Do nothing.
                    break;
                case DestructiveActionPolicy.Warn:
                    logger.LogWarning("Destructive action will be executed: {ActionType}", action.GetType().Name);
                    break;
                case DestructiveActionPolicy.Error:
                default:
                    yield return new PolicyError(
                        nameof(DestructiveActionMigrationPolicy),
                        $"Destructive action blocked by policy: {action.GetType().Name}");
                    break;
            }
        }
    }
}
