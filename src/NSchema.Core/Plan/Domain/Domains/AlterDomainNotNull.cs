using NSchema.Model;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents adding or dropping a domain's not-null requirement (<c>ALTER DOMAIN … SET/DROP NOT NULL</c>).
/// </summary>
/// <param name="Domain">The address of the domain.</param>
/// <param name="NotNull">Whether the domain should forbid <c>NULL</c> after the change.</param>
public sealed record AlterDomainNotNull(ObjectAddress Domain, bool NotNull) : MigrationAction;
