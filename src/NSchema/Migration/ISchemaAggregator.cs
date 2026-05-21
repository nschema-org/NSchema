using NSchema.Schema;

namespace NSchema.Migration;

public interface ISchemaAggregator
{
    DatabaseSchema Aggregate(IEnumerable<DatabaseSchema> schemas);
}
