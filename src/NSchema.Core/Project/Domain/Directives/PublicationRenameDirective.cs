using NSchema.Model;

namespace NSchema.Project.Domain.Directives;

/// <summary>
/// A publication rename directive.
/// </summary>
/// <param name="From">The publication's current address.</param>
/// <param name="To">The address the publication is renamed to.</param>
public sealed record PublicationRenameDirective(DatabaseAddress From, DatabaseAddress To);
