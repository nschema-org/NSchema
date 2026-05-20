using NSchema.Domain;
using NSchema.Instructions;

namespace NSchema.Diffing;

public interface ISchemaDiffer
{
    IReadOnlyList<SchemaInstruction> Diff(DatabaseModel current, DatabaseModel desired);
}
