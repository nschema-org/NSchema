using NSchema.Domain.Schema;

namespace NSchema.Validation;

public interface ISchemaPolicy
{
    IEnumerable<PolicyError> Validate(DatabaseSchema schema);
}
