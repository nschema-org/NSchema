using NSchema.Model;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents the removal of an existing domain from the database schema.
/// </summary>
/// <param name="Domain">The address of the domain.</param>
public sealed record DropDomain(ObjectAddress Domain) : MigrationAction;
