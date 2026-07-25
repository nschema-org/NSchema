using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Routines;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation (or in-place body replacement) of a routine.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateRoutine(CreateRoutine action) =>
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
