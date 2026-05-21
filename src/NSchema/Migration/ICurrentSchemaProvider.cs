using NSchema.Schema;

namespace NSchema.Migration;

public interface ICurrentSchemaProvider
{
    Task<DatabaseSchema> GetSchema(string[] schemaNames, CancellationToken cancellationToken = default);
}
