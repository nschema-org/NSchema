using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Domain.Migration;
using NSchema.Validation;

namespace NSchema.Migration;

public sealed class DestructiveActionPolicyEnforcer(
    IOptions<MigrationOptions> options,
    ILogger<DestructiveActionPolicyEnforcer> logger
) : IMigrationActionPolicy
{
    public IEnumerable<MigrationActionError> Validate(MigrationPlan plan)
    {
        foreach (var action in plan.Actions.Where(a => a.IsDestructive))
        {
            switch (options.Value.DestructiveActionPolicy)
            {
                case DestructiveActionPolicy.Error:
                    yield return new MigrationActionError(
                        nameof(DestructiveActionPolicyEnforcer),
                        $"Destructive action blocked by policy: {action.GetType().Name}");
                    break;
                case DestructiveActionPolicy.Warn:
                    logger.LogWarning("Destructive action will be executed: {ActionType}", action.GetType().Name);
                    break;
            }
        }
    }
}
