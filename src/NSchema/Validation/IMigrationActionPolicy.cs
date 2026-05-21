using NSchema.Domain.Migration;

namespace NSchema.Validation;

public interface IMigrationActionPolicy
{
    IEnumerable<MigrationActionError> Validate(MigrationPlan plan);
}
