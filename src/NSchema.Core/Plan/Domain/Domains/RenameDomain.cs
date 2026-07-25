using NSchema.Model;

namespace NSchema.Plan.Domain.Domains;

/// <summary>
/// Represents renaming an existing domain.
/// </summary>
/// <param name="Domain">The address of the domain.</param>
/// <param name="NewName">The new name of the domain.</param>
public sealed record RenameDomain(ObjectAddress Domain, SqlIdentifier NewName) : MigrationAction;
