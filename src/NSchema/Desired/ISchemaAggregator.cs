using NSchema.Domain.Schema;

namespace NSchema.Desired;

public interface ISchemaAggregator
{
    DatabaseSchema Aggregate(IEnumerable<DatabaseSchema> schemas);
}
