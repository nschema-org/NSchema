using NSchema.Domain.Schema;

namespace NSchema.Desired;

public interface IDesiredSchemaProvider
{
    Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default);
}
