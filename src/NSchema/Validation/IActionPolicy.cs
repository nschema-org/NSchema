using NSchema.Domain.Migration;

namespace NSchema.Validation;

public interface IActionPolicy
{
    IEnumerable<PolicyError> Validate(MigrationPlan plan);
}
