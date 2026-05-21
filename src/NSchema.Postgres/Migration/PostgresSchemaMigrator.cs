using Npgsql;
using NSchema.Migration;
using NSchema.Migration.Actions;
using NSchema.Schema;

namespace NSchema.Postgres.Migration;

public sealed class PostgresSchemaMigrator(NpgsqlDataSource dataSource) : ISchemaMigrator
{
    public async Task Migrate(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        foreach (var action in plan.Actions)
        {
            string sql = GenerateSql(action);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    // ── SQL generation ────────────────────────────────────────────────────────

    private static string GenerateSql(SchemaAction action) => action switch
    {
        CreateSchema x => $"""CREATE SCHEMA IF NOT EXISTS "{x.SchemaName}" """,
        DropSchema x => $"""DROP SCHEMA "{x.SchemaName}" CASCADE""",
        RenameSchema x => $"""ALTER SCHEMA "{x.OldName}" RENAME TO "{x.NewName}" """,
        CreateTable x => BuildCreateTable(x),
        DropTable x => $"""DROP TABLE "{x.SchemaName}"."{x.TableName}" """,
        RenameTable x => $"""ALTER TABLE "{x.SchemaName}"."{x.OldName}" RENAME TO "{x.NewName}" """,
        AddColumn x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ADD COLUMN {BuildColumnDef(x.Column)}""",
        DropColumn x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" DROP COLUMN "{x.ColumnName}" """,
        RenameColumn x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" RENAME COLUMN "{x.OldName}" TO "{x.NewName}" """,
        AlterColumnType x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ALTER COLUMN "{x.ColumnName}" TYPE {ToPostgresType(x.NewType)}""",
        AlterColumnNullability { IsNullable: false } x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ALTER COLUMN "{x.ColumnName}" SET NOT NULL""",
        AlterColumnNullability x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ALTER COLUMN "{x.ColumnName}" DROP NOT NULL""",
        AlterIdentitySequence x => BuildAlterIdentitySequence(x),
        SetColumnDefault { NewDefault: null } x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ALTER COLUMN "{x.ColumnName}" DROP DEFAULT""",
        SetColumnDefault x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ALTER COLUMN "{x.ColumnName}" SET DEFAULT {x.NewDefault}""",
        AddPrimaryKey x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ADD CONSTRAINT "{x.PrimaryKey.Name}" PRIMARY KEY ({ColList(x.PrimaryKey.ColumnNames)})""",
        DropPrimaryKey x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" DROP CONSTRAINT "{x.PrimaryKeyName}" """,
        AddForeignKey x => BuildAddForeignKey(x),
        DropForeignKey x => $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" DROP CONSTRAINT "{x.ForeignKeyName}" """,
        CreateIndex x => BuildCreateIndex(x),
        DropIndex x => $"""DROP INDEX "{x.SchemaName}"."{x.IndexName}" """,
        SetSchemaComment x => x.NewComment is null
            ? $"""COMMENT ON SCHEMA "{x.SchemaName}" IS NULL"""
            : $"""COMMENT ON SCHEMA "{x.SchemaName}" IS $comment${x.NewComment}$comment$""",
        SetTableComment x => x.NewComment is null
            ? $"""COMMENT ON TABLE "{x.SchemaName}"."{x.TableName}" IS NULL"""
            : $"""COMMENT ON TABLE "{x.SchemaName}"."{x.TableName}" IS $comment${x.NewComment}$comment$""",
        SetColumnComment x => x.NewComment is null
            ? $"""COMMENT ON COLUMN "{x.SchemaName}"."{x.TableName}"."{x.ColumnName}" IS NULL"""
            : $"""COMMENT ON COLUMN "{x.SchemaName}"."{x.TableName}"."{x.ColumnName}" IS $comment${x.NewComment}$comment$""",
        SetIndexComment x => x.NewComment is null
            ? $"""COMMENT ON INDEX "{x.SchemaName}"."{x.IndexName}" IS NULL"""
            : $"""COMMENT ON INDEX "{x.SchemaName}"."{x.IndexName}" IS $comment${x.NewComment}$comment$""",
        GrantSchemaUsage x => $"""GRANT USAGE ON SCHEMA "{x.SchemaName}" TO {x.Role}""",
        RevokeSchemaUsage x => $"""REVOKE USAGE ON SCHEMA "{x.SchemaName}" FROM {x.Role}""",
        GrantTablePrivileges x => $"""GRANT {PrivilegeList(x.Privileges)} ON TABLE "{x.SchemaName}"."{x.TableName}" TO {x.Role}""",
        RevokeTablePrivileges x => $"""REVOKE ALL PRIVILEGES ON TABLE "{x.SchemaName}"."{x.TableName}" FROM {x.Role}""",
        RunPreDeploymentScript x => x.Script.Sql,
        RunPostDeploymentScript x => x.Script.Sql,
        _ => throw new ArgumentOutOfRangeException(nameof(action), $"Unhandled action type: {action.GetType().Name}")
    };

    private static string BuildCreateTable(CreateTable x)
    {
        var parts = x.Table.Columns.Select(BuildColumnDef).ToList<string>();

        if (x.Table.PrimaryKey is { } pk)
            parts.Add($"""CONSTRAINT "{pk.Name}" PRIMARY KEY ({ColList(pk.ColumnNames)})""");

        return $"""
            CREATE TABLE "{x.SchemaName}"."{x.Table.Name}" (
                {string.Join(",\n    ", parts)}
            )
            """;
    }

    private static string BuildAddForeignKey(AddForeignKey x)
    {
        var fk = x.ForeignKey;
        string onDelete = ToReferentialAction(fk.OnDelete);
        string onUpdate = ToReferentialAction(fk.OnUpdate);
        return $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ADD CONSTRAINT "{fk.Name}" FOREIGN KEY ({ColList(fk.ColumnNames)}) REFERENCES "{fk.ReferencedSchema}"."{fk.ReferencedTable}" ({ColList(fk.ReferencedColumnNames)}) ON DELETE {onDelete} ON UPDATE {onUpdate}""";
    }

    private static string BuildCreateIndex(CreateIndex x)
    {
        var sql = $"""CREATE {(x.Index.IsUnique ? "UNIQUE " : "")}INDEX "{x.Index.Name}" ON "{x.SchemaName}"."{x.TableName}" ({ColList(x.Index.ColumnNames)})""";
        return x.Index.Predicate is { } pred ? $"{sql} WHERE {pred}" : sql;
    }

    private static string BuildColumnDef(Column col)
    {
        string type = ToPostgresType(col.Type);
        string nullable = col.IsNullable ? "" : " NOT NULL";
        string identity = col.IsIdentity ? BuildIdentityClause(col.IdentityOptions) : "";
        string def = col.DefaultExpression is { } d && !col.IsIdentity ? $" DEFAULT {d}" : "";
        return $"\"{col.Name}\" {type}{nullable}{identity}{def}";
    }

    private static string BuildIdentityClause(IdentityOptions? options)
    {
        if (options is null)
            return " GENERATED ALWAYS AS IDENTITY";
        var parts = new List<string>();
        if (options.MinValue.HasValue) parts.Add($"MINVALUE {options.MinValue}");
        if (options.StartWith.HasValue) parts.Add($"START WITH {options.StartWith}");
        if (options.IncrementBy.HasValue) parts.Add($"INCREMENT BY {options.IncrementBy}");
        return parts.Count > 0
            ? $" GENERATED ALWAYS AS IDENTITY ({string.Join(" ", parts)})"
            : " GENERATED ALWAYS AS IDENTITY";
    }

    private static string BuildAlterIdentitySequence(AlterIdentitySequence x)
    {
        var opts = x.NewOptions;
        var parts = new List<string>();
        if (opts?.MinValue.HasValue == true) parts.Add($"SET MINVALUE {opts.MinValue}");
        if (opts?.StartWith.HasValue == true) parts.Add($"SET START {opts.StartWith}");
        if (opts?.IncrementBy.HasValue == true) parts.Add($"SET INCREMENT BY {opts.IncrementBy}");
        parts.Add("RESTART");
        return $"""ALTER TABLE "{x.SchemaName}"."{x.TableName}" ALTER COLUMN "{x.ColumnName}" {string.Join(" ", parts)}""";
    }

    private static string ColList(IReadOnlyList<string> cols) =>
        string.Join(", ", cols.Select(c => $"\"{c}\""));

    private static string PrivilegeList(TablePrivilege privileges)
    {
        var parts = new List<string>();
        if (privileges.HasFlag(TablePrivilege.Select)) parts.Add("SELECT");
        if (privileges.HasFlag(TablePrivilege.Insert)) parts.Add("INSERT");
        if (privileges.HasFlag(TablePrivilege.Update)) parts.Add("UPDATE");
        if (privileges.HasFlag(TablePrivilege.Delete)) parts.Add("DELETE");
        return parts.Count > 0 ? string.Join(", ", parts) : "ALL PRIVILEGES";
    }

    // ── Type mapping ──────────────────────────────────────────────────────────

    internal static string ToPostgresType(SqlType type) => type switch
    {
        SqlType.BooleanType => "boolean",
        SqlType.TinyIntType => "smallint",
        SqlType.SmallIntType => "smallint",
        SqlType.IntType => "integer",
        SqlType.BigIntType => "bigint",
        SqlType.FloatType => "real",
        SqlType.DoubleType => "double precision",
        SqlType.DecimalType(var p, var s) => $"numeric({p}, {s})",
        SqlType.CharType(var n) => $"character({n})",
        SqlType.NCharType(var n) => $"character({n})",
        SqlType.VarCharType(null) => "character varying",
        SqlType.VarCharType(var n) => $"character varying({n})",
        SqlType.NVarCharType(null) => "character varying",
        SqlType.NVarCharType(var n) => $"character varying({n})",
        SqlType.TextType => "text",
        SqlType.DateType => "date",
        SqlType.TimeType => "time",
        SqlType.DateTimeType => "timestamp",
        SqlType.DateTimeOffsetType => "timestamptz",
        SqlType.GuidType => "uuid",
        SqlType.BinaryType => "bytea",
        SqlType.VarBinaryType => "bytea",
        SqlType.CustomType(var n) => n,
        _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unhandled SqlType: {type.GetType().Name}")
    };

    private static string ToReferentialAction(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE",
        ReferentialAction.SetNull => "SET NULL",
        ReferentialAction.SetDefault => "SET DEFAULT",
        _ => "NO ACTION",
    };
}
