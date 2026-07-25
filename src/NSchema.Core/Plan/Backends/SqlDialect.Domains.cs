using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Domains;

namespace NSchema.Plan.Backends;

public abstract partial class SqlDialect
{
    /// <summary>
    /// Renders the creation of a domain.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> CreateDomain(CreateDomain action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the removal of a domain.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropDomain(DropDomain action) =>
        Unsupported(action);

    /// <summary>
    /// Renders the renaming of a domain.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RenameDomain(RenameDomain action) =>
        Unsupported(action);

    /// <summary>
    /// Renders dropping and recreating a domain whose base type changed.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> RecreateDomain(RecreateDomain action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or dropping a domain's default expression.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AlterDomainDefault(AlterDomainDefault action) =>
        Unsupported(action);

    /// <summary>
    /// Renders adding or dropping a domain's not-null requirement.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AlterDomainNotNull(AlterDomainNotNull action) =>
        Unsupported(action);

    /// <summary>
    /// Renders adding a check constraint to a domain.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> AddDomainCheck(AddDomainCheck action) =>
        Unsupported(action);

    /// <summary>
    /// Renders dropping a check constraint from a domain.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> DropDomainCheck(DropDomainCheck action) =>
        Unsupported(action);

    /// <summary>
    /// Renders setting or clearing a domain's comment.
    /// </summary>
    protected virtual Result<IReadOnlyList<SqlStatement>> SetDomainComment(SetDomainComment action) =>
        Unsupported(action);
}
