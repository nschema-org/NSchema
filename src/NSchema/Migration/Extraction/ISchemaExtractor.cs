using NSchema.Domain.Schema;

namespace NSchema.Migration.Extraction;

public interface ISchemaExtractor
{
    Task<DatabaseModel> Extract(CancellationToken cancellationToken = default);
}
