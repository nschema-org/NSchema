using NSchema.Domain.Migration;

namespace NSchema.Policies;

public interface IActionPolicy
{
    IEnumerable<PolicyError> Validate(MigrationPlan plan);
}
