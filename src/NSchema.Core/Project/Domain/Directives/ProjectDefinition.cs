using NSchema.Model;

namespace NSchema.Project.Domain.Directives;

/// <summary>
/// The desired state declared by the project.
/// </summary>
/// <param name="Database">The declared database structure.</param>
/// <param name="Directives">The management directives declared across the project's files.</param>
public sealed record ProjectDefinition(Database Database, ProjectDirectives? Directives = null)
{
    /// <summary>
    /// The management directives declared across the project's files.
    /// </summary>
    public ProjectDirectives Directives { get; init; } = Directives ?? ProjectDirectives.Empty;

    /// <summary>
    /// The address of every schema the project manages.
    /// </summary>
    public DatabaseAddress[] AddressedSchemas => Database.Schemas
        .Select(s => s.Name)
        .Concat(Directives.SchemaRenames.Select(r => r.From.Name))
        .Distinct()
        .Select(name => DatabaseAddress.Schema(name))
        .ToArray();

    /// <summary>
    /// Restricts a project to a given scope.
    /// </summary>
    public ProjectDefinition ScopedTo(PlanningScope scope)
    {
        if (scope.IsUnscoped)
        {
            return this;
        }

        var database = Database.ScopedTo(scope);
        var directives = Directives.ScopedTo(scope);
        return new ProjectDefinition(database, directives);
    }
}
