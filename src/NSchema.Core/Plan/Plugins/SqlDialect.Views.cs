using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Views;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a view that does not yet exist.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> CreateView(CreateView action);

    /// <summary>
    /// Renders the in-place body replacement of an existing view.
    /// </summary>
    /// <remarks>
    /// Deliberately no drop-and-create default: dropping a view is not equivalent to replacing it — an
    /// engine may block the drop for dependents, or shed grants and comments the in-place form preserves —
    /// so the dialect opts in with its engine's rules in view.
    /// </remarks>
    protected virtual Result<IReadOnlyList<SqlStatement>> ReplaceView(ReplaceView action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a view. Materialized views are not universal, so their removal is unsupported
    /// until a dialect opts in.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropView(DropView action) =>
        action.IsMaterialized
            ? Unsupported(action)
            : Statement($"DROP VIEW {Qualify(action.View)}");

    /// <summary>
    /// Renders the renaming of a view. Materialized views are not universal, so their renaming is unsupported
    /// until a dialect opts in.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameView(RenameView action) =>
        action.IsMaterialized
            ? Unsupported(action)
            : Statement($"ALTER VIEW {Qualify(action.View)} RENAME TO {Quote(action.NewName)}");

    /// <summary>
    /// Renders setting or clearing a view's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetViewComment(SetViewComment action) =>
        Unsupported(action);
}
