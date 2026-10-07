using NSchema.Model;
using NSchema.Model.Publications;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Project.Policies;

/// <summary>
/// Warns about published tables whose updates and deletions cannot be sent:
/// nothing identifies their rows, or a row filter reads columns their identity leaves out.
/// </summary>
internal sealed class ReplicationPolicy : IProjectPolicy
{
    /// <inheritdoc />
    public IEnumerable<Diagnostic> Validate(ProjectDefinition project)
    {
        var tables = project.Database.Objects<Table>().ToList();

        foreach (var publication in project.Database.Publications)
        {
            if ((publication.Operations & (PublishedOperations.Update | PublishedOperations.Delete)) == 0)
            {
                continue;
            }

            // Only a declared table can be checked; one the project does not declare is taken as it stands.
            foreach (var (address, table, entry) in Published(publication, tables))
            {
                var identity = table.IdentifyingColumns();
                if (identity.Count == 0)
                {
                    yield return ReplicationDiagnostics.UnidentifiedTable(publication.Name, address);
                    continue;
                }

                if (entry?.Filter is { } filter)
                {
                    var columns = table.Columns.Select(c => c.Name).ToHashSet();
                    var outside = Model.Services.ExpressionDependencyExtractor.Names(filter.Value)
                        .Where(name => columns.Contains(name) && !identity.Contains(name))
                        .ToList();
                    if (outside.Count > 0)
                    {
                        yield return ReplicationDiagnostics.FilterOutsideIdentity(publication.Name, address, outside);
                    }
                }
            }
        }
    }

    private static IEnumerable<(ObjectAddress Address, Table Table, PublishedTable? Entry)> Published(
        Publication publication, IReadOnlyList<(SqlIdentifier Schema, Table Object)> tables)
    {
        foreach (var (schema, table) in tables)
        {
            var entry = publication.Tables.FirstOrDefault(t => t.Table.Schema == schema && t.Table.Name == table.Name);
            if (entry is not null || publication.AllTables || publication.Schemas.Contains(schema))
            {
                yield return (new ObjectAddress(schema, table.Name), table, entry);
            }
        }
    }
}
