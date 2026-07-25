using NSchema.Model;
using NSchema.Model.Constraints;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents adding a check constraint to a domain (<c>ALTER DOMAIN … ADD CONSTRAINT … CHECK (…)</c>).
/// </summary>
/// <param name="Domain">The address of the domain.</param>
/// <param name="Check">The check constraint to add.</param>
public sealed record AddDomainCheck(ObjectAddress Domain, CheckConstraint Check) : MigrationAction;
