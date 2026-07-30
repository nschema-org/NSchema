using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Templates;

namespace NSchema.Project.Projection;

/// <summary>
/// Expands template applications during project assembly.
/// </summary>
internal static class TemplateExpander
{
    /// <summary>
    /// One schema template instantiated for one applied schema: the instance objects, the includes its body
    /// carried, and the directives it bound to the applied schema.
    /// </summary>
    private sealed record TemplateInstance(Schema Schema, IReadOnlyList<TemplateInclude> Includes, ProjectDirectives Directives);

    /// <summary>
    /// Builds the template registry — one name space across both kinds, as ever: a schema template and a
    /// table template cannot share a name.
    /// </summary>
    public static Result<IReadOnlyDictionary<SqlIdentifier, TemplateStatement>> BuildRegistry(
        IReadOnlyList<SchemaTemplateStatement> schemaTemplates,
        IReadOnlyList<TableTemplateStatement> tableTemplates
    )
    {
        var diagnostics = new DiagnosticCollector();
        var byName = new Dictionary<SqlIdentifier, TemplateStatement>();
        foreach (var template in schemaTemplates.Cast<TemplateStatement>().Concat(tableTemplates))
        {
            SqlIdentifier name = template.Name.Value;
            if (!byName.TryAdd(name, template))
            {
                diagnostics.AddRange(TemplateDiagnostics.DuplicateTemplate(name));
            }
        }

        return diagnostics.ToResult<IReadOnlyDictionary<SqlIdentifier, TemplateStatement>>(byName);
    }

    /// <summary>
    /// Expands every application of a template.
    /// </summary>
    public static Result<ProjectDirectives> Expand(
        DatabaseAccumulator accumulator,
        IReadOnlyDictionary<SqlIdentifier, TemplateStatement> templates,
        IReadOnlyList<TemplateApplication> applications
    )
    {
        var diagnostics = new DiagnosticCollector();

        // Each successful instance's directives (its scripts, and object renames) accumulate here, scoped
        // to their applied schema, and ride back for the assembler to merge with the top-level ones.
        var instanceDirectives = new DirectiveCollector();

        foreach (var (application, file) in applications)
        {
            SqlIdentifier templateName = application.TemplateName.Value;
            if (!templates.TryGetValue(templateName, out var template))
            {
                diagnostics.AddRange(TemplateDiagnostics.UnknownTemplate(templateName));
                continue;
            }
            if (template is not SchemaTemplateStatement schemaTemplate)
            {
                diagnostics.AddRange(TemplateDiagnostics.AppliedTableTemplate(templateName));
                continue;
            }

            foreach (var schemaNameNode in application.Schemas)
            {
                SqlIdentifier schemaName = schemaNameNode.Value;
                if (!accumulator.HasSchema(schemaName))
                {
                    diagnostics.AddRange(TemplateDiagnostics.UnknownTargetSchema(templateName, schemaName));
                    continue;
                }

                var instance = Instantiate(schemaTemplate, schemaName);
                Absorb(accumulator, instance.Schema, templateName, schemaName, schemaNameNode.Position, file);

                // The instance's own includes resolve with everything else, so an instantiated table can
                // itself include a template.
                foreach (var include in instance.Includes)
                {
                    accumulator.AddInclude(include);
                }
                instanceDirectives.Absorb(instance.Directives);
            }
        }

        return diagnostics.ToResult(instanceDirectives.Build());
    }

