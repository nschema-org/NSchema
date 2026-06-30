using System.Text;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders a <see cref="DatabaseSchema"/> as human-readable text, presenting it as an indented tree.
/// </summary>
internal static class SchemaRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders the given schema as human-readable text.
    /// </summary>
    public static string Render(DatabaseSchema schema)
    {
        if (schema.Schemas.Count == 0 && schema.Extensions.Count == 0)
        {
            return "Schema is empty.";
        }

        var sb = new StringBuilder();

        // Extensions are database-global, so they render at the root before any schema.
        foreach (var extension in schema.Extensions)
        {
            sb.AppendLine();
            sb.Append("extension ").Append(extension.Name);
            if (extension.Version is { } version)
            {
                sb.Append(" (version ").Append(version).Append(')');
            }
            sb.AppendLine(CommentSuffix(extension.Comment));
        }

        foreach (var definition in schema.Schemas)
        {
            RenderSchema(sb, definition);
        }

        return sb.ToString().Trim();
    }

    private static void RenderSchema(StringBuilder sb, SchemaDefinition schema)
    {
        sb.AppendLine();
        sb.Append("schema ").Append(schema.Name).AppendLine(CommentSuffix(schema.Comment));

        foreach (var grant in schema.Grants)
        {
            sb.Append(Indent).Append("grant usage to ").AppendLine(grant.Role);
        }

        foreach (var table in schema.Tables)
        {
            RenderTable(sb, table);
        }

        foreach (var view in schema.Views)
        {
            RenderView(sb, view);
        }

        foreach (var enumType in schema.Enums)
        {
            sb.Append(Indent).Append("enum ").Append(enumType.Name)
                .Append(" (").Append(string.Join(", ", enumType.Values)).Append(')')
                .AppendLine(CommentSuffix(enumType.Comment));
        }

        foreach (var domain in schema.Domains)
        {
            sb.Append(Indent).Append("domain ").Append(domain.Name)
                .Append(" (").Append(domain.DataType);
            if (domain.NotNull)
            {
                sb.Append(" not null");
            }
            if (domain.Default is { } @default)
            {
                sb.Append(" default ").Append(@default);
            }
            sb.Append(')').AppendLine(CommentSuffix(domain.Comment));
            foreach (var check in domain.Checks)
            {
                sb.Append(Indent).Append(Indent).Append("check ").Append(check.Name)
                    .Append(" (").Append(check.Expression).Append(')').AppendLine();
            }
        }

        foreach (var compositeType in schema.CompositeTypes)
        {
            sb.Append(Indent).Append("type ").Append(compositeType.Name)
                .Append(" (").Append(string.Join(", ", compositeType.Fields.Select(f => $"{f.Name} {f.DataType}"))).Append(')')
                .AppendLine(CommentSuffix(compositeType.Comment));
        }

        foreach (var sequence in schema.Sequences)
        {
            sb.Append(Indent).Append("sequence ").Append(sequence.Name);
            if (FormatSequenceOptions(sequence.Options) is { } options)
            {
                sb.Append(" (").Append(options).Append(')');
            }
            sb.AppendLine(CommentSuffix(sequence.Comment));
        }

        // Routines render as kind + name + arguments only; the definition is opaque and arbitrarily long.
        foreach (var routine in schema.Routines)
        {
            var label = routine.Kind == RoutineKind.Procedure ? "procedure" : "function";
            sb.Append(Indent).Append(label).Append(' ').Append(routine.Name)
                .Append('(').Append(routine.Arguments).Append(')')
                .AppendLine(CommentSuffix(routine.Comment));
        }
    }

    private static string? FormatSequenceOptions(SequenceOptions options)
    {
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

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static void RenderView(StringBuilder sb, View view)
    {
        var label = view.IsMaterialized ? "materialized view" : "view";
        sb.Append(Indent).Append(label).Append(' ').Append(view.Name).AppendLine(CommentSuffix(view.Comment));
        foreach (var dependency in view.DependsOn)
        {
            sb.Append(Indent).Append(Indent).Append("reads ").Append(dependency.Schema).Append('.').AppendLine(dependency.Name);
        }
        foreach (var index in view.Indexes)
        {
            RenderIndex(sb, index);
        }
    }

    private static void RenderTable(StringBuilder sb, Table table)
    {
        sb.Append(Indent).Append("table ").Append(table.Name).AppendLine(CommentSuffix(table.Comment));

        foreach (var column in table.Columns)
        {
            sb.Append(Indent).Append(Indent).AppendLine(FormatColumn(column));
        }

        if (table.PrimaryKey is { } pk)
        {
            sb.Append(Indent).Append(Indent)
                .Append("primary key ").Append(pk.Name)
                .Append(" (").Append(string.Join(", ", pk.ColumnNames)).Append(')')
                .AppendLine(CommentSuffix(pk.Comment));
        }

        foreach (var fk in table.ForeignKeys)
        {
            sb.Append(Indent).Append(Indent)
                .Append("foreign key ").Append(fk.Name)
                .Append(" (").Append(string.Join(", ", fk.ColumnNames)).Append(") -> ")
                .Append(fk.ReferencedSchema).Append('.').Append(fk.ReferencedTable)
                .Append(" (").Append(string.Join(", ", fk.ReferencedColumnNames)).Append(')')
                .AppendLine(CommentSuffix(fk.Comment));
        }

        foreach (var unique in table.UniqueConstraints)
        {
            sb.Append(Indent).Append(Indent)
                .Append("unique ").Append(unique.Name)
                .Append(" (").Append(string.Join(", ", unique.ColumnNames)).Append(')')
                .AppendLine(CommentSuffix(unique.Comment));
        }

        foreach (var check in table.CheckConstraints)
        {
            sb.Append(Indent).Append(Indent)
                .Append("check ").Append(check.Name)
                .Append(" (").Append(check.Expression).Append(')')
                .AppendLine(CommentSuffix(check.Comment));
        }

        foreach (var exclusion in table.ExclusionConstraints)
        {
            sb.Append(Indent).Append(Indent)
                .Append("exclude ").Append(exclusion.Name);
            if (exclusion.Method is { } method)
            {
                sb.Append(" using ").Append(method);
            }
            sb.Append(" (").Append(string.Join(", ", exclusion.Elements.Select(e =>
                $"{(e.IsExpression ? $"({e.Expression})" : e.Expression)} with {e.Operator}"))).Append(')');
            sb.AppendLine(CommentSuffix(exclusion.Comment));
        }

        foreach (var index in table.Indexes)
        {
            RenderIndex(sb, index);
        }

        foreach (var trigger in table.Triggers)
        {
            sb.Append(Indent).Append(Indent)
                .Append("trigger ").Append(trigger.Name)
                .Append(' ').Append(FormatTrigger(trigger))
                .AppendLine(CommentSuffix(trigger.Comment));
        }

        foreach (var grant in table.Grants)
        {
            sb.Append(Indent).Append(Indent)
                .Append("grant ").Append(FormatPrivileges(grant.Privileges))
                .Append(" to ").AppendLine(grant.Role);
        }
    }

    private static string FormatTrigger(Trigger trigger)
    {
        var timing = trigger.Timing switch
        {
            TriggerTiming.Before => "before",
            TriggerTiming.After => "after",
            _ => "instead of",
        };
        var level = trigger.Level == TriggerLevel.Row ? "row" : "statement";
        return $"{timing} {FormatTriggerEvents(trigger)} {level} -> {trigger.Function}";
    }

    private static string FormatTriggerEvents(Trigger trigger)
    {
        var parts = new List<string>(4);
        if (trigger.Events.HasFlag(TriggerEvent.Insert))
        {
            parts.Add("insert");
        }
        if (trigger.Events.HasFlag(TriggerEvent.Update))
        {
            parts.Add(trigger.UpdateOfColumns.Count > 0 ? $"update of ({string.Join(", ", trigger.UpdateOfColumns)})" : "update");
        }
        if (trigger.Events.HasFlag(TriggerEvent.Delete))
        {
            parts.Add("delete");
        }
        if (trigger.Events.HasFlag(TriggerEvent.Truncate))
        {
            parts.Add("truncate");
        }
        return string.Join(", ", parts);
    }

    private static string FormatColumn(Column column)
    {
        var text = $"{column.Name} {column.Type} {(column.IsNullable ? "null" : "not null")}";
        if (column.IsIdentity)
        {
            text += " identity";
        }

        if (column.DefaultExpression is { } @default)
        {
            text += $" default {@default}";
        }

        if (column.GeneratedExpression is { } generated)
        {
            text += $" generated as ({generated})";
        }

        return text + CommentSuffix(column.Comment);
    }

    private static void RenderIndex(StringBuilder sb, TableIndex index)
    {
        sb.Append(Indent).Append(Indent)
            .Append("index ").Append(index.Name)
            .Append(" (").Append(string.Join(", ", index.Columns.Select(FormatIndexKey))).Append(')');
        if (index.Method is { } method)
        {
            sb.Append(" using ").Append(method);
        }
        if (index.Include.Count > 0)
        {
            sb.Append(" include (").Append(string.Join(", ", index.Include)).Append(')');
        }
        sb.Append(index.IsUnique ? " unique" : string.Empty)
            .AppendLine(index.Predicate is { } p ? $" where {p}" : string.Empty);
    }

    private static string FormatIndexKey(IndexColumn column)
    {
        var text = column.IsExpression ? $"({column.Expression})" : column.Expression;
        text += column.Sort switch
        {
            IndexSort.Ascending => " asc",
            IndexSort.Descending => " desc",
            _ => string.Empty,
        };
        text += column.Nulls switch
        {
            IndexNulls.First => " nulls first",
            IndexNulls.Last => " nulls last",
            _ => string.Empty,
        };
        return text;
    }

    private static string CommentSuffix(string? comment) => comment is null ? string.Empty : $" (\"{comment}\")";

    // Decompose the privilege flags into the underlying SQL privileges rather than rendering the enum name,
    // which would surface aliases (e.g. ReadOnly for Select) and composites (All) instead of the real grants.
    private static string FormatPrivileges(TablePrivilege privileges)
    {
        if (privileges == TablePrivilege.None)
        {
            return "no privileges";
        }

        var parts = new List<string>(4);
        if (privileges.HasFlag(TablePrivilege.Select))
        {
            parts.Add("SELECT");
        }

        if (privileges.HasFlag(TablePrivilege.Insert))
        {
            parts.Add("INSERT");
        }

        if (privileges.HasFlag(TablePrivilege.Update))
        {
            parts.Add("UPDATE");
        }

        if (privileges.HasFlag(TablePrivilege.Delete))
        {
            parts.Add("DELETE");
        }

        return string.Join(", ", parts);
    }
}
