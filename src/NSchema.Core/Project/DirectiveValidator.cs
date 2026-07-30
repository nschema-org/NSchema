using NSchema.Model;
using NSchema.Model.Services;
using NSchema.Project.Domain.Directives;

namespace NSchema.Project;

/// <summary>
/// Validates the project's directives against its declarations.
/// </summary>
internal static class DirectiveValidator
{
    public static IEnumerable<Diagnostic> Validate(ProjectDefinition project)
    {
        var directives = project.Directives;
        var index = new DatabaseLookup(project.Database);

        // A current schema name maps to its declared name through the schema renames. First wins on a
        // duplicate source — the duplicate itself is reported by the shape checks, and validation continues
        // best-effort.
        var declaredNames = new Dictionary<SqlIdentifier, SqlIdentifier>();
        foreach (var rename in directives.SchemaRenames)
        {
            declaredNames.TryAdd(rename.From.Name, rename.To.Name);
        }

        return ValidateSchemaRenames(directives, index)
            .Concat(ValidateObjectRenames(directives, index, declaredNames))
            .Concat(ValidateColumnRenames(directives, index, declaredNames));
    }

    private static IEnumerable<Diagnostic> ValidateSchemaRenames(ProjectDirectives directives, DatabaseLookup index)
    {
        foreach (var d in ValidateRenameShape("schema",
            directives.SchemaRenames.Select(r => (Container: 0, From: r.From.Name, To: r.To.Name)).ToList(),
            (_, name) => DatabaseAddress.Schema(name)))
        {
            yield return d;
        }

        foreach (var rename in directives.SchemaRenames)
        {
            if (index.FindSchema(rename.To.Name) is null)
            {
                yield return ProjectDiagnostics.RenameTargetNotDeclared("schema", rename.From, rename.To.Name);
            }
            if (index.FindSchema(rename.From.Name) is not null)
            {
                yield return ProjectDiagnostics.RenameSourceStillDeclared("schema", rename.From, rename.To.Name);
            }
        }
    }

    /// <summary>
    /// Validates the object-level renames, one uniform rule set per kind.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateObjectRenames(
        ProjectDirectives directives, DatabaseLookup index, Dictionary<SqlIdentifier, SqlIdentifier> declaredNames)
    {
        foreach (var kind in Enum.GetValues<SchemaObjectKind>())
        {
            var kindName = kind.Display();
            var renames = directives.ObjectRenames.Where(r => r.From.Kind == kind).ToList();

            foreach (var d in ValidateRenameShape(kindName,
                renames.Select(r => (Container: r.From.Schema, From: r.From.Name, r.To)).ToList(),
                (schema, name) => new ObjectAddress(schema, name)))
            {
                yield return d;
            }

            foreach (var rename in renames)
            {
                var declaredSchema = declaredNames.GetValueOrDefault(rename.From.Schema, rename.From.Schema);
                if (index.FindSchema(declaredSchema) is null)
                {
                    yield return ProjectDiagnostics.DirectiveSchemaNotDeclared($"RENAME of {kindName:text} '{rename.From}'", rename.From.Schema);
                }
                else if (!index.Has(kind, new ObjectAddress(declaredSchema, rename.To)))
                {
                    yield return ProjectDiagnostics.RenameTargetNotDeclared(kindName, rename.From, rename.To);
                }
                if (index.Has(kind, new ObjectAddress(declaredSchema, rename.From.Name)))
                {
                    yield return ProjectDiagnostics.RenameSourceStillDeclared(kindName, rename.From, rename.To);
                }
            }
        }
    }

    private static IEnumerable<Diagnostic> ValidateColumnRenames(
        ProjectDirectives directives, DatabaseLookup index, Dictionary<SqlIdentifier, SqlIdentifier> declaredNames)
    {
        foreach (var d in ValidateRenameShape("column",
            directives.MemberRenames.Select(r => (Container: (r.From.Schema, r.From.Object), From: r.From.Member, r.To)).ToList(),
            (table, name) => new MemberAddress(table.Schema, table.Object, name)))
        {
            yield return d;
        }

        var tableRenames = new Dictionary<ObjectAddress, SqlIdentifier>();
        foreach (var rename in directives.ObjectRenames.Where(r => r.From.Kind == SchemaObjectKind.Table))
        {
            tableRenames.TryAdd(rename.From with { Kind = null }, rename.To);
        }
        foreach (var rename in directives.MemberRenames)
        {
            // The path names current reality: resolve the schema and the table through their own renames to
            // find the declared table the column must be declared on.
            var declaredSchema = declaredNames.GetValueOrDefault(rename.From.Schema, rename.From.Schema);
            var declaredTable = tableRenames.GetValueOrDefault(new ObjectAddress(rename.From.Schema, rename.From.Object), rename.From.Object);
            if (index.FindTable(new ObjectAddress(declaredSchema, declaredTable)) is not { } table)
            {
                yield return ProjectDiagnostics.DirectiveTableNotDeclared(rename.From);
                continue;
            }
            if (table.Columns.All(c => c.Name != rename.To))
            {
                yield return ProjectDiagnostics.RenameTargetNotDeclared("column", rename.From, rename.To);
            }
            if (table.Columns.Any(c => c.Name == rename.From.Member))
            {
                yield return ProjectDiagnostics.RenameSourceStillDeclared("column", rename.From, rename.To);
            }
        }
    }

    /// <summary>
    /// The container-independent rename rules: no self-renames, no two renames sharing a source or a target,
    /// and no chains (one rename's target being another's source is unordered and therefore ambiguous).
    /// Containers partition the checks — the same bare names in different containers never interact.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateRenameShape<TContainer>(
        string kind,
        IReadOnlyList<(TContainer Container, SqlIdentifier From, SqlIdentifier To)> renames,
        Func<TContainer, SqlIdentifier, Address> display
    ) where TContainer : notnull
    {
        for (var i = 0; i < renames.Count; i++)
        {
            var (container, from, to) = renames[i];
            if (from == to)
            {
                yield return ProjectDiagnostics.SelfRename(kind, display(container, from));
            }

            for (var j = i + 1; j < renames.Count; j++)
            {
                if (!EqualityComparer<TContainer>.Default.Equals(container, renames[j].Container))
                {
                    continue;
                }

                var (_, otherFrom, otherTo) = renames[j];
                if (from == otherFrom)
                {
                    yield return ProjectDiagnostics.DuplicateRenameSource(kind, display(container, from));
                }
                else if (to == otherTo)
                {
                    yield return ProjectDiagnostics.DuplicateRenameTarget(kind, display(container, to));
                }
                else if (to == otherFrom || from == otherTo)
                {
                    yield return ProjectDiagnostics.RenameChain(kind, display(container, to == otherFrom ? to : from));
                }
            }
        }
    }
}
