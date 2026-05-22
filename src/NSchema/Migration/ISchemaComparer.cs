using NSchema.Schema;

namespace NSchema.Migration;

public interface ISchemaComparer
{
    MigrationPlan Compare(DatabaseSchema current, DatabaseSchema desired);
}
