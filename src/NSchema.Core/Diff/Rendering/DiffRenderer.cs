using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Routines;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;

namespace NSchema.Diff.Rendering;

/// <summary>
/// Walks a diff and emits the <see cref="DiffDocument"/> behind <see cref="DiffDocument.From"/>.
/// </summary>
internal static class DiffRenderer
{
    public static DiffDocument Render(DatabaseDiff diff)
    {
        var lines = new List<DiffLine>();
        if (diff.IsEmpty)
        {
            return new DiffDocument(lines, diff.GetSummary());
        }

        // Extensions are database-global, so they render at the root before any schema.
        foreach (var extension in diff.Extensions)
        {
            RenderExtension(lines, extension);
        }

        foreach (var schema in diff.Schemas)
        {
            if (schema.Change != ChangeKind.Touched)
            {
                RenderSchema(lines, schema, schema.Change);
            }

            foreach (var table in schema.Tables)
            {
                RenderTable(lines, table);
            }

            foreach (var view in schema.Views)
            {
                RenderView(lines, view);
            }

            foreach (var enumDiff in schema.Enums)
            {
                RenderEnum(lines, enumDiff);
            }

            foreach (var domain in schema.Domains)
            {
                RenderDomain(lines, domain);
            }

            foreach (var compositeType in schema.CompositeTypes)
            {
                RenderCompositeType(lines, compositeType);
            }

            foreach (var sequence in schema.Sequences)
            {
                RenderSequence(lines, sequence);
            }

            foreach (var routine in schema.Routines)
            {
                RenderRoutine(lines, routine);
            }
        }

        RenderDeploymentScripts(lines, diff.DeploymentScripts);

        return new DiffDocument(lines, diff.GetSummary());
    }

    private static void RenderDeploymentScripts(List<DiffLine> lines, IReadOnlyList<DeploymentScript> scripts)
    {
        if (scripts.Count == 0)
        {
            return;
        }

        if (lines.Count > 0)
        {
            lines.Add(DiffLine.Blank);
        }

        foreach (var script in scripts)
        {
            AppendHeader(lines, ChangeKind.Add, $"script {script.Reference} ({EventText(script)})");
        }
    }

    private static string EventText(Script script) => $"on {script.Description.ToLowerInvariant()}";

    private static void RenderSchema(List<DiffLine> lines, SchemaDiff schema, ChangeKind kind)
    {
        var target = schema.RenamedFrom is null ? schema.Name.Value : $"{schema.RenamedFrom} → {schema.Name}";
        AppendHeader(lines, kind, $"schema {target}{CommentSuffix(schema.Comment)}");

        foreach (var grant in schema.Grants)
        {
            var text = grant.Change == ChangeKind.Add ? $"grant usage to {grant.Role}" : $"revoke usage from {grant.Role}";
            AppendDetail(lines, grant.Change, text);
        }
    }

