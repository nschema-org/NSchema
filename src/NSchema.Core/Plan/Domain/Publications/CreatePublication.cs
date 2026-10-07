using NSchema.Model.Publications;

namespace NSchema.Plan.Domain.Publications;

/// <summary>
/// Represents the creation of a publication.
/// </summary>
/// <param name="Publication">The definition of the publication to create.</param>
public sealed record CreatePublication(Publication Publication) : MigrationAction;
