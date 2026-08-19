using NSchema.Model.Scripts;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Templates;
using NSchema.Project.Projection;

namespace NSchema.Project;

/// <summary>
/// Assembles parsed documents into a cohesive <see cref="ProjectDefinition"/>.
/// </summary>
internal static class ProjectAssembler
{
    public static Result<ProjectDefinition> Assemble(IReadOnlyList<NsqlDocument> documents)
    {
        var diagnostics = new DiagnosticCollector();
        var accumulator = new DatabaseAccumulator();
        var schemaTemplates = new List<SchemaTemplateStatement>();
        var tableTemplates = new List<TableTemplateStatement>();
        var applications = new List<TemplateApplication>();
        var directives = new DirectiveCollector();

        foreach (var document in documents)
        {
            // Set the current file context so it can be attached to any reported diagnostics.
            accumulator.CurrentFile = document.FilePath;
            foreach (var statement in document.Statements)
            {
                switch (statement)
                {
                    case SchemaTemplateStatement template:
                        // Validating the body (internal duplicates, stray-qualified declarations) at read
                        // time, whether the template is ever applied.
                        var validateResult = DocumentProjector.ValidateTemplateBody(template);
                        diagnostics.AddRange(validateResult.Select(d => d with { File = document.FilePath }));
                        schemaTemplates.Add(template);
                        break;
                    case TableTemplateStatement template:
                        tableTemplates.Add(template);
                        break;
                    case ApplyTemplateStatement application:
                        applications.Add(new TemplateApplication(application, document.FilePath));
                        break;
                    case Nsql.Syntax.Settings.SettingsStatement:
                        // Configuration is not project content; the configuration read seam interprets it.
                        break;
                    case var directive when directives.TryAdd(directive):
                        break;
                    default:
                        DocumentProjector.ProjectStatement(statement, accumulator, context: null);
                        break;
                }
            }
        }
        accumulator.CurrentFile = null;

        // Expand the template applications.
        var templatesResult = TemplateExpander.BuildRegistry(schemaTemplates, tableTemplates);
        var templates = diagnostics.Require(templatesResult);

        var expandResult = TemplateExpander.Expand(accumulator, templates, applications);
        var projectDirectives = diagnostics.Require(expandResult);
        directives.Absorb(projectDirectives);

        // Build once, now everything is together.
        var database = accumulator.Build();
        diagnostics.AddRange(accumulator.Diagnostics);

        // Includes resolve over the aggregate, after expansion, so a templated table can include a template.
        var resolver = new IncludeResolver(templates);
        database = resolver.Resolve(database, accumulator.Includes);
        diagnostics.AddRange(resolver.Diagnostics);

        var built = directives.Build();
        var project = new ProjectDefinition(database, built);

        // Collisions are project errors, so validate before scoping drops any instance — all of them at once.
        diagnostics.AddRange(ValidateChangeTargets(built.ChangeScripts));
        diagnostics.AddRange(ValidateScriptNames(built));
        diagnostics.AddRange(DirectiveValidator.Validate(project));

        return diagnostics.ToResult(project);
    }

    /// <summary>
    /// Enforces script-address uniqueness per scope.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateScriptNames(ProjectDirectives directives)
    {
        var addresses = new HashSet<ScriptReference>();
        foreach (var address in directives.ChangeScripts.Select(s => s.Reference).Concat(directives.DeploymentScripts.Select(s => s.Reference)))
        {
            if (!addresses.Add(address))
            {
                yield return ProjectDiagnostics.DuplicateScriptName(address);
            }
        }
    }

    /// <summary>
    /// Rejects a second change-event script for the same trigger and path — two blocks preparing the same
    /// change is a conflict, whatever their names.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateChangeTargets(IReadOnlyList<ChangeScript> scripts)
    {
        var targets = new HashSet<ChangeTarget>();
        foreach (var change in scripts)
        {
            if (!targets.Add(change.Target))
            {
                yield return ProjectDiagnostics.DuplicateChangeTarget(change);
            }
        }
    }
}
