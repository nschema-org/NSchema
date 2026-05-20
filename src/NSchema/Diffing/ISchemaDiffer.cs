using NSchema.Domain.Execution;
using NSchema.Domain.Schema;

namespace NSchema.Diffing;

public interface ISchemaDiffer
{
    IReadOnlyList<SchemaInstruction> Diff(DatabaseModel current, DatabaseModel desired);
}
