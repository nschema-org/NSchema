using NSchema.Domain.Schema;

namespace NSchema.Current;

public interface ICurrentSchemaProvider
{
    Task<DatabaseSchema> GetSchema(string[] schemaNames, CancellationToken cancellationToken = default);
}
