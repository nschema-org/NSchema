using NSchema.Model;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents the removal of a publication.
/// </summary>
/// <param name="PublicationName">The name of the publication to remove.</param>
public sealed record DropPublication(SqlIdentifier PublicationName) : MigrationAction;
