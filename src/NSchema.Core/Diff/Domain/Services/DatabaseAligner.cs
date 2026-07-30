using NSchema.Model;
using NSchema.Project.Domain.Directives;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Rewrites the current schema into the declared name-space by applying the project's rename directives.
/// </summary>
internal static class DatabaseAligner
{
    public static Result<AlignedDatabase> Align(Database current, Database desired, ProjectDirectives directives)
    {
        var diagnostics = new List<Diagnostic>();

        // Applied renames, keyed by current names (what the tree carries before alignment); the logs are the
        // same renames keyed by declared names (what the tree carries after).
        var schemaRenames = new Dictionary<SqlIdentifier, SqlIdentifier>();
        var schemaLog = new Dictionary<DatabaseAddress, SqlIdentifier>();
        var objectRenames = new Dictionary<ObjectAddress, SqlIdentifier>();
        var objectLog = new Dictionary<ObjectAddress, SqlIdentifier>();
        var columnRenames = new Dictionary<MemberAddress, SqlIdentifier>();
        var columnLog = new Dictionary<MemberAddress, SqlIdentifier>();

        // Schema renames resolve first: object and column directives address current reality, but their
        // entities land in the declared schema, so the logs key through the applied schema renames.
        foreach (var rename in directives.SchemaRenames)
        {
            var from = rename.From.Name;
            var to = rename.To.Name;
            if (current.Schemas.All(s => s.Name != from))
            {
                if (current.Schemas.Any(s => s.Name == to))
                {
                    diagnostics.Add(DiffDiagnostics.AppliedRename("schema", DatabaseAddress.Schema(from), to));
                }
                continue;
            }

            if (desired.Schemas.Any(s => s.Name == from))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameSourceStillDeclared("schema", DatabaseAddress.Schema(to), from));
                continue;
            }
            if (current.Schemas.Any(s => s.Name == to))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameTargetTaken("schema", DatabaseAddress.Schema(to), from, to));
                continue;
            }

            schemaRenames[from] = to;
            schemaLog[rename.To] = from;
        }

        foreach (var rename in directives.ObjectRenames)
        {
            // An object rename always names the kind it renames; one without is not a rename we can align.
            if (rename.From.Kind is not { } kind)
            {
                continue;
            }

            var schema = current.Schemas.FirstOrDefault(s => s.Name == rename.From.Schema);
            if (schema is null || schema.Objects().All(o => o.Kind != kind || o.Name != rename.From.Name))
            {
                if (schema?.Objects().Any(o => o.Kind == kind && o.Name == rename.To) == true)
                {
                    diagnostics.Add(DiffDiagnostics.AppliedRename(kind.Display(), rename.From, rename.To));
                }
                continue;
            }

            var declaredSchema = schemaRenames.GetValueOrDefault(rename.From.Schema, rename.From.Schema);
            var address = new ObjectAddress(declaredSchema, rename.To);
            if (desired.Schemas.FirstOrDefault(s => s.Name == declaredSchema)?.Objects()
                    .Any(o => o.Kind == kind && o.Name == rename.From.Name) == true)
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameSourceStillDeclared(kind.Display(), address, rename.From.Name));
                continue;
            }
            if (schema.Objects().Any(o => o.Kind == kind && o.Name == rename.To))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameTargetTaken(kind.Display(), address, rename.From.Name, rename.To));
                continue;
            }

            objectRenames[rename.From] = rename.To;
            objectLog[new ObjectAddress(declaredSchema, rename.To, kind)] = rename.From.Name;
        }

        foreach (var rename in directives.MemberRenames)
        {
            var table = current.Schemas.FirstOrDefault(s => s.Name == rename.From.Schema)
                ?.Tables.FirstOrDefault(t => t.Name == rename.From.Object);
            if (table is null || table.Columns.All(c => c.Name != rename.From.Member))
            {
                if (table?.Columns.Any(c => c.Name == rename.To) == true)
                {
                    diagnostics.Add(DiffDiagnostics.AppliedRename("column", rename.From, rename.To));
                }
                continue;
            }

            var declaredSchema = schemaRenames.GetValueOrDefault(rename.From.Schema, rename.From.Schema);
            var declaredTable = objectRenames.GetValueOrDefault(new ObjectAddress(rename.From.Schema, rename.From.Object, SchemaObjectKind.Table), rename.From.Object);
            var address = new MemberAddress(declaredSchema, declaredTable, rename.To);
            if (desired.Schemas.FirstOrDefault(s => s.Name == declaredSchema)
                    ?.Tables.FirstOrDefault(t => t.Name == declaredTable)
                    ?.Columns.Any(c => c.Name == rename.From.Member) == true)
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameSourceStillDeclared("column", address, rename.From.Member));
                continue;
            }
            if (table.Columns.Any(c => c.Name == rename.To))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameTargetTaken("column", address, rename.From.Member, rename.To));
                continue;
            }

            columnRenames[rename.From] = rename.To;
            columnLog[address] = rename.From.Member;
        }

        if (schemaRenames.Count == 0 && objectRenames.Count == 0 && columnRenames.Count == 0)
        {
            return Result.From(AlignedDatabase.Unaligned(current), diagnostics);
        }

        // Apply the renames to a copy — the observation is shared with the rest of the run (widening, drift
        // display), so it is not ours to mutate. The rename maps key by current names, so renames apply
        // inside-out: columns while their table still carries its current name, then objects, then the schema.
        var aligned = current.Clone();
        foreach (var schema in aligned.Schemas)
        {
            var currentSchemaName = schema.Name;
            foreach (var table in schema.Tables)
            {
                foreach (var column in table.Columns)
                {
                    if (columnRenames.TryGetValue(new MemberAddress(currentSchemaName, table.Name, column.Name), out var columnName))
                    {
                        column.Name = columnName;
                    }
                }
            }
            foreach (var obj in schema.Objects())
            {
                if (objectRenames.TryGetValue(new ObjectAddress(currentSchemaName, obj.Name, obj.Kind), out var objectName))
                {
                    obj.Name = objectName;
                }
            }
            if (schemaRenames.TryGetValue(currentSchemaName, out var schemaName))
            {
                schema.Name = schemaName;
            }
        }

        return Result.From(new AlignedDatabase(aligned, new RenameLog(schemaLog, objectLog, columnLog)), diagnostics);
    }
}
