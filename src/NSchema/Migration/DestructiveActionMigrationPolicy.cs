using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// A migration policy that checks for destructive actions in a migration plan and applies the configured policy (allow, warn, or error) accordingly.
/// </summary>
/// <param name="reporter">The reporter used to surface warnings about destructive actions when the policy is set to warn.</param>
/// <param name="options">The migration options that contain the configured policy for handling destructive actions.</param>
internal sealed class DestructiveActionMigrationPolicy(IMigrationReporter reporter, IOptions<MigrationOptions> options) : IMigrationPolicy
{
    public IEnumerable<PolicyError> Validate(MigrationPlan plan)
    {
        var destructiveActions = plan.Actions.Where(a => a.IsDestructive).ToList();
        if (destructiveActions.Count == 0)
        {
            reporter.Info("No destructive actions detected in migration plan.");
            return [];
        }

        var typeList = destructiveActions.Select(a => a.GetType().Name).Distinct().ToList();
        var typeString = string.Join(", ", typeList);

        switch (options.Value.DestructiveActionPolicy)
        {
            case DestructiveActionPolicy.Allow:
                reporter.Info($"Allowing destructive actions detected in migration plan: {typeString}.");
                return [];
            case DestructiveActionPolicy.Warn:
                reporter.Warn($"Migration plan contains destructive actions: {typeString}");
                return [];
            case DestructiveActionPolicy.Error:
            default:
                return [new PolicyError(
                    nameof(DestructiveActionMigrationPolicy),
                    $"Destructive actions blocked by policy: {typeString}"
                )];
        }
    }
}
