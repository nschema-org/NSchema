using NSchema.Model;

namespace NSchema.Project.Domain.Directives;

/// <summary>
/// A schema rename directive.
/// </summary>
/// <param name="From">The schema's current address.</param>
/// <param name="To">The address the schema is renamed to.</param>
public sealed record SchemaRenameDirective(DatabaseAddress From, DatabaseAddress To);
