using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Views;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation (or in-place replacement) of a view.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> CreateView(CreateView action);

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
