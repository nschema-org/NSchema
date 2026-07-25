using NSchema.Model;
using NSchema.Model.Domains;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents the creation of a domain, with its default, not-null requirement and check constraints inline.
/// </summary>
/// <param name="SchemaName">The name of the schema the domain belongs to.</param>
/// <param name="DomainType">The definition of the domain to create.</param>
public sealed record CreateDomain(SqlIdentifier SchemaName, DomainType DomainType) : MigrationAction;
