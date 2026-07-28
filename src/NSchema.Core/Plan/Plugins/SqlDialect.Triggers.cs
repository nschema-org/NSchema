using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Triggers;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a trigger.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> CreateTrigger(CreateTrigger action);

    /// <summary>
    /// Renders the removal of a trigger.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> DropTrigger(DropTrigger action);

    /// <summary>
    /// Renders setting or clearing a trigger's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetTriggerComment(SetTriggerComment action) =>
        Unsupported(action);
}
