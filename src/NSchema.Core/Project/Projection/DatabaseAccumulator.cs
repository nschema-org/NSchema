using NSchema.Model;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Model.XmlSchemaCollections;
using NSchema.Project.Nsql;

namespace NSchema.Project.Projection;

/// <summary>
/// Accumulates parsed statements into a <see cref="Database"/>.
/// </summary>
internal sealed class DatabaseAccumulator
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<SqlIdentifier, Entry> _byName = new();
    private readonly List<Extension> _extensions = [];
    private readonly List<PendingGrant> _tableGrants = [];
    private readonly List<PendingTrigger> _triggers = [];
    private readonly List<PendingIndex> _standaloneIndexes = [];
    private readonly List<TemplateInclude> _includes = [];
    private readonly List<NsqlDiagnostic> _diagnostics = [];

    /// <summary>
    /// The file whose statements are currently accumulating.
    /// </summary>
    public string? CurrentFile { get; set; }

    /// <summary>
    /// A prose prefix for findings recorded while it is set.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// The assembly findings recorded while accumulating — duplicate declarations, references to unknown
    /// tables. Positioned like syntax errors, but they are project semantics, not grammar: the accumulator
    /// records and carries on, so one pass reports every finding.
    /// </summary>
    public IReadOnlyList<NsqlDiagnostic> Diagnostics => _diagnostics;

    private void Report(NsqlDiagnostic diagnostic, string? file) =>
        _diagnostics.Add(diagnostic with
        {
            Text = Context is null ? diagnostic.Text : (FormattedText)$"{Context:text}: {diagnostic.Text}",
            File = file,
        });

    /// <summary>
    /// The <c>INCLUDE</c> members collected from table bodies. Unlike the other pending lists these are not
    /// resolved at <see cref="Build"/> — they resolve against the assembled aggregate in the include-resolution
    /// phase, so they are exported alongside the built schema rather than folded into it.
    /// </summary>
    public IReadOnlyList<TemplateInclude> Includes => _includes;

    public void AddInclude(TemplateInclude include) => _includes.Add(include);

    /// <summary>
    /// Whether a schema entry exists — declared explicitly or vivified by a declaration into it.
    /// </summary>
    public bool HasSchema(SqlIdentifier name) => _byName.ContainsKey(name);

    public void DeclareSchema(SqlIdentifier name, string? comment, SourcePosition position)
    {
        var entry = GetOrAdd(name);

        // An entry that is no longer implicit was declared already: this is the second CREATE SCHEMA for it.
        if (!entry.IsImplicit)
        {
            Report(ProjectDiagnostics.SchemaAlreadyDeclared(name, position), CurrentFile);
            return;
        }

        entry.IsImplicit = false;
        entry.Comment = comment;
    }

    public void AddTable(SqlIdentifier schema, Table table, SourcePosition position) =>
        Add(schema, table, e => e.Tables, position);

    public void AddView(SqlIdentifier schema, View view, SourcePosition position) =>
        Add(schema, view, e => e.Views, position);

    public void AddXmlSchemaCollection(SqlIdentifier schema, XmlSchemaCollection collection, SourcePosition position) =>
        Add(schema, collection, e => e.XmlSchemaCollections, position);

    public void AddEnum(SqlIdentifier schema, EnumType enumType, SourcePosition position) =>
        Add(schema, enumType, e => e.Enums, position);

    public void AddSequence(SqlIdentifier schema, Sequence sequence, SourcePosition position) =>
        Add(schema, sequence, e => e.Sequences, position);

    public void AddDomain(SqlIdentifier schema, DomainType domain, SourcePosition position) =>
        Add(schema, domain, e => e.Domains, position);

    public void AddCompositeType(SqlIdentifier schema, CompositeType compositeType, SourcePosition position) =>
        Add(schema, compositeType, e => e.CompositeTypes, position);

    // Functions and procedures are one routine pool sharing a single name space, as they do in the database:
    // a function and a procedure with the same name cannot coexist in a schema, which a single list enforces.
    public void AddRoutine(SqlIdentifier schema, Routine routine, SourcePosition position) =>
        Add(schema, routine, e => e.Routines, position);

    private void Add<T>(SqlIdentifier schema, T item, Func<Entry, SchemaObjectCollection<T>> collection, SourcePosition position)
        where T : SchemaObject
    {
        var target = collection(GetOrAdd(schema));
        if (target.Any(x => x.Name == item.Name))
        {
            Report(ProjectDiagnostics.ObjectAlreadyDeclared(item.Kind, schema, item.Name, position), CurrentFile);
            return;
        }

        target.Add(item);
    }

    public void AddSchemaGrant(SqlIdentifier schema, SqlIdentifier role)
    {
        var entry = GetOrAdd(schema);
        if (entry.Grants.All(g => g.Role != role))
        {
            entry.Grants.Add(new SchemaGrant(role));
        }
    }

    // Table grants are resolved at Build, so a grant may appear before or after the table it targets — in
    // any file. The pendings capture the current file, since they resolve after their document is done.
    public void AddTableGrant(SqlIdentifier schema, SqlIdentifier table, TableGrant grant, SourcePosition position)
        => _tableGrants.Add(new PendingGrant(schema, table, grant, position, CurrentFile));

    // Triggers are standalone statements attached to their table at Build, so a trigger may appear before or
    // after the CREATE TABLE it targets.
    public void AddTrigger(SqlIdentifier schema, SqlIdentifier table, Trigger trigger, SourcePosition position)
        => _triggers.Add(new PendingTrigger(schema, table, trigger, position, CurrentFile));

    // Standalone indexes attach to their relation (a table or a materialized view) at Build, so an index may
    // appear before or after the CREATE that declares its target.
    public void AddIndex(SqlIdentifier schema, SqlIdentifier relation, TableIndex index, SourcePosition position)
        => _standaloneIndexes.Add(new PendingIndex(schema, relation, index, position, CurrentFile));

    // Extensions are database-global, so they live on the accumulator itself rather than a per-schema entry.
    public void AddExtension(Extension extension, SourcePosition position)
    {
        if (_extensions.Any(e => e.Name == extension.Name))
        {
            Report(ProjectDiagnostics.ExtensionAlreadyDeclared(extension.Name, position), CurrentFile);
            return;
        }

        _extensions.Add(extension);
    }

    public Database Build()
    {
        ApplyTableGrants();
        ApplyTriggers();
        ApplyIndexes();
        var schemas = _entries
            .Select(e => new Schema
            {
                Name = e.Name,
                IsImplicit = e.IsImplicit,
                Tables = e.Tables,
                Grants = e.Grants,
                Views = e.Views,
                XmlSchemaCollections = e.XmlSchemaCollections,
                Enums = e.Enums,
                Sequences = e.Sequences,
                Routines = e.Routines,
                Domains = e.Domains,
                CompositeTypes = e.CompositeTypes,
                Comment = e.Comment,
            });
        return new Database { Schemas = [.. schemas], Extensions = [.. _extensions] };
    }

    private void ApplyTableGrants()
    {
        foreach (var pending in _tableGrants)
        {
            if (!_byName.TryGetValue(pending.Schema, out var entry))
            {
                Report(ProjectDiagnostics.UnknownGrantTable(pending.Schema, pending.Table, pending.Position), pending.File);
                continue;
            }

            var table = entry.Tables.FirstOrDefault(t => t.Name == pending.Table);
            if (table is null)
            {
                Report(ProjectDiagnostics.UnknownGrantTable(pending.Schema, pending.Table, pending.Position), pending.File);
                continue;
            }

            // The same grant declared twice — possibly from two files — is one grant.
            if (!table.Grants.Contains(pending.Grant))
            {
                table.Grants.Add(pending.Grant);
            }
        }
    }

    private void ApplyTriggers()
    {
        foreach (var pending in _triggers)
        {
            if (!_byName.TryGetValue(pending.Schema, out var entry))
            {
                Report(ProjectDiagnostics.UnknownTriggerTable(pending.Schema, pending.Table, pending.Position), pending.File);
                continue;
            }

            var table = entry.Tables.FirstOrDefault(t => t.Name == pending.Table);
            if (table is null)
            {
                Report(ProjectDiagnostics.UnknownTriggerTable(pending.Schema, pending.Table, pending.Position), pending.File);
                continue;
            }

            if (table.Triggers.Any(t => t.Name == pending.Trigger.Name))
            {
                Report(ProjectDiagnostics.TriggerAlreadyDeclared(pending.Trigger.Name, pending.Schema, pending.Table, pending.Position), pending.File);
                continue;
            }

            table.Triggers.Add(pending.Trigger);
        }
    }

    private void ApplyIndexes()
    {
        foreach (var pending in _standaloneIndexes)
        {
            if (!_byName.TryGetValue(pending.Schema, out var entry))
            {
                Report(ProjectDiagnostics.UnknownIndexRelation(pending.Schema, pending.Relation, pending.Position), pending.File);
                continue;
            }

            // A standalone index attaches to a table (the same as an inline index) or a view.
            if (entry.Tables.FirstOrDefault(t => t.Name == pending.Relation) is { } table)
            {
                if (table.Indexes.Any(i => i.Name == pending.Index.Name))
                {
                    Report(ProjectDiagnostics.IndexAlreadyDeclared(pending.Index.Name, pending.Schema, pending.Relation, pending.Position), pending.File);
                    continue;
                }

                table.Indexes.Add(pending.Index);
                continue;
            }

            var view = entry.Views.FirstOrDefault(v => v.Name == pending.Relation);
            if (view is null)
            {
                Report(ProjectDiagnostics.UnknownIndexRelation(pending.Schema, pending.Relation, pending.Position), pending.File);
                continue;
            }
            if (view.Indexes.Any(i => i.Name == pending.Index.Name))
            {
                Report(ProjectDiagnostics.IndexAlreadyDeclared(pending.Index.Name, pending.Schema, pending.Relation, pending.Position), pending.File);
                continue;
            }

            view.Indexes.Add(pending.Index);
        }
    }

    private Entry GetOrAdd(SqlIdentifier name)
    {
        if (_byName.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var entry = new Entry(name);
        _entries.Add(entry);
        _byName[name] = entry;
        return entry;
    }

    private sealed class Entry(SqlIdentifier name)
    {
        public SqlIdentifier Name { get; } = name;
        public bool IsImplicit { get; set; } = true;
        public string? Comment { get; set; }
        public SchemaObjectCollection<Table> Tables { get; } = [];
        public List<SchemaGrant> Grants { get; } = [];
        public SchemaObjectCollection<View> Views { get; } = [];
        public SchemaObjectCollection<EnumType> Enums { get; } = [];
        public SchemaObjectCollection<Sequence> Sequences { get; } = [];
        public SchemaObjectCollection<Routine> Routines { get; } = [];
        public SchemaObjectCollection<DomainType> Domains { get; } = [];
        public SchemaObjectCollection<CompositeType> CompositeTypes { get; } = [];
        public SchemaObjectCollection<XmlSchemaCollection> XmlSchemaCollections { get; } = [];
    }

    private readonly record struct PendingGrant(SqlIdentifier Schema, SqlIdentifier Table, TableGrant Grant, SourcePosition Position, string? File);

    private readonly record struct PendingTrigger(SqlIdentifier Schema, SqlIdentifier Table, Trigger Trigger, SourcePosition Position, string? File);

    private readonly record struct PendingIndex(SqlIdentifier Schema, SqlIdentifier Relation, TableIndex Index, SourcePosition Position, string? File);
}
