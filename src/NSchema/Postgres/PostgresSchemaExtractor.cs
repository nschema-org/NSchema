using Npgsql;
using NSchema.Domain.Schema;
using NSchema.Extractors;
using NSchema.Postgres.Models;

namespace NSchema.Postgres;

public sealed class PostgresSchemaExtractor : ISchemaExtractor
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string[] _schemaNames;

    public PostgresSchemaExtractor(NpgsqlDataSource dataSource, params string[] schemaNames)
    {
        _dataSource = dataSource;
        _schemaNames = schemaNames;
    }

    public async Task<DatabaseModel> Extract(CancellationToken cancellationToken = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        var tables = await QueryTables(conn, cancellationToken);
        var columns = await QueryColumns(conn, cancellationToken);
        var primaryKeys = await QueryPrimaryKeys(conn, cancellationToken);
        var foreignKeys = await QueryForeignKeys(conn, cancellationToken);
        var indexes = await QueryIndexes(conn, cancellationToken);

        return Build(tables, columns, primaryKeys, foreignKeys, indexes);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    private async Task<List<TableRow>> QueryTables(NpgsqlConnection conn, CancellationToken ct)
    {
        var rows = new List<TableRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
            AND table_schema = ANY(@schemas)
            ORDER BY table_schema, table_name
            """;
        cmd.Parameters.AddWithValue("schemas", _schemaNames);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new TableRow(reader.GetString(0), reader.GetString(1)));

        return rows;
    }

    private async Task<List<ColumnRow>> QueryColumns(NpgsqlConnection conn, CancellationToken ct)
    {
        var rows = new List<ColumnRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                table_schema,
                table_name,
                column_name,
                data_type,
                udt_name,
                character_maximum_length,
                numeric_precision,
                numeric_scale,
                is_nullable,
                column_default,
                is_identity
            FROM information_schema.columns
            WHERE table_schema = ANY(@schemas)
            ORDER BY table_schema, table_name, ordinal_position
            """;
        cmd.Parameters.AddWithValue("schemas", _schemaNames);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ColumnRow(
                TableSchema: reader.GetString(0),
                TableName: reader.GetString(1),
                ColumnName: reader.GetString(2),
                DataType: reader.GetString(3),
                UdtName: reader.GetString(4),
                MaxLength: reader.IsDBNull(5) ? null : reader.GetInt32(5),
                NumericPrecision: reader.IsDBNull(6) ? null : reader.GetInt32(6),
                NumericScale: reader.IsDBNull(7) ? null : reader.GetInt32(7),
                IsNullable: reader.GetString(8) == "YES",
                DefaultExpression: reader.IsDBNull(9) ? null : reader.GetString(9),
                IsIdentity: reader.GetString(10) == "YES"
            ));
        }

        return rows;
    }

    private async Task<List<PrimaryKeyRow>> QueryPrimaryKeys(NpgsqlConnection conn, CancellationToken ct)
    {
        var rows = new List<PrimaryKeyRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                tc.table_schema,
                tc.table_name,
                tc.constraint_name,
                kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON  tc.constraint_name = kcu.constraint_name
                AND tc.table_schema    = kcu.table_schema
                AND tc.table_name      = kcu.table_name
            WHERE tc.constraint_type = 'PRIMARY KEY'
            AND tc.table_schema = ANY(@schemas)
            ORDER BY tc.table_schema, tc.table_name, kcu.ordinal_position
            """;
        cmd.Parameters.AddWithValue("schemas", _schemaNames);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new PrimaryKeyRow(
                TableSchema: reader.GetString(0),
                TableName: reader.GetString(1),
                ConstraintName: reader.GetString(2),
                ColumnName: reader.GetString(3)
            ));
        }

        return rows;
    }

    private async Task<List<ForeignKeyRow>> QueryForeignKeys(NpgsqlConnection conn, CancellationToken ct)
    {
        var rows = new List<ForeignKeyRow>();
        await using var cmd = conn.CreateCommand();
        // information_schema doesn't preserve FK column ordering, so use pg_catalog.
        // confupdtype / confdeltype are internal "char" — cast to text for reliable ADO reading.
        cmd.CommandText = """
            SELECT
                n.nspname  AS table_schema,
                t.relname  AS table_name,
                c.conname  AS constraint_name,
                array_agg(a.attname  ORDER BY array_position(c.conkey,  a.attnum))  AS column_names,
                fn.nspname AS foreign_schema,
                ft.relname AS foreign_table,
                array_agg(fa.attname ORDER BY array_position(c.confkey, fa.attnum)) AS foreign_column_names,
                c.confupdtype::text AS update_rule,
                c.confdeltype::text AS delete_rule
            FROM pg_constraint c
            JOIN pg_class     t  ON t.oid  = c.conrelid
            JOIN pg_namespace n  ON n.oid  = t.relnamespace
            JOIN pg_class     ft ON ft.oid = c.confrelid
            JOIN pg_namespace fn ON fn.oid = ft.relnamespace
            JOIN pg_attribute a  ON a.attrelid = t.oid  AND a.attnum = ANY(c.conkey)
            JOIN pg_attribute fa ON fa.attrelid = ft.oid AND fa.attnum = ANY(c.confkey)
            WHERE c.contype = 'f'
            AND n.nspname = ANY(@schemas)
            GROUP BY n.nspname, t.relname, c.conname, fn.nspname, ft.relname, c.confupdtype, c.confdeltype
            ORDER BY n.nspname, t.relname, c.conname
            """;
        cmd.Parameters.AddWithValue("schemas", _schemaNames);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ForeignKeyRow(
                TableSchema: reader.GetString(0),
                TableName: reader.GetString(1),
                ConstraintName: reader.GetString(2),
                ColumnNames: reader.GetFieldValue<string[]>(3),
                ForeignSchema: reader.GetString(4),
                ForeignTable: reader.GetString(5),
                ForeignColumnNames: reader.GetFieldValue<string[]>(6),
                UpdateRule: reader.GetString(7)[0],
                DeleteRule: reader.GetString(8)[0]
            ));
        }

        return rows;
    }

    private async Task<List<IndexRow>> QueryIndexes(NpgsqlConnection conn, CancellationToken ct)
    {
        var rows = new List<IndexRow>();
        await using var cmd = conn.CreateCommand();
        // Exclude primary-key indexes. Exclude expression indexes (attnum = 0).
        // Unique constraint indexes are included — they appear as TableIndex with IsUnique = true.
        cmd.CommandText = """
            SELECT
                n.nspname  AS schema_name,
                t.relname  AS table_name,
                i.relname  AS index_name,
                ix.indisunique AS is_unique,
                array_agg(a.attname ORDER BY k.ordinality) AS column_names
            FROM pg_index ix
            JOIN pg_class     t ON t.oid = ix.indrelid
            JOIN pg_class     i ON i.oid = ix.indexrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ordinality) ON TRUE
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE n.nspname = ANY(@schemas)
            AND NOT ix.indisprimary
            AND k.attnum > 0
            GROUP BY n.nspname, t.relname, i.relname, ix.indisunique
            ORDER BY n.nspname, t.relname, i.relname
            """;
        cmd.Parameters.AddWithValue("schemas", _schemaNames);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new IndexRow(
                SchemaName: reader.GetString(0),
                TableName: reader.GetString(1),
                IndexName: reader.GetString(2),
                IsUnique: reader.GetBoolean(3),
                ColumnNames: reader.GetFieldValue<string[]>(4)
            ));
        }

        return rows;
    }

    // ── Model assembly ────────────────────────────────────────────────────────

    private DatabaseModel Build(
        List<TableRow> tables,
        List<ColumnRow> columns,
        List<PrimaryKeyRow> primaryKeys,
        List<ForeignKeyRow> foreignKeys,
        List<IndexRow> indexes)
    {
        var bySchema = tables
            .GroupBy(t => t.Schema)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => BuildTable(t, columns, primaryKeys, foreignKeys, indexes)).ToList());

        // Ensure every requested schema name appears in the result, even when it has no tables.
        var schemas = _schemaNames
            .Select(name => new DatabaseSchema(
                name,
                bySchema.TryGetValue(name, out var schemaTables) ? schemaTables : []))
            .ToList();

        return new DatabaseModel(schemas);
    }

    private static Table BuildTable(
        TableRow tableRow,
        List<ColumnRow> allColumns,
        List<PrimaryKeyRow> allPrimaryKeys,
        List<ForeignKeyRow> allForeignKeys,
        List<IndexRow> allIndexes)
    {
        var cols = allColumns
            .Where(c => c.TableSchema == tableRow.Schema && c.TableName == tableRow.Name)
            .Select(MapColumn)
            .ToList();

        var pk = allPrimaryKeys
            .Where(pk => pk.TableSchema == tableRow.Schema && pk.TableName == tableRow.Name)
            .GroupBy(pk => pk.ConstraintName)
            .Select(g => new PrimaryKey(g.Key, g.Select(r => r.ColumnName).ToList()))
            .FirstOrDefault();

        var fks = allForeignKeys
            .Where(fk => fk.TableSchema == tableRow.Schema && fk.TableName == tableRow.Name)
            .Select(MapForeignKey)
            .ToList();

        var idxs = allIndexes
            .Where(i => i.SchemaName == tableRow.Schema && i.TableName == tableRow.Name)
            .Select(i => new TableIndex(i.IndexName, i.ColumnNames, i.IsUnique))
            .ToList();

        return new Table(
            tableRow.Name,
            cols,
            pk,
            fks.Count > 0 ? fks : null,
            idxs.Count > 0 ? idxs : null);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Column MapColumn(ColumnRow row)
    {
        var type = MapSqlType(row.DataType, row.UdtName, row.MaxLength, row.NumericPrecision, row.NumericScale);
        return new Column(
            row.ColumnName,
            type,
            IsNullable: row.IsNullable,
            IsIdentity: row.IsIdentity,
            DefaultExpression: row.IsIdentity ? null : row.DefaultExpression);
    }

    private static SqlType MapSqlType(
        string dataType, string udtName, int? maxLength, int? precision, int? scale) =>
        dataType switch
        {
            "boolean" => SqlType.Boolean,
            "smallint" => SqlType.SmallInt,
            "integer" => SqlType.Int,
            "bigint" => SqlType.BigInt,
            "real" => SqlType.Float,
            "double precision" => SqlType.Double,
            "numeric" => SqlType.Decimal(precision ?? 18, scale ?? 0),
            "character" => SqlType.Char(maxLength ?? 1),
            "character varying" => SqlType.VarChar(maxLength),
            "text" => SqlType.Text,
            "date" => SqlType.Date,
            "time without time zone" => SqlType.Time,
            "timestamp without time zone" => SqlType.DateTime,
            "timestamp with time zone" => SqlType.DateTimeOffset,
            "uuid" => SqlType.Guid,
            "bytea" => SqlType.VarBinary(),
            _ => SqlType.Custom(udtName),
        };

    private static ForeignKey MapForeignKey(ForeignKeyRow row) =>
        new(row.ConstraintName,
            row.ColumnNames,
            row.ForeignSchema,
            row.ForeignTable,
            row.ForeignColumnNames,
            OnDelete: MapReferentialAction(row.DeleteRule),
            OnUpdate: MapReferentialAction(row.UpdateRule));

    private static ReferentialAction MapReferentialAction(char code) => code switch
    {
        'c' => ReferentialAction.Cascade,
        'n' => ReferentialAction.SetNull,
        'd' => ReferentialAction.SetDefault,
        _ => ReferentialAction.NoAction, // 'a' = NO ACTION, 'r' = RESTRICT
    };
}
