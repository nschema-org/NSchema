using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Domain.Migration;
using NSchema.Policies;

namespace NSchema.Migration;

public sealed class DestructiveActionPolicyEnforcer(
    ILogger<DestructiveActionPolicyEnforcer> logger,
    IOptions<MigrationOptions> options
) : IActionPolicy
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
                        nameof(DestructiveActionPolicyEnforcer),
                        $"Destructive action blocked by policy: {action.GetType().Name}");
                    break;
            }
        }
    }
}
