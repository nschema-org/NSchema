using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Routines;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a routine that does not yet exist.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateRoutine(CreateRoutine action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the in-place body replacement of an existing routine.
    /// </summary>
    /// <remarks>
    /// Deliberately no drop-and-create default: dropping a routine is not equivalent to replacing it — an
    /// engine may block the drop for dependents, or shed grants and comments the in-place form preserves —
    /// so the dialect opts in with its engine's rules in view.
    /// </remarks>
    protected virtual Result<IReadOnlyList<SqlStatement>> ReplaceRoutine(ReplaceRoutine action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a routine.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropRoutine(DropRoutine action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the renaming of a routine.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameRoutine(RenameRoutine action) =>
        Unsupported(action);

    /// <summary>
    /// Renders dropping and recreating a routine whose signature changed.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RecreateRoutine(RecreateRoutine action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a routine's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetRoutineComment(SetRoutineComment action) =>
        Unsupported(action);
}
