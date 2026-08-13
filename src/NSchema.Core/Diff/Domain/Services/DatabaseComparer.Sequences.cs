using NSchema.Diff.Domain.Sequences;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private List<SequenceDiff> CompareSequences(SqlIdentifier schemaName, IReadOnlyList<Sequence> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.Sequences,
            name => renames.RenamedFrom(ObjectAddress.Sequence(schemaName, name)),
            sequence => SequenceDiff.Removed(schemaName, sequence.Name),
            sequence => BuildNewSequence(schemaName, sequence),
            (currentSequence, desiredSequence, renamedFrom) => BuildModifiedSequence(schemaName, currentSequence, desiredSequence, renamedFrom));

    private static SequenceDiff BuildNewSequence(SqlIdentifier schema, Sequence sequence) =>
        // A create renders what was declared: an explicit default is legal SQL and saying it back is not drift.
        SequenceDiff.Added(schema, sequence);

    private SequenceDiff? BuildModifiedSequence(SqlIdentifier schema, Sequence current, Sequence desired, SqlIdentifier? renamedFrom)
    {
        var comment = ValueChange.Between(current.Comment, desired.Comment);
        // Both sides are folded onto the engine's defaults before comparison, so an option declared with the value
        // the engine would have chosen anyway is not drift. The change carries the folded options rather than the
        // declared ones, so a sequence altered for some other reason does not also restate a default it never
        // changed — on some engines restating START restarts the live counter.
        var options = ValueChange.Between(equivalence.WithDefaults(current.Options), equivalence.WithDefaults(desired.Options));

        if (renamedFrom is null && options is null && comment is null)
        {
            return null;
        }

        return SequenceDiff.Modified(schema, desired.Name) with
        {
            RenamedFrom = renamedFrom,
            Options = options,
            Comment = comment,
        };
    }
}
