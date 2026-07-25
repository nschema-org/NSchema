using NSchema.Diff.Domain.Sequences;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private static List<SequenceDiff> CompareSequences(SqlIdentifier schemaName, IReadOnlyList<Sequence> current, Schema desired, RenameLog renames) =>
        CompareObjects(current, desired.Sequences,
            name => renames.RenamedFrom(new ObjectAddress(schemaName, name, ObjectKind.Sequence)),
            sequence => SequenceDiff.Removed(schemaName, sequence.Name),
            sequence => BuildNewSequence(schemaName, sequence),
            (currentSequence, desiredSequence, renamedFrom) => BuildModifiedSequence(schemaName, currentSequence, desiredSequence, renamedFrom));

    private static SequenceDiff BuildNewSequence(SqlIdentifier schema, Sequence sequence) =>
        SequenceDiff.Added(schema, sequence);

    private static SequenceDiff? BuildModifiedSequence(SqlIdentifier schema, Sequence current, Sequence desired, SqlIdentifier? renamedFrom)
    {
        var comment = ValueChange.Between(current.Comment, desired.Comment);
        var options = ValueChange.Between(current.Options, desired.Options);

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
