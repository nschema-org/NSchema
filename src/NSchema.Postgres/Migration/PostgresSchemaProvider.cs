using Npgsql;
using NSchema.Migration;
using NSchema.Postgres.Models;
using NSchema.Schema;

namespace NSchema.Postgres.Migration;

internal sealed class PostgresSchemaProvider(NpgsqlDataSource dataSource) : ICurrentSchemaProvider
{
    public async Task<DatabaseSchema> GetSchema(string[] schemas, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        var tables = await QueryTables(conn, schemas, cancellationToken);
        var columns = await QueryColumns(conn, schemas, cancellationToken);
        var primaryKeys = await QueryPrimaryKeys(conn, schemas, cancellationToken);
        var foreignKeys = await QueryForeignKeys(conn, schemas, cancellationToken);
        var indexes = await QueryIndexes(conn, schemas, cancellationToken);
        var schemaComments = await QuerySchemaComments(conn, schemas, cancellationToken);
        var tableComments = await QueryTableComments(conn, schemas, cancellationToken);
        var columnComments = await QueryColumnComments(conn, schemas, cancellationToken);
        var indexComments = await QueryIndexComments(conn, schemas, cancellationToken);
        var schemaGrants = await QuerySchemaGrants(conn, schemas, cancellationToken);
        var tableGrants = await QueryTableGrants(conn, schemas, cancellationToken);

        return Build(
            tables, columns, primaryKeys, foreignKeys, indexes,
            schemaComments, tableComments, columnComments, indexComments,
            schemaGrants, tableGrants
        );
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    private static async Task<List<TableRow>> QueryTables(NpgsqlConnection conn, string[] schemes, CancellationToken ct)
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
        cmd.Parameters.AddWithValue("schemas", schemes);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new TableRow(reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<List<ColumnRow>> QueryColumns(NpgsqlConnection conn, string[] schemes, CancellationToken ct)
    {
        var rows = new List<ColumnRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                c.table_schema,
                c.table_name,
                c.column_name,
                c.data_type,
                c.udt_name,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale,
                c.is_nullable,
                c.column_default,
                c.is_identity,
                seq.seqstart   AS identity_start,
                seq.seqmin     AS identity_min_value,
                seq.seqincrement AS identity_increment
            FROM information_schema.columns c
            LEFT JOIN pg_class        t  ON t.relname   = c.table_name
                                        AND t.relkind   = 'r'
            LEFT JOIN pg_namespace    n  ON n.oid        = t.relnamespace
                                        AND n.nspname   = c.table_schema
            LEFT JOIN pg_attribute    a  ON a.attrelid  = t.oid
                                        AND a.attname   = c.column_name
            LEFT JOIN pg_depend       d  ON d.refobjid  = t.oid
                                        AND d.refobjsubid = a.attnum
                                        AND d.deptype   = 'i'
            LEFT JOIN pg_class        sc ON sc.oid       = d.objid
                                        AND sc.relkind  = 'S'
            LEFT JOIN pg_sequence     seq ON seq.seqrelid = sc.oid
            WHERE c.table_schema = ANY(@schemas)
            ORDER BY c.table_schema, c.table_name, c.ordinal_position
            """;
        cmd.Parameters.AddWithValue("schemas", schemes);

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
                IsIdentity: reader.GetString(10) == "YES",
                IdentityStart: reader.IsDBNull(11) ? null : reader.GetInt64(11),
                IdentityMinValue: reader.IsDBNull(12) ? null : reader.GetInt64(12),
                IdentityIncrement: reader.IsDBNull(13) ? null : reader.GetInt64(13)
            ));
        }

        return rows;
    }

    private static async Task<List<PrimaryKeyRow>> QueryPrimaryKeys(NpgsqlConnection conn, string[] schemes, CancellationToken ct)
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
        cmd.Parameters.AddWithValue("schemas", schemes);

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

    private static async Task<List<ForeignKeyRow>> QueryForeignKeys(NpgsqlConnection conn, string[] schemes, CancellationToken ct)
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
        cmd.Parameters.AddWithValue("schemas", schemes);

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

    private static async Task<List<IndexRow>> QueryIndexes(NpgsqlConnection conn, string[] schemes, CancellationToken ct)
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
                array_agg(a.attname ORDER BY k.ordinality) AS column_names,
                pg_get_expr(ix.indpred, ix.indrelid) AS predicate
            FROM pg_index ix
            JOIN pg_class     t ON t.oid = ix.indrelid
            JOIN pg_class     i ON i.oid = ix.indexrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ordinality) ON TRUE
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE n.nspname = ANY(@schemas)
            AND NOT ix.indisprimary
            AND k.attnum > 0
            GROUP BY n.nspname, t.relname, i.relname, ix.indisunique, ix.indpred, ix.indrelid
            ORDER BY n.nspname, t.relname, i.relname
            """;
        cmd.Parameters.AddWithValue("schemas", schemes);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new IndexRow(
                SchemaName: reader.GetString(0),
                TableName: reader.GetString(1),
                IndexName: reader.GetString(2),
                IsUnique: reader.GetBoolean(3),
                ColumnNames: reader.GetFieldValue<string[]>(4),
                Predicate: reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return rows;
    }

    private static async Task<Dictionary<string, string?>> QuerySchemaComments(NpgsqlConnection conn, string[] schemas, CancellationToken ct)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.nspname, d.description
            FROM pg_namespace n
            LEFT JOIN pg_description d
                ON d.objoid = n.oid
                AND d.classoid = 'pg_namespace'::regclass
                AND d.objsubid = 0
            WHERE n.nspname = ANY(@schemas)
            """;
        cmd.Parameters.AddWithValue("schemas", schemas);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        return result;
    }

    private static async Task<Dictionary<(string, string), string?>> QueryTableComments(NpgsqlConnection conn, string[] schemas, CancellationToken ct)
    {
        var result = new Dictionary<(string, string), string?>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.nspname, c.relname, d.description
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_description d
                ON d.objoid = c.oid
                AND d.classoid = 'pg_class'::regclass
                AND d.objsubid = 0
            WHERE n.nspname = ANY(@schemas)
            AND c.relkind = 'r'
            ORDER BY n.nspname, c.relname
            """;
        cmd.Parameters.AddWithValue("schemas", schemas);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[(reader.GetString(0), reader.GetString(1))] = reader.IsDBNull(2) ? null : reader.GetString(2);
        }

        return result;
    }

    private static async Task<Dictionary<(string, string, string), string?>> QueryColumnComments(NpgsqlConnection conn, string[] schemas, CancellationToken ct)
    {
        var result = new Dictionary<(string, string, string), string?>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.nspname, c.relname, a.attname, d.description
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
            LEFT JOIN pg_description d
                ON d.objoid = c.oid
                AND d.classoid = 'pg_class'::regclass
                AND d.objsubid = a.attnum
            WHERE n.nspname = ANY(@schemas)
            AND c.relkind = 'r'
            ORDER BY n.nspname, c.relname, a.attnum
            """;
        cmd.Parameters.AddWithValue("schemas", schemas);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[(reader.GetString(0), reader.GetString(1), reader.GetString(2))] = reader.IsDBNull(3) ? null : reader.GetString(3);
        }

        return result;
    }

    private static async Task<Dictionary<(string, string), string?>> QueryIndexComments(NpgsqlConnection conn, string[] schemas, CancellationToken ct)
    {
        var result = new Dictionary<(string, string), string?>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.nspname, i.relname, d.description
            FROM pg_class i
            JOIN pg_index ix ON ix.indexrelid = i.oid
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            LEFT JOIN pg_description d
                ON d.objoid = i.oid
                AND d.classoid = 'pg_class'::regclass
                AND d.objsubid = 0
            WHERE n.nspname = ANY(@schemas)
            AND NOT ix.indisprimary
            ORDER BY n.nspname, i.relname
            """;
        cmd.Parameters.AddWithValue("schemas", schemas);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[(reader.GetString(0), reader.GetString(1))] = reader.IsDBNull(2) ? null : reader.GetString(2);
        }

        return result;
    }

