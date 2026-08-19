using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Indexes;

namespace NSchema.Plan.Plugins;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of an index.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> CreateIndex(CreateIndex action);

    /// <summary>
    /// Renders the removal of an index.
    /// </summary>
    protected abstract Result<IReadOnlyList<SqlStatement>> DropIndex(DropIndex action);

    /// <summary>
    /// Renders setting or clearing an index's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetIndexComment(SetIndexComment action) =>
        Unsupported(action);
}
