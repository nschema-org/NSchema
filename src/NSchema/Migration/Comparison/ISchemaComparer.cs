using NSchema.Domain.Migration;
using NSchema.Domain.Schema;

namespace NSchema.Migration.Comparison;

public interface ISchemaComparer
{
    IReadOnlyList<SchemaInstruction> Compare(DatabaseModel current, DatabaseModel desired);
}
