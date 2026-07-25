using NSchema.Diff.Domain.Triggers;
using NSchema.Model;
using NSchema.Model.Triggers;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    // Triggers are table members like indexes: matched by name, a structural change is a remove + add (Trigger's
    // Equals excludes the comment), and a comment-only change is an in-place modify.
    private List<TriggerDiff> CompareTriggers(ObjectAddress owner, IReadOnlyList<Trigger> current, IReadOnlyList<Trigger> desired) =>
        CompareTableMembers(owner, "Trigger", current, desired,
            TriggerDiff.Added, TriggerDiff.Removed, TriggerDiff.CommentChanged);
}
