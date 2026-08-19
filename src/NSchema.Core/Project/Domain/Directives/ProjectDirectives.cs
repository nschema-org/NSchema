using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Project.Domain.Directives;

/// <summary>
/// The management directives a project declares.
/// </summary>
public sealed record ProjectDirectives(
    IReadOnlyList<SchemaRenameDirective>? SchemaRenames = null,
    IReadOnlyList<ObjectRenameDirective>? ObjectRenames = null,
    IReadOnlyList<MemberRenameDirective>? MemberRenames = null,
    IReadOnlyList<ChangeScript>? ChangeScripts = null,
    IReadOnlyList<DeploymentScript>? DeploymentScripts = null
)
{
    /// <summary>
    /// A project declaring no directives.
    /// </summary>
    public static ProjectDirectives Empty { get; } = new();

    /// <summary>
    /// The declared schema renames.
    /// </summary>
    public IReadOnlyList<SchemaRenameDirective> SchemaRenames { get; init; } = SchemaRenames ?? [];

    /// <summary>
    /// The schema-level object renames, of every kind.
    /// </summary>
    public IReadOnlyList<ObjectRenameDirective> ObjectRenames { get; init; } = ObjectRenames ?? [];

    /// <summary>
    /// The declared member renames.
    /// </summary>
    public IReadOnlyList<MemberRenameDirective> MemberRenames { get; init; } = MemberRenames ?? [];

    /// <summary>
    /// The change-event scripts targeting table members.
    /// </summary>
    public IReadOnlyList<ChangeScript> ChangeScripts { get; init; } = ChangeScripts ?? [];

    /// <summary>
    /// The deployment scripts.
    /// </summary>
    public IReadOnlyList<DeploymentScript> DeploymentScripts { get; init; } = DeploymentScripts ?? [];

    /// <summary>
    /// Restricts the directives to those addressing in-scope schemas.
    /// </summary>
    public ProjectDirectives ScopedTo(PlanningScope scope)
    {
        // A current schema name maps to its declared name through the schema renames.
        var declaredNames = SchemaRenames.ToDictionary(r => r.From.Name, r => r.To.Name);

        return new ProjectDirectives(
            [.. SchemaRenames.Where(r => scope.Contains(r.From) || scope.Contains(r.To))],
            [.. ObjectRenames.Where(r => InScope(r.From) || InScope(r.ToAddress))],
            [.. MemberRenames.Where(r => InScope(r.From.Owner))],
            [.. ChangeScripts.Where(ChangeInScope)],
            [.. DeploymentScripts.Where(DeploymentInScope)]);

        // A deployment script is a schema-level facet, below the schema and no object, so only a
        // whole-schema scope covers it; a change script rides its table.
        bool DeploymentInScope(DeploymentScript script) =>
            script.ScopeSchema is not { } schema || scope.Contains(schema);

        bool ChangeInScope(ChangeScript script) =>
            script.Target.TableAddress is not { } table || InScope(table);

        // A directive addresses current reality; the scope may name either side of a schema rename.
        bool InScope(ObjectAddress current) =>
            scope.Contains(current)
            || (declaredNames.TryGetValue(current.Schema, out var declared) && scope.Contains(current with { Schema = declared }));
    }
}