    private static async Task<List<SchemaGrantRow>> QuerySchemaGrants(NpgsqlConnection conn, string[] schemas, CancellationToken ct)
    {
        var rows = new List<SchemaGrantRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.nspname AS schema_name,
                   acl.grantee::regrole::text AS role
            FROM pg_namespace n
            CROSS JOIN LATERAL aclexplode(n.nspacl) AS acl(grantor, grantee, privilege_type, is_grantable)
            WHERE n.nspname = ANY(@schemas)
            AND acl.privilege_type = 'USAGE'
            AND acl.grantee != 0
            """;
        cmd.Parameters.AddWithValue("schemas", schemas);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new SchemaGrantRow(reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<List<TableGrantRow>> QueryTableGrants(NpgsqlConnection conn, string[] schemas, CancellationToken ct)
    {
        var rows = new List<TableGrantRow>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_schema, table_name, grantee, privilege_type
            FROM information_schema.role_table_grants
            WHERE table_schema = ANY(@schemas)
            AND privilege_type IN ('SELECT', 'INSERT', 'UPDATE', 'DELETE')
            AND grantee != 'PUBLIC'
            ORDER BY table_schema, table_name, grantee, privilege_type
            """;
        cmd.Parameters.AddWithValue("schemas", schemas);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new TableGrantRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    // ── Model assembly ────────────────────────────────────────────────────────

    private static DatabaseSchema Build(
        List<TableRow> tables,
        List<ColumnRow> columns,
        List<PrimaryKeyRow> primaryKeys,
        List<ForeignKeyRow> foreignKeys,
        List<IndexRow> indexes,
        Dictionary<string, string?> schemaComments,
        Dictionary<(string, string), string?> tableComments,
        Dictionary<(string, string, string), string?> columnComments,
        Dictionary<(string, string), string?> indexComments,
        List<SchemaGrantRow> schemaGrants,
        List<TableGrantRow> tableGrants
    )
    {
        var bySchema = tables
            .GroupBy(t => t.Schema)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => BuildTable(t, columns, primaryKeys, foreignKeys, indexes, tableComments, columnComments, indexComments, tableGrants)).ToList());