    private static void RenderTable(List<DiffLine> lines, TableDiff table)
    {
        AppendHeader(lines, table.Change, $"table {QualifiedName(table)}{CommentSuffix(table.Comment)}");

        foreach (var column in table.Columns)
        {
            RenderColumn(lines, column);
        }

        // A new table renders its columns as a block, separated from the constraint/index/grant block by a
        // blank line. An existing table lists its column changes inline with everything that follows.
        var hasTrailingBlock = table.PrimaryKeys.Count > 0 || table.ForeignKeys.Count > 0
            || table.UniqueConstraints.Count > 0 || table.Checks.Count > 0
            || table.Indexes.Count > 0 || table.Triggers.Count > 0 || table.Grants.Count > 0;
        if (table is { Change: ChangeKind.Add, Columns.Count: > 0 } && hasTrailingBlock)
        {
            lines.Add(DiffLine.Blank);
        }

        foreach (var pk in table.PrimaryKeys)
        {
            AppendDetail(lines, pk.Change, MemberText("primary key", pk.Name, pk.Change, pk.Comment) + MigrationSuffix(pk.MigrationScript));
        }

        foreach (var fk in table.ForeignKeys)
        {
            AppendDetail(lines, fk.Change, MemberText("foreign key", fk.Name, fk.Change, fk.Comment) + MigrationSuffix(fk.MigrationScript));
        }

        foreach (var unique in table.UniqueConstraints)
        {
            AppendDetail(lines, unique.Change, MemberText("unique constraint", unique.Name, unique.Change, unique.Comment) + MigrationSuffix(unique.MigrationScript));
        }

        foreach (var check in table.Checks)
        {
            AppendDetail(lines, check.Change, MemberText("check constraint", check.Name, check.Change, check.Comment) + MigrationSuffix(check.MigrationScript));
        }

        foreach (var exclusion in table.ExclusionConstraints)
        {
            AppendDetail(lines, exclusion.Change, MemberText("exclusion constraint", exclusion.Name, exclusion.Change, exclusion.Comment) + MigrationSuffix(exclusion.MigrationScript));
        }

        foreach (var index in table.Indexes)
        {
            AppendDetail(lines, index.Change, MemberText("index", index.Name, index.Change, index.Comment));
        }

        foreach (var trigger in table.Triggers)
        {
            AppendDetail(lines, trigger.Change, MemberText("trigger", trigger.Name, trigger.Change, trigger.Comment));
        }

        foreach (var grant in table.Grants)
        {
            var privileges = FormatPrivileges(grant.Privileges);
            var text = grant.Change == ChangeKind.Add
                ? $"grant {privileges} to {grant.Role}"
                : $"revoke {privileges} from {grant.Role}";
            AppendDetail(lines, grant.Change, text);
        }
    }

    private static void RenderView(List<DiffLine> lines, ViewDiff view)
    {
        // A plain ⇄ materialized conversion renders as a label transition, mirroring the rename arrow.
        var label = view.Materialized is { } materialized
            ? $"{ViewLabel(materialized.Old)} → {ViewLabel(materialized.New)}"
            : ViewLabel(view.IsMaterialized);
        var name = QualifiedName(view);

        // A comment-only modify (no body or index change) reports the comment transition rather than a bare header.
        if (view is { Change: ChangeKind.Modify, Definition: null, RenamedFrom: null, Comment: { } only, Indexes.Count: 0 })
        {
            AppendHeader(lines, ChangeKind.Modify, $"{label} {name} {CommentTransition(only)}");
            return;
        }

        AppendHeader(lines, view.Change, $"{label} {name}{CommentSuffix(view.Comment)}");

        // In-place index changes on a materialized view.
        foreach (var index in view.Indexes)
        {
            AppendDetail(lines, index.Change, MemberText("index", index.Name, index.Change, index.Comment));
        }
    }

    private static string ViewLabel(bool isMaterialized) => isMaterialized ? "materialized view" : "view";

    private static void RenderEnum(List<DiffLine> lines, EnumDiff enumDiff)
    {
        var name = QualifiedName(enumDiff);

        // A comment-only modify reports the comment transition rather than a bare header.
        if (enumDiff is { Change: ChangeKind.Modify, Values: null, RenamedFrom: null, Comment: { } only })
        {
            AppendHeader(lines, ChangeKind.Modify, $"enum {name} {CommentTransition(only)}");
            return;
        }

        var values = enumDiff is { Change: ChangeKind.Add, Definition: { } definition }
            ? $" ({string.Join(", ", definition.Values)})"
            : string.Empty;
        AppendHeader(lines, enumDiff.Change, $"enum {name}{values}{CommentSuffix(enumDiff.Comment)}");

        foreach (var addition in enumDiff.AddedValues)
        {
            var anchor = addition switch
            {
                { After: { } after } => $" (after '{after}')",
                { Before: { } before } => $" (before '{before}')",
                _ => " (append)",
            };
            AppendDetail(lines, ChangeKind.Add, $"value '{addition.Value}'{anchor}");
        }

        if (enumDiff.RequiresRecreate)
        {
            AppendDetail(lines, ChangeKind.Modify,
                $"values: [{string.Join(", ", enumDiff.Values!.Old ?? [])}] → [{string.Join(", ", enumDiff.Values!.New ?? [])}]"
                + " (removal/reorder; requires manual recreate)");
        }
    }

