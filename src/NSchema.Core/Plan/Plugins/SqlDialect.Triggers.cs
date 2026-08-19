using NSchema.Model;
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
    /// Renders the replacement of an existing trigger. The base decomposes to the dialect's own drop and
    /// create; a dialect with an in-place form overrides.
    /// </summary>
    /// <remarks>
    /// A trigger is a leaf — no dependents, no grants, no identity anything else holds — so drop-and-create
    /// is observably equivalent to replacing on every engine, and the base can default to it. The routine
    /// and view replacements have no such default: dropping either is not equivalent to replacing it.
    /// </remarks>
    protected virtual Result<IReadOnlyList<SqlStatement>> ReplaceTrigger(ReplaceTrigger action)
    {
        var drop = DropTrigger(new DropTrigger(new MemberAddress(action.Table.Schema, action.Table.Name, action.Trigger.Name)));
        if (drop.Value is not { } dropStatements)
        {
            return drop;
        }

        var create = CreateTrigger(new CreateTrigger(action.Table, action.Trigger));
        if (create.Value is not { } createStatements)
        {
            return create;
        }

        return Result.From<IReadOnlyList<SqlStatement>>(
            [.. dropStatements, .. createStatements],
            [.. drop.Diagnostics, .. create.Diagnostics]);
    }

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
