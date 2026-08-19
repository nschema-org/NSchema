using NSchema.Model;
using NSchema.Model.Tables;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project.Projection;

/// <summary>
/// Resolves <c>INCLUDE name</c> members: merges each referenced table template's members into the table the
/// include was written in. Runs over the aggregate after schema-template application, so an instantiated table
/// can itself include a template. The template registry, the projected-member cache, and the accumulating
/// diagnostics are the resolver's own state rather than threaded arguments.
/// </summary>
/// <param name="templates">The project's template registry, shared with the expander.</param>
internal sealed class IncludeResolver(IReadOnlyDictionary<SqlIdentifier, TemplateStatement> templates)
{
    private readonly Dictionary<SqlIdentifier, Table> _memberCache = [];
    private readonly List<Diagnostic> _diagnostics = [];

    /// <summary>
    /// The schema name table-template members project against; foreign keys referencing it re-point at the
    /// including table's schema when the include merges.
    /// </summary>
    private static readonly SqlIdentifier _includePlaceholder = SchemaToken.TargetSchemaPlaceholder;

    /// <summary>
    /// The findings accumulated while resolving.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public Database Resolve(Database database, IReadOnlyList<TemplateInclude> includes)
    {
        if (includes.Count == 0)
        {
            return database;
        }

        var byTable = includes
            .GroupBy(i => i.Table)
            .ToDictionary(g => g.Key, g => g.ToList());

        // The database is the assembly's own aggregate, so includes merge into it in place.
        var consumed = new HashSet<ObjectAddress>();
        foreach (var definition in database.Schemas)
        {
            foreach (var table in definition.Tables)
            {
                var key = new ObjectAddress(definition.Name, table.Name);
                if (!byTable.TryGetValue(key, out var tableIncludes))
                {
                    continue;
                }
                consumed.Add(key);
                MergeIncludes(definition.Name, table, tableIncludes);
            }
        }

        // Parsed includes always name the table whose body they were written in, so a dangling one can only come
        // from a hand-built document; fail rather than drop it silently.
        foreach (var dangling in byTable.Keys.Where(key => !consumed.Contains(key)))
        {
            var include = byTable[dangling][0];
            _diagnostics.Add(TemplateDiagnostics.IncludeUnknownTable(include.TemplateName, include.Table));
        }

        return database;
    }

    /// <summary>
    /// Merges each included table template's members into <paramref name="table"/>: columns land at the position
    /// the include was written, foreign keys referencing the include placeholder re-point at the including table's
    /// schema, and everything else appends. A member the table already declares is rejected.
    /// </summary>
    private void MergeIncludes(SqlIdentifier schemaName, Table table, List<TemplateInclude> includes)
    {
        var offset = 0;
        foreach (var include in includes)
        {
            if (!templates.TryGetValue(include.TemplateName, out var template))
            {
                _diagnostics.Add(TemplateDiagnostics.IncludeUnknownTemplate(schemaName, table.Name, include.TemplateName));
                continue;
            }
            if (template is not TableTemplateStatement tableTemplate)
            {
                _diagnostics.Add(TemplateDiagnostics.IncludedSchemaTemplate(schemaName, table.Name, include.TemplateName));
                continue;
            }

            if (!_memberCache.TryGetValue(include.TemplateName, out var members))
            {
                // The members project once against the placeholder; placeholder references (an unqualified
                // REFERENCES in the body) re-point per including table below.
                (members, _) = DocumentProjector.ProjectTableMembers(_includePlaceholder, null, tableTemplate.Members);
                _memberCache[include.TemplateName] = members;
            }

            // The table owns the all-or-nothing merge, including its collision checks and copies.
            var merged = table.TryMergeMembers(
                members,
                include.ColumnPosition + offset,
                _includePlaceholder,
                schemaName);
            _diagnostics.AddRange(merged.Diagnostics);
            if (!merged.IsSuccess)
            {
                continue;
            }

            offset += merged.Value;
        }
    }
}