    private static void RenderSequence(List<DiffLine> lines, SequenceDiff sequence)
    {
        var name = QualifiedName(sequence);

        // A comment-only modify reports the comment transition rather than a bare header.
        if (sequence is { Change: ChangeKind.Modify, Options: null, RenamedFrom: null, Comment: { } only })
        {
            AppendHeader(lines, ChangeKind.Modify, $"sequence {name} {CommentTransition(only)}");
            return;
        }

        var options = sequence is { Change: ChangeKind.Add, Definition: { } definition }
            && FormatSequenceOptions(definition.Options) != "<default>"
            ? $" ({FormatSequenceOptions(definition.Options)})"
            : string.Empty;
        AppendHeader(lines, sequence.Change, $"sequence {name}{options}{CommentSuffix(sequence.Comment)}");

        if (sequence.Options is { } change)
        {
            AppendDetail(lines, ChangeKind.Modify, $"options: {FormatSequenceOptions(change.Old)} → {FormatSequenceOptions(change.New)}");
        }
    }

    private static void RenderRoutine(List<DiffLine> lines, RoutineDiff routine)
    {
        var label = routine.RoutineKind == RoutineKind.Procedure ? "procedure" : "function";
        var name = QualifiedName(routine);

        // A comment-only modify reports the comment transition rather than a bare header.
        if (routine is { Change: ChangeKind.Modify, Definition: null, RenamedFrom: null, Comment: { } only })
        {
            AppendHeader(lines, ChangeKind.Modify, $"{label} {name} {CommentTransition(only)}");
            return;
        }

        var arguments = routine is { Change: ChangeKind.Add, Definition: { } definition } ? $"({definition.Arguments})" : string.Empty;
        AppendHeader(lines, routine.Change, $"{label} {name}{arguments}{CommentSuffix(routine.Comment)}");

        if (routine.Arguments is { } change)
        {
            AppendDetail(lines, ChangeKind.Modify, $"arguments: ({change.Old}) → ({change.New}) (recreate)");
        }
    }

    private static void RenderExtension(List<DiffLine> lines, ExtensionDiff extension)
    {
        // A comment-only modify reports the comment transition rather than a bare header.
        if (extension is { Change: ChangeKind.Modify, Version: null, Comment: { } only })
        {
            AppendHeader(lines, ChangeKind.Modify, $"extension {extension.Name} {CommentTransition(only)}");
            return;
        }

        var version = extension is { Change: ChangeKind.Add, Definition.Version: { } added } ? $" version {added}" : string.Empty;
        AppendHeader(lines, extension.Change, $"extension {extension.Name}{version}{CommentSuffix(extension.Comment)}");

        if (extension.Version is { } change)
        {
            AppendDetail(lines, ChangeKind.Modify, $"version: {FormatVersion(change.Old)} → {FormatVersion(change.New)}");
        }
    }

    private static void RenderDomain(List<DiffLine> lines, DomainDiff domain)
    {
        var name = QualifiedName(domain);

        // A comment-only modify reports the comment transition rather than a bare header.
        if (domain is { Change: ChangeKind.Modify, Definition: null, RenamedFrom: null, DataType: null, Default: null, NotNull: null, Comment: { } only, Checks.Count: 0 })
        {
            AppendHeader(lines, ChangeKind.Modify, $"domain {name} {CommentTransition(only)}");
            return;
        }

        var type = domain is { Change: ChangeKind.Add, Definition: { } definition } ? $" {definition.DataType}" : string.Empty;
        AppendHeader(lines, domain.Change, $"domain {name}{type}{CommentSuffix(domain.Comment)}");

        if (domain.DataType is { } dataType)
        {
            AppendDetail(lines, ChangeKind.Modify, $"type: {dataType.Old} → {dataType.New} (recreate)");
        }
        if (domain.Default is { } @default)
        {
            AppendDetail(lines, ChangeKind.Modify, $"default: {FormatDefault(@default.Old)} → {FormatDefault(@default.New)}");
        }
        if (domain.NotNull is { } notNull)
        {
            AppendDetail(lines, ChangeKind.Modify, $"nullable: {FormatNullability(!notNull.Old)} → {FormatNullability(!notNull.New)}");
        }
        foreach (var check in domain.Checks)
        {
            AppendDetail(lines, check.Change, $"check {check.Name}");
        }
    }

