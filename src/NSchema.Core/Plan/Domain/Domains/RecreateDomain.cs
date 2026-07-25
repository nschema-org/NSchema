using NSchema.Model;
using NSchema.Model.Domains;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents dropping and recreating a domain whose base type changed.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainType">The desired domain to recreate.</param>
public sealed record RecreateDomain(SqlIdentifier SchemaName, DomainType DomainType) : MigrationAction;
