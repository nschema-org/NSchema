using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Extensions;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a database extension.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateExtension(CreateExtension action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a database extension.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropExtension(DropExtension action) =>
        Unsupported(action);

    /// <summary>
    /// Renders updating a database extension to a different version.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AlterExtension(AlterExtension action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a database extension's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetExtensionComment(SetExtensionComment action) =>
        Unsupported(action);
}
