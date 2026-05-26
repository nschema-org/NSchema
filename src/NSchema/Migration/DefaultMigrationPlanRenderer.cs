using System.Text;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Default <see cref="IMigrationPlanRenderer"/> that produces a Terraform-style diff.
/// Uses ANSI colour when the output is going to a TTY and <c>NO_COLOR</c> is not set.
/// </summary>
internal sealed class DefaultMigrationPlanRenderer : IMigrationPlanRenderer
{
    public string Render(MigrationPlan plan)
    {
        if (plan.IsEmpty)
        {
            return "No changes detected.";
        }

        var palette = Palette.ForCurrentEnvironment();
        var sb = new StringBuilder();

        var scripted = new List<RunScript>();
        var grouped = new Dictionary<GroupKey, List<MigrationAction>>();
        foreach (var action in plan.Actions)
        {
            if (action is RunScript script)
            {
                scripted.Add(script);
                continue;
            }
            var key = GroupKeyFor(action);
            if (!grouped.TryGetValue(key, out var list))
            {
                grouped[key] = list = [];
            }
            list.Add(action);
        }

        var groupBlocks = grouped
            .Select(g => RenderGroup(g.Key, g.Value, palette))
            .OrderBy(b => b.SortKey, StringComparer.Ordinal)
            .ToList();

        var adds = groupBlocks.Count(b => b.Kind == ChangeKind.Add);
        var changes = groupBlocks.Count(b => b.Kind == ChangeKind.Modify);
        var destroys = groupBlocks.Count(b => b.Kind == ChangeKind.Remove);

        sb.AppendLine($"Plan: {adds} to add, {changes} to change, {destroys} to destroy.");

        foreach (var block in groupBlocks)
        {
            sb.AppendLine();
            sb.AppendLine(block.Text);
        }

        var preScripts = scripted.Where(s => s.Script.Type == ScriptType.PreDeployment).ToList();
        var postScripts = scripted.Where(s => s.Script.Type == ScriptType.PostDeployment).ToList();

        if (preScripts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Pre-deployment scripts:");
            foreach (var script in preScripts)
            {
                sb.AppendLine($"  • {script.Script.Name}");
            }
        }

        if (postScripts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Post-deployment scripts:");
            foreach (var script in postScripts)
            {
                sb.AppendLine($"  • {script.Script.Name}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static GroupKey GroupKeyFor(MigrationAction action) => action switch
    {
        CreateSchema a => new GroupKey(a.SchemaName, null),
        DropSchema a => new GroupKey(a.SchemaName, null),
        RenameSchema a => new GroupKey(a.NewName, null),
        SetSchemaComment a => new GroupKey(a.SchemaName, null),
        GrantSchemaUsage a => new GroupKey(a.SchemaName, null),
        RevokeSchemaUsage a => new GroupKey(a.SchemaName, null),

        CreateTable a => new GroupKey(a.SchemaName, a.Table.Name),
        DropTable a => new GroupKey(a.SchemaName, a.TableName),
        RenameTable a => new GroupKey(a.SchemaName, a.NewName),
        SetTableComment a => new GroupKey(a.SchemaName, a.TableName),
        GrantTablePrivileges a => new GroupKey(a.SchemaName, a.TableName),
        RevokeTablePrivileges a => new GroupKey(a.SchemaName, a.TableName),

        AddColumn a => new GroupKey(a.SchemaName, a.TableName),
        DropColumn a => new GroupKey(a.SchemaName, a.TableName),
        RenameColumn a => new GroupKey(a.SchemaName, a.TableName),
        AlterColumnType a => new GroupKey(a.SchemaName, a.TableName),
        AlterColumnNullability a => new GroupKey(a.SchemaName, a.TableName),
        AlterIdentitySequence a => new GroupKey(a.SchemaName, a.TableName),
        SetColumnDefault a => new GroupKey(a.SchemaName, a.TableName),
        SetColumnComment a => new GroupKey(a.SchemaName, a.TableName),

        CreateIndex a => new GroupKey(a.SchemaName, a.TableName),
        DropIndex a => new GroupKey(a.SchemaName, a.TableName),
        SetIndexComment a => new GroupKey(a.SchemaName, a.TableName),

        AddPrimaryKey a => new GroupKey(a.SchemaName, a.TableName),
        DropPrimaryKey a => new GroupKey(a.SchemaName, a.TableName),
        AddForeignKey a => new GroupKey(a.SchemaName, a.TableName),
        DropForeignKey a => new GroupKey(a.SchemaName, a.TableName),

        _ => throw new NotSupportedException($"Renderer cannot group action of type {action.GetType().Name}."),
    };

    private static GroupBlock RenderGroup(GroupKey key, IReadOnlyList<MigrationAction> actions, Palette palette)
    {
        var (kind, headerLabel, headerTarget) = ClassifyGroup(key, actions);
        var marker = palette.For(kind);
        var sb = new StringBuilder();
        sb.Append(marker).Append(' ').Append(headerLabel).Append(' ').AppendLine(headerTarget);

        // For Add/Remove of a brand-new table, show the columns the create defined.
        if (key.TableName is not null && kind == ChangeKind.Add &&
            actions.OfType<CreateTable>().FirstOrDefault() is { } createdTable)
        {
            foreach (var column in createdTable.Table.Columns)
            {
                sb.Append("    ").Append(palette.For(ChangeKind.Add)).Append(' ')
                  .AppendLine(FormatColumn(column));
            }
        }
        else
        {
            foreach (var action in actions)
            {
                // The create/drop that drives a group's headline is implicit; nested CreateTable
                // already had its columns enumerated above.
                if (kind == ChangeKind.Add && action is CreateSchema or CreateTable)
                {
                    continue;
                }
                if (kind == ChangeKind.Remove && action is DropSchema or DropTable)
                {
                    continue;
                }

                var (lineKind, detail) = DescribeAction(action);
                sb.Append("    ").Append(palette.For(lineKind)).Append(' ').AppendLine(detail);
            }
        }

        var sortKey = key.TableName is null ? $"0:{key.SchemaName}" : $"1:{key.SchemaName}.{key.TableName}";
        return new GroupBlock(sortKey, kind, sb.ToString().TrimEnd('\r', '\n'));
    }

    private static (ChangeKind kind, string label, string target) ClassifyGroup(GroupKey key, IReadOnlyList<MigrationAction> actions)
    {
        if (key.TableName is null)
        {
            var schemaKind =
                actions.Any(a => a is CreateSchema) ? ChangeKind.Add :
                actions.Any(a => a is DropSchema) ? ChangeKind.Remove :
                ChangeKind.Modify;
            return (schemaKind, "schema", key.SchemaName);
        }

        var tableKind =
            actions.Any(a => a is CreateTable) ? ChangeKind.Add :
            actions.Any(a => a is DropTable) ? ChangeKind.Remove :
            ChangeKind.Modify;
        return (tableKind, "table", $"{key.SchemaName}.{key.TableName}");
    }

    private static (ChangeKind, string) DescribeAction(MigrationAction action) => action switch
    {
        CreateSchema a => (ChangeKind.Add, $"schema {a.SchemaName}"),
        DropSchema a => (ChangeKind.Remove, $"schema {a.SchemaName}"),
        RenameSchema a => (ChangeKind.Modify, $"rename: {a.OldName} → {a.NewName}"),
        SetSchemaComment a => (ChangeKind.Modify, $"comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),
        GrantSchemaUsage a => (ChangeKind.Add, $"grant usage to {a.Role}"),
        RevokeSchemaUsage a => (ChangeKind.Remove, $"revoke usage from {a.Role}"),

        CreateTable a => (ChangeKind.Add, $"table {a.Table.Name}"),
        DropTable a => (ChangeKind.Remove, $"table {a.TableName}"),
        RenameTable a => (ChangeKind.Modify, $"rename: {a.OldName} → {a.NewName}"),
        SetTableComment a => (ChangeKind.Modify, $"comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),
        GrantTablePrivileges a => (ChangeKind.Add, $"grant {a.Privileges} to {a.Role}"),
        RevokeTablePrivileges a => (ChangeKind.Remove, $"revoke {a.Privileges} from {a.Role}"),

        AddColumn a => (ChangeKind.Add, FormatColumn(a.Column)),
        DropColumn a => (ChangeKind.Remove, a.ColumnName),
        RenameColumn a => (ChangeKind.Modify, $"rename column: {a.OldName} → {a.NewName}"),
        AlterColumnType a => (ChangeKind.Modify, $"{a.ColumnName} type: {a.OldType} → {a.NewType}"),
        AlterColumnNullability a => (ChangeKind.Modify, $"{a.ColumnName} nullable: {a.OldNullable.ToString().ToLowerInvariant()} → {a.NewNullable.ToString().ToLowerInvariant()}"),
        AlterIdentitySequence a => (ChangeKind.Modify, $"{a.ColumnName} identity: {FormatIdentity(a.OldOptions)} → {FormatIdentity(a.NewOptions)}"),
        SetColumnDefault a => (ChangeKind.Modify, $"{a.ColumnName} default: {FormatDefault(a.OldDefault)} → {FormatDefault(a.NewDefault)}"),
        SetColumnComment a => (ChangeKind.Modify, $"{a.ColumnName} comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),

        CreateIndex a => (ChangeKind.Add, $"index {a.Index.Name}"),
        DropIndex a => (ChangeKind.Remove, $"index {a.IndexName}"),
        SetIndexComment a => (ChangeKind.Modify, $"index {a.IndexName} comment: {FormatComment(a.OldComment)} → {FormatComment(a.NewComment)}"),

        AddPrimaryKey a => (ChangeKind.Add, $"primary key {a.PrimaryKey.Name}"),
        DropPrimaryKey a => (ChangeKind.Remove, $"primary key {a.PrimaryKeyName}"),
        AddForeignKey a => (ChangeKind.Add, $"foreign key {a.ForeignKey.Name}"),
        DropForeignKey a => (ChangeKind.Remove, $"foreign key {a.ForeignKeyName}"),

        _ => throw new NotSupportedException($"Renderer cannot describe action of type {action.GetType().Name}."),
    };

    private static string FormatColumn(Column column)
    {
        var nullability = column.IsNullable ? "null" : "not null";
        return $"{column.Name} {column.Type} {nullability}";
    }

    private static string FormatComment(string? comment) => comment is null ? "<none>" : $"\"{comment}\"";

    private static string FormatDefault(string? value) => value is null ? "<none>" : value;

    private static string FormatIdentity(IdentityOptions? options) => options is null ? "<none>" : options.ToString() ?? "<set>";

    private enum ChangeKind { Add, Modify, Remove }

    private readonly record struct GroupKey(string SchemaName, string? TableName);

    private readonly record struct GroupBlock(string SortKey, ChangeKind Kind, string Text);

    // Encapsulates ANSI rendering. When colour is disabled we substitute the plain marker
    // characters so the rest of the renderer doesn't have to branch on environment state.
    private sealed class Palette
    {
        private const string Reset = "\x1b[0m";
        private const string Green = "\x1b[32m";
        private const string Red = "\x1b[31m";
        private const string Yellow = "\x1b[33m";

        private readonly string _add;
        private readonly string _remove;
        private readonly string _modify;

        private Palette(string add, string remove, string modify)
        {
            _add = add;
            _remove = remove;
            _modify = modify;
        }

        public string For(ChangeKind kind) => kind switch
        {
            ChangeKind.Add => _add,
            ChangeKind.Remove => _remove,
            ChangeKind.Modify => _modify,
            _ => "?",
        };

        public static Palette ForCurrentEnvironment()
        {
            var noColor = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            var tty = !Console.IsOutputRedirected;
            return noColor || !tty
                ? new Palette("+", "-", "~")
                : new Palette($"{Green}+{Reset}", $"{Red}-{Reset}", $"{Yellow}~{Reset}");
        }
    }
}