    private static void RenderCompositeType(List<DiffLine> lines, CompositeTypeDiff type)
    {
        var name = QualifiedName(type);

        // A comment-only modify reports the comment transition rather than a bare header.
        if (type is { Change: ChangeKind.Modify, Definition: null, RenamedFrom: null, Comment: { } only, Fields.Count: 0 })
        {
            AppendHeader(lines, ChangeKind.Modify, $"type {name} {CommentTransition(only)}");
            return;
        }

        var fields = type is { Change: ChangeKind.Add, Definition: { } definition }
            ? $" ({string.Join(", ", definition.Fields.Select(f => $"{f.Name} {f.DataType}"))})"
            : string.Empty;
        AppendHeader(lines, type.Change, $"type {name}{fields}{CommentSuffix(type.Comment)}");

        foreach (var field in type.Fields)
        {
            var text = field switch
            {
                { Change: ChangeKind.Add, Definition: { } def } => $"field {def.Name} {def.DataType}",
                { Change: ChangeKind.Modify, Type: { } change } => $"field {field.Name} type: {change.Old} → {change.New}",
                _ => $"field {field.Name}",
            };
            AppendDetail(lines, field.Change, text);
        }
    }

    private static void RenderColumn(List<DiffLine> lines, ColumnDiff column)
    {
        if (column is { Change: ChangeKind.Add, Definition: { } added })
        {
            AppendDetail(lines, ChangeKind.Add, FormatColumn(added) + CommentSuffix(column.Comment) + MigrationSuffix(column.MigrationScript));
            return;
        }

        if (column is { Change: ChangeKind.Remove, Definition: { } removed })
        {
            AppendDetail(lines, ChangeKind.Remove, FormatColumn(removed));
            return;
        }

        if (column.RenamedFrom is not null)
        {
            AppendDetail(lines, ChangeKind.Modify, $"rename column: {column.RenamedFrom} → {column.Name}");
        }

        if (column.Type is { } type)
        {
            AppendDetail(lines, ChangeKind.Modify, $"{column.Name} type: {type.Old} → {type.New}{MigrationSuffix(column.MigrationScript)}");
        }

        if (column.Nullability is { } nullable)
        {
            AppendDetail(lines, ChangeKind.Modify, $"{column.Name} nullable: {FormatNullability(nullable.Old)} → {FormatNullability(nullable.New)}");
        }

        if (column.Default is { } @default)
        {
            AppendDetail(lines, ChangeKind.Modify, $"{column.Name} default: {FormatDefault(@default.Old)} → {FormatDefault(@default.New)}");
        }

        if (column.Generated is { } generated)
        {
            AppendDetail(lines, ChangeKind.Modify, $"{column.Name} generated: {FormatDefault(generated.Old)} → {FormatDefault(generated.New)}");
        }

        if (column.Identity is { } identity)
        {
            AppendDetail(lines, ChangeKind.Modify, $"{column.Name} identity: {FormatIdentity(identity.Old)} → {FormatIdentity(identity.New)}");
        }

        if (column.Comment is { } comment)
        {
            AppendDetail(lines, ChangeKind.Modify, $"{column.Name} {CommentTransition(comment)}");
        }
    }

