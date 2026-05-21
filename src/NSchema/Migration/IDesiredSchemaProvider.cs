using NSchema.Schema;

namespace NSchema.Migration;

public interface IDesiredSchemaProvider
{
    Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default);
}
