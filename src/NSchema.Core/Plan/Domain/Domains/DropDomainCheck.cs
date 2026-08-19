using NSchema.Model;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents dropping a check constraint from a domain (<c>ALTER DOMAIN … DROP CONSTRAINT …</c>).
/// </summary>
/// <param name="Check">The address of the check constraint.</param>
public sealed record DropDomainCheck(MemberAddress Check) : MigrationAction;