    private static void AppendHeader(List<DiffLine> lines, ChangeKind kind, string text)
    {
        // A blank spacer separates this block from the previous one; the first block needs none, so the
        // document never opens with a blank line and consumers don't have to trim one.
        if (lines.Count > 0)
        {
            lines.Add(DiffLine.Blank);
        }

        lines.Add(new DiffLine(kind, 0, text));
    }

    // A detail beneath the current header (depth 1).
    private static void AppendDetail(List<DiffLine> lines, ChangeKind kind, string text) =>
        lines.Add(new DiffLine(kind, 1, text));

    // -------------------------------------------------------------------------
    // Formatters
    // -------------------------------------------------------------------------

    private static string FormatColumn(Column column) =>
        $"{column.Name} {column.Type} {(column.IsNullable ? "null" : "not null")}"
        + (column.IsIdentity ? " identity" : string.Empty)
        + (column.DefaultExpression is { } @default ? $" default {@default}" : string.Empty)
        + (column.GeneratedExpression is { } generated ? $" generated as ({generated})" : string.Empty);

    private static string CommentSuffix(ValueChange<string>? comment) => comment is null
        ? string.Empty
        : comment.Old is null
            ? $" ({FormatComment(comment.New)})"
            : $" ({FormatComment(comment.Old)} → {FormatComment(comment.New)})";

    private static string MigrationSuffix(ChangeScript? script) =>
        script is null ? string.Empty : $" (with migration {script.Name})";

    private static string FormatPrivileges(TablePrivilege? privileges) =>
        privileges is not { } granted || granted == TablePrivilege.None
            ? "no privileges"
            : string.Join(", ", granted.SqlNames());

    private static string FormatComment(string? comment) => comment is null ? "<none>" : $"\"{comment}\"";

    private static string FormatVersion(string? version) => version ?? "<default>";

    // A table-member Modify is always a comment-only change (structural changes are Remove + Add).
    private static string MemberText(string label, SqlIdentifier name, ChangeKind kind, ValueChange<string>? comment) =>
        kind == ChangeKind.Modify
            ? $"{label} {name} {CommentTransition(comment)}"
            : $"{label} {name}";

    private static string CommentTransition(ValueChange<string>? comment) =>
        $"comment: {FormatComment(comment?.Old)} → {FormatComment(comment?.New)}";

    // The schema-qualified object name, rendering a rename as its transition.
    private static string QualifiedName(ISchemaObjectDiff diff) => diff.RenamedFrom is null
        ? $"{diff.Schema}.{diff.Name}"
        : $"{diff.Schema}.{diff.RenamedFrom} → {diff.Name}";

    private static string FormatDefault(ValueObject<string>? value) => value?.Value ?? "<none>";

    private static string FormatNullability(bool? nullable) => nullable == true ? "null" : "not null";

    private static string FormatIdentity(IdentityOptions? options)
    {
        if (options is null)
        {
            return "<none>";
        }

        var parts = new List<string>(3);
        if (options.StartWith.HasValue)
        {
            parts.Add($"start={options.StartWith}");
        }

        if (options.MinValue.HasValue)
        {
            parts.Add($"min={options.MinValue}");
        }

        if (options.IncrementBy.HasValue)
        {
            parts.Add($"step={options.IncrementBy}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "<default>";
    }

    private static string FormatSequenceOptions(SequenceOptions? options)
    {
        if (options is null)
        {
            return "<none>";
        }

        var parts = new List<string>(7);
        if (options.DataType is { } type)
        {
            parts.Add($"as={type}");
        }

        if (options.StartWith.HasValue)
        {
            parts.Add($"start={options.StartWith}");
        }

        if (options.IncrementBy.HasValue)
        {
            parts.Add($"step={options.IncrementBy}");
        }

        if (options.MinValue.HasValue)
        {
            parts.Add($"min={options.MinValue}");
        }

        if (options.MaxValue.HasValue)
        {
            parts.Add($"max={options.MaxValue}");
        }

        if (options.Cache.HasValue)
        {
            parts.Add($"cache={options.Cache}");
        }

        if (options.Cycle)
        {
            parts.Add("cycle");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "<default>";
    }
}
