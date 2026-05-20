using NSchema.Domain.Schema;

namespace NSchema.Extractors;

public interface ISchemaExtractor
{
    Task<DatabaseModel> Extract(CancellationToken cancellationToken = default);
}
