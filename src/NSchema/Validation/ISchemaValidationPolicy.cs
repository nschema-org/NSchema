using NSchema.Domain.Schema;

namespace NSchema.Validation;

public interface ISchemaValidationPolicy
{
    IEnumerable<SchemaValidationError> Validate(DatabaseSchema schema);
}
