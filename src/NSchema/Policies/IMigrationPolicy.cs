using NSchema.Migration;

namespace NSchema.Policies;

/// <summary>
///
/// </summary>
public interface IMigrationPolicy
{
    IEnumerable<PolicyError> Validate(MigrationPlan plan);
}