    /// <summary>
    /// Absorbs one instance's objects into the accumulator, exactly as if they had been written in the
    /// target schema by hand: a name the schema already declares is rejected per object by the ordinary
    /// per-add check, and the rest of the instance still lands.
    /// </summary>
    private static void Absorb(
        DatabaseAccumulator accumulator,
        Schema instance,
        SqlIdentifier templateName,
        SqlIdentifier schemaName,
        SourcePosition position,
        string? file
    )
    {
        accumulator.CurrentFile = file;
        accumulator.Context = $"APPLY TEMPLATE '{templateName}' IN SCHEMA {schemaName}";
        try
        {
            foreach (var table in instance.Tables)
            {
                accumulator.AddTable(schemaName, table.Clone(), position);
            }
            foreach (var view in instance.Views)
            {
                accumulator.AddView(schemaName, view.Clone(), position);
            }
            foreach (var enumType in instance.Enums)
            {
                accumulator.AddEnum(schemaName, enumType.Clone(), position);
            }
            foreach (var sequence in instance.Sequences)
            {
                accumulator.AddSequence(schemaName, sequence.Clone(), position);
            }
            foreach (var routine in instance.Routines)
            {
                accumulator.AddRoutine(schemaName, routine.Clone(), position);
            }
            foreach (var domain in instance.Domains)
            {
                accumulator.AddDomain(schemaName, domain.Clone(), position);
            }
            foreach (var compositeType in instance.CompositeTypes)
            {
                accumulator.AddCompositeType(schemaName, compositeType.Clone(), position);
            }
            foreach (var grant in instance.Grants)
            {
                accumulator.AddSchemaGrant(schemaName, grant.Role);
            }
        }
        finally
        {
            accumulator.Context = null;
            accumulator.CurrentFile = null;
        }
    }

    /// <summary>
    /// Instantiates a schema template for one target schema: the body's declarations project with the target as
    /// their binding context (then unqualified type and trigger-function references the template itself declares
    /// are qualified to it), while its directives — scripts, and object renames — bind to the applied
    /// schema through the collector, the <c>{schema}</c> token substituted in script bodies.
    /// </summary>
    private static TemplateInstance Instantiate(SchemaTemplateStatement template, SqlIdentifier schemaName)
    {
        var body = new DatabaseAccumulator();
        var directives = new DirectiveCollector();
        foreach (var statement in template.Statements)
        {
            // Directives instantiate into the applied schema; everything else is a declaration of the instance.
            if (!directives.TryAdd(statement, schemaName))
            {
                DocumentProjector.ProjectStatement(statement, body, schemaName);
            }
        }

        var fragment = body.Build();
        var instance = Qualify(fragment.Schemas.SingleOrDefault() ?? new Schema { Name = schemaName }, schemaName);

        return new TemplateInstance(instance, body.Includes, directives.Build());
    }

    /// <summary>
    /// Qualifies unqualified references to what the instance itself declares: a column, domain, or composite
    /// field whose type the template declares points at the instance schema, as does a trigger function it
    /// declares. Object names already bound at projection. The instance is this expansion's own projection,
    /// so it is qualified in place.
    /// </summary>
    private static Schema Qualify(Schema instance, SqlIdentifier schemaName)
    {
        var declaredTypes = instance.Enums.Select(e => e.Name)
            .Concat(instance.Domains.Select(d => d.Name))
            .Concat(instance.CompositeTypes.Select(t => t.Name))
            .ToHashSet();
        var declaredRoutines = instance.Routines.Select(r => r.Name).ToHashSet();

        foreach (var table in instance.Tables)
        {
            foreach (var column in table.Columns)
            {
                column.Type = Qualify(column.Type, declaredTypes, schemaName);
            }
            foreach (var trigger in table.Triggers)
            {
                if (trigger.Function is { Schema: null } function && declaredRoutines.Contains(function.Name))
                {
                    trigger.Function = function with { Schema = schemaName };
                }
            }
        }
        foreach (var domain in instance.Domains)
        {
            domain.DataType = Qualify(domain.DataType, declaredTypes, schemaName);
        }
        foreach (var type in instance.CompositeTypes)
        {
            for (var i = 0; i < type.Fields.Count; i++)
            {
                type.Fields[i] = type.Fields[i] with { DataType = Qualify(type.Fields[i].DataType, declaredTypes, schemaName) };
            }
        }
        return instance;
    }

    private static SqlType Qualify(SqlType type, HashSet<SqlIdentifier> declaredTypes, SqlIdentifier schemaName)
    {
        if (type.Schema is not null)
        {
            return type; // explicitly qualified — escapes the template
        }

        // Only a type the instance itself declares binds to the target schema; a built-in or a type from
        // elsewhere is left for the engine's search path to resolve.
        if (!declaredTypes.Contains(type.Name))
        {
            return type;
        }

        return type with { Schema = schemaName };
    }
}
