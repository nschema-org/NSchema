using NSchema.Domain;

namespace NSchema.Extractors;

public interface ISchemaExtractor
{
    Task<DatabaseModel> Extract(CancellationToken cancellationToken = default);
}