        // Drive schema list from what actually exists in the database, not from what was requested.
        var existingSchemas = schemaComments.Keys
            .Union(bySchema.Keys)
            .Union(schemaGrants.Select(g => g.SchemaName))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var dbSchemas = existingSchemas
            .Select(name =>
            {
                var grants = schemaGrants
                    .Where(g => g.SchemaName == name)
                    .Select(g => new SchemaGrant(g.Role))
                    .ToList();
                return new SchemaDefinition(name, null, false, schemaComments.GetValueOrDefault(name), bySchema.GetValueOrDefault(name, []), [], grants);
            })
            .ToList();

        return new DatabaseSchema(dbSchemas, []);
    }

    private static Table BuildTable(
        TableRow tableRow,
        List<ColumnRow> allColumns,
        List<PrimaryKeyRow> allPrimaryKeys,
        List<ForeignKeyRow> allForeignKeys,
        List<IndexRow> allIndexes,
        Dictionary<(string, string), string?> tableComments,
        Dictionary<(string, string, string), string?> columnComments,
        Dictionary<(string, string), string?> indexComments,
        List<TableGrantRow> allTableGrants
    )
    {
        var cols = allColumns
            .Where(c => c.TableSchema == tableRow.Schema && c.TableName == tableRow.Name)
            .Select(c => MapColumn(c, columnComments))
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
            .Select(i => new TableIndex(
                i.IndexName, i.ColumnNames, i.IsUnique,
                indexComments.GetValueOrDefault((tableRow.Schema, i.IndexName)),
                i.Predicate))
            .ToList();

        tableComments.TryGetValue((tableRow.Schema, tableRow.Name), out var tableComment);

        var grants = allTableGrants
            .Where(g => g.SchemaName == tableRow.Schema && g.TableName == tableRow.Name)
            .GroupBy(g => g.Role)
            .Select(g => new TableGrant(g.Key, ToTablePrivilege(g.Select(r => r.Privilege))))
            .ToList();

        return new Table(tableRow.Name, null, pk, tableComment, cols, fks, idxs, grants);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Column MapColumn(ColumnRow row, Dictionary<(string, string, string), string?> columnComments)
    {
        var type = MapSqlType(row.DataType, row.UdtName, row.MaxLength, row.NumericPrecision, row.NumericScale);
        columnComments.TryGetValue((row.TableSchema, row.TableName, row.ColumnName), out var comment);
        IdentityOptions? identityOptions = row.IsIdentity
            ? new IdentityOptions(row.IdentityStart, row.IdentityMinValue, row.IdentityIncrement)
            : null;
        return new Column(row.ColumnName, type, row.IsNullable, row.IsIdentity, row.DefaultExpression, null, comment, identityOptions);
    }

    private static SqlType MapSqlType(string dataType, string udtName, int? maxLength, int? precision, int? scale) => dataType switch
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

    private static TablePrivilege ToTablePrivilege(IEnumerable<string> privileges)
    {
        return privileges.Aggregate(TablePrivilege.None, (current, p) => current | p switch
        {
            "SELECT" => TablePrivilege.Select,
            "INSERT" => TablePrivilege.Insert,
            "UPDATE" => TablePrivilege.Update,
            "DELETE" => TablePrivilege.Delete,
            _ => TablePrivilege.None,
        });
    }

    private static ForeignKey MapForeignKey(ForeignKeyRow row) => new(
        row.ConstraintName,
        row.ColumnNames,
        row.ForeignSchema,
        row.ForeignTable,
        row.ForeignColumnNames,
        MapReferentialAction(row.DeleteRule),
        MapReferentialAction(row.UpdateRule)
    );

    private static ReferentialAction MapReferentialAction(char code) => code switch
    {
        'c' => ReferentialAction.Cascade,
        'n' => ReferentialAction.SetNull,
        'd' => ReferentialAction.SetDefault,
        _ => ReferentialAction.NoAction, // 'a' = NO ACTION, 'r' = RESTRICT
    };
}
