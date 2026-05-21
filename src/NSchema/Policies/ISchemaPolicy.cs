using NSchema.Domain.Schema;

namespace NSchema.Policies;

public interface ISchemaPolicy
{
    IEnumerable<PolicyError> Validate(DatabaseSchema schema);
}
