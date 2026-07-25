using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Indexes;
using NSchema.Model;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Tables;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    // The primary key is single-valued (not a list member), so it keeps its own comparison rather than the
    // shared CompareTableMembers skeleton.
    private List<PrimaryKeyDiff> ComparePrimaryKey(ObjectAddress owner, PrimaryKey? current, PrimaryKey? desired)
    {
        var result = new List<PrimaryKeyDiff>();

        // Structurally identical (Equals excludes the comment): at most a comment-only change, applied in place.
        if (current is not null && desired is not null && current.Equals(desired))
        {
            if (current.Comment != desired.Comment)
            {
                LogPrimaryKeyCommentChanged(desired.Name, owner);
                result.Add(PrimaryKeyDiff.CommentChanged(desired.Name, new ValueChange<string>(current.Comment, desired.Comment)));
            }
            else
            {
                LogPrimaryKeyUnchanged(owner);
            }
            return result;
        }

        if (current is null && desired is null)
        {
            LogPrimaryKeyUnchanged(owner);
            return result;
        }

        if (current is not null)
        {
            LogPrimaryKeyDropping(current.Name, owner);
            result.Add(PrimaryKeyDiff.Removed(current.Name));
        }
        if (desired is not null)
        {
            LogPrimaryKeyAdding(desired.Name, owner);
            result.Add(PrimaryKeyDiff.Added(desired));
            if (desired.Comment is not null)
            {
                result.Add(PrimaryKeyDiff.CommentChanged(desired.Name, new ValueChange<string>(null, desired.Comment)));
            }
        }
        return result;
    }

    private List<ForeignKeyDiff> CompareForeignKeys(ObjectAddress owner, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired) =>
        CompareTableMembers(owner, "Foreign key", current, desired,
            ForeignKeyDiff.Added, ForeignKeyDiff.Removed, ForeignKeyDiff.CommentChanged);

    private List<UniqueConstraintDiff> CompareUniqueConstraints(ObjectAddress owner, IReadOnlyList<UniqueConstraint> current, IReadOnlyList<UniqueConstraint> desired) =>
        CompareTableMembers(owner, "Unique constraint", current, desired,
            UniqueConstraintDiff.Added, UniqueConstraintDiff.Removed, UniqueConstraintDiff.CommentChanged);

    private List<CheckConstraintDiff> CompareChecks(ObjectAddress owner, IReadOnlyList<CheckConstraint> current, IReadOnlyList<CheckConstraint> desired) =>
        CompareTableMembers(owner, "Check constraint", current, desired,
            CheckConstraintDiff.Added, CheckConstraintDiff.Removed, CheckConstraintDiff.CommentChanged);

    private List<ExclusionConstraintDiff> CompareExclusionConstraints(ObjectAddress owner, IReadOnlyList<ExclusionConstraint> current, IReadOnlyList<ExclusionConstraint> desired) =>
        CompareTableMembers(owner, "Exclusion constraint", current, desired,
            ExclusionConstraintDiff.Added, ExclusionConstraintDiff.Removed, ExclusionConstraintDiff.CommentChanged);

    private List<IndexDiff> CompareIndexes(ObjectAddress owner, IReadOnlyList<TableIndex> current, IReadOnlyList<TableIndex> desired) =>
        CompareTableMembers(owner, "Index", current, desired,
            IndexDiff.Added, IndexDiff.Removed, IndexDiff.CommentChanged);
}
