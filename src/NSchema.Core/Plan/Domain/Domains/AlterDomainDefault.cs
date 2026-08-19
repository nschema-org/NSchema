using NSchema.Model;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents setting, changing, or dropping a domain's default expression (<c>ALTER DOMAIN … SET/DROP DEFAULT</c>).
/// </summary>
/// <param name="Domain">The address of the domain.</param>
/// <param name="OldDefault">The previous default expression, if any.</param>
/// <param name="NewDefault">The new default expression, or <see langword="null"/> to drop it.</param>
public sealed record AlterDomainDefault(ObjectAddress Domain, SqlDefaultExpression? OldDefault, SqlDefaultExpression? NewDefault) : MigrationAction;
