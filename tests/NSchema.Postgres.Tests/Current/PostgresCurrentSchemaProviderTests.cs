using Npgsql;
using NSchema.Schema;
using NSchema.Postgres.Current;
using NSchema.Postgres.Tests.Fixtures;

namespace NSchema.Postgres.Tests.Current;

[Collection("postgres")]
public sealed class PostgresCurrentSchemaProviderTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private readonly NpgsqlDataSource _dataSource = fixture.DataSource;
    private readonly string _schema = $"test_{Guid.NewGuid():N}";
    private NpgsqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = await _dataSource.OpenConnectionAsync();
        await Exec($"CREATE SCHEMA \"{_schema}\"");
    }

    public async Task DisposeAsync()
    {
        await Exec($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
        await _connection.DisposeAsync();
    }

    private async Task Exec(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private PostgresCurrentSchemaProvider Extractor() => new(_dataSource);

    // ── Schema / table structure ──────────────────────────────────────────────

    [Fact]
    public async Task Extract_EmptySchema_ReturnsSchemaWithNoTables()
    {
        // Arrange
        // (schema created in InitializeAsync)

        // Act
        var model = await Extractor().GetSchema([_schema]);

        // Assert
        model.Schemas.ShouldHaveSingleItem();
        model.Schemas[0].Name.ShouldBe(_schema);
        model.Schemas[0].Tables.ShouldBeEmpty();
    }

    [Fact]
    public async Task Extract_SingleTable_ReturnsTable()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id    INTEGER NOT NULL,
                email TEXT    NOT NULL
            )
            """);

        // Act
        var model = await Extractor().GetSchema([_schema]);

        // Assert
        model.Schemas[0].Tables.ShouldHaveSingleItem();
        model.Schemas[0].Tables[0].Name.ShouldBe("users");
    }

    // ── Nullability ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Extract_Columns_NullabilityMappedCorrectly()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id    INTEGER NOT NULL,
                email TEXT
            )
            """);

        // Act
        var cols = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Columns.ToDictionary(c => c.Name);

        // Assert
        cols["id"].IsNullable.ShouldBeFalse();
        cols["email"].IsNullable.ShouldBeTrue();
    }

    // ── Type mapping ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Extract_Columns_StandardTypesMappedCorrectly()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".types_test (
                col_bool     BOOLEAN,
                col_smallint SMALLINT,
                col_int      INTEGER,
                col_bigint   BIGINT,
                col_real     REAL,
                col_double   DOUBLE PRECISION,
                col_numeric  NUMERIC(10, 3),
                col_char     CHAR(5),
                col_varchar  VARCHAR(100),
                col_text     TEXT,
                col_date     DATE,
                col_time     TIME,
                col_ts       TIMESTAMP,
                col_tstz     TIMESTAMPTZ,
                col_uuid     UUID,
                col_bytea    BYTEA
            )
            """);

        // Act
        var cols = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Columns.ToDictionary(c => c.Name);

        // Assert
        cols["col_bool"].Type.ShouldBe(SqlType.Boolean);
        cols["col_smallint"].Type.ShouldBe(SqlType.SmallInt);
        cols["col_int"].Type.ShouldBe(SqlType.Int);
        cols["col_bigint"].Type.ShouldBe(SqlType.BigInt);
        cols["col_real"].Type.ShouldBe(SqlType.Float);
        cols["col_double"].Type.ShouldBe(SqlType.Double);
        cols["col_numeric"].Type.ShouldBe(SqlType.Decimal(10, 3));
        cols["col_char"].Type.ShouldBe(SqlType.Char(5));
        cols["col_varchar"].Type.ShouldBe(SqlType.VarChar(100));
        cols["col_text"].Type.ShouldBe(SqlType.Text);
        cols["col_date"].Type.ShouldBe(SqlType.Date);
        cols["col_time"].Type.ShouldBe(SqlType.Time);
        cols["col_ts"].Type.ShouldBe(SqlType.DateTime);
        cols["col_tstz"].Type.ShouldBe(SqlType.DateTimeOffset);
        cols["col_uuid"].Type.ShouldBe(SqlType.Guid);
        cols["col_bytea"].Type.ShouldBe(SqlType.VarBinary());
    }

    [Fact]
    public async Task Extract_CustomType_MapsToCustomSqlType()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id    INTEGER NOT NULL,
                email CITEXT  NOT NULL
            )
            """);

        // Act
        var emailCol = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Columns.Single(c => c.Name == "email");

        // Assert
        emailCol.Type.ShouldBe(SqlType.Custom("citext"));
    }

    // ── Identity & defaults ───────────────────────────────────────────────────

    [Fact]
    public async Task Extract_IdentityColumn_SetsIsIdentityAndClearsDefault()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id    INTEGER GENERATED ALWAYS AS IDENTITY,
                email TEXT NOT NULL
            )
            """);

        // Act
        var idCol = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Columns.Single(c => c.Name == "id");

        // Assert
        idCol.IsIdentity.ShouldBeTrue();
        idCol.DefaultExpression.ShouldBeNull();
    }

    [Fact]
    public async Task Extract_ColumnDefault_CapturesExpression()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id     INTEGER NOT NULL,
                status TEXT DEFAULT 'active'
            )
            """);

        // Act
        var statusCol = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Columns.Single(c => c.Name == "status");

        // Assert
        statusCol.DefaultExpression.ShouldNotBeNull();
        statusCol.DefaultExpression!.ShouldContain("active");
    }

    // ── Primary key ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Extract_PrimaryKey_ReturnsPrimaryKey()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id INTEGER NOT NULL,
                CONSTRAINT pk_users PRIMARY KEY (id)
            )
            """);

        // Act
        var table = (await Extractor().GetSchema([_schema])).Schemas[0].Tables[0];

        // Assert
        table.PrimaryKey.ShouldNotBeNull();
        table.PrimaryKey!.Name.ShouldBe("pk_users");
        table.PrimaryKey.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public async Task Extract_CompositePrimaryKey_ReturnsColumnsInOrder()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".order_items (
                order_id INTEGER NOT NULL,
                item_id  INTEGER NOT NULL,
                CONSTRAINT pk_order_items PRIMARY KEY (order_id, item_id)
            )
            """);

        // Act
        var pk = (await Extractor().GetSchema([_schema])).Schemas[0].Tables[0].PrimaryKey;

        // Assert
        pk.ShouldNotBeNull();
        pk!.ColumnNames.ShouldBe(["order_id", "item_id"]);
    }

    [Fact]
    public async Task Extract_TableWithNoPrimaryKey_ReturnsNullPrimaryKey()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".events (
                name TEXT NOT NULL
            )
            """);

        // Act
        var table = (await Extractor().GetSchema([_schema])).Schemas[0].Tables[0];

        // Assert
        table.PrimaryKey.ShouldBeNull();
    }

    // ── Foreign keys ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Extract_ForeignKey_ReturnsConstraint()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".organisations (
                id INTEGER NOT NULL CONSTRAINT pk_orgs PRIMARY KEY
            );
            CREATE TABLE "{_schema}".users (
                id     INTEGER NOT NULL CONSTRAINT pk_users PRIMARY KEY,
                org_id INTEGER NOT NULL,
                CONSTRAINT fk_users_org FOREIGN KEY (org_id)
                    REFERENCES "{_schema}".organisations (id)
            )
            """);

        // Act
        var fks = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables.Single(t => t.Name == "users").ForeignKeys;

        // Assert
        fks.ShouldNotBeNull();
        fks!.ShouldHaveSingleItem();
        fks[0].Name.ShouldBe("fk_users_org");
        fks[0].ColumnNames.ShouldBe(["org_id"]);
        fks[0].ReferencedSchema.ShouldBe(_schema);
        fks[0].ReferencedTable.ShouldBe("organisations");
        fks[0].ReferencedColumnNames.ShouldBe(["id"]);
        fks[0].OnDelete.ShouldBe(ReferentialAction.NoAction);
        fks[0].OnUpdate.ShouldBe(ReferentialAction.NoAction);
    }

    [Fact]
    public async Task Extract_ForeignKeyOnDelete_MapsReferentialAction()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".organisations (
                id INTEGER NOT NULL CONSTRAINT pk_orgs PRIMARY KEY
            );
            CREATE TABLE "{_schema}".users (
                id     INTEGER NOT NULL CONSTRAINT pk_users PRIMARY KEY,
                org_id INTEGER NOT NULL,
                CONSTRAINT fk_users_org FOREIGN KEY (org_id)
                    REFERENCES "{_schema}".organisations (id)
                    ON DELETE CASCADE
                    ON UPDATE SET NULL
            )
            """);

        // Act
        var fk = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables.Single(t => t.Name == "users").ForeignKeys![0];

        // Assert
        fk.OnDelete.ShouldBe(ReferentialAction.Cascade);
        fk.OnUpdate.ShouldBe(ReferentialAction.SetNull);
    }

    [Fact]
    public async Task Extract_TableWithNoForeignKeys_ReturnsNullForeignKeys()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".standalone (
                id INTEGER NOT NULL
            )
            """);

        // Act
        var table = (await Extractor().GetSchema([_schema])).Schemas[0].Tables[0];

        // Assert
        table.ForeignKeys.ShouldBeNull();
    }

    // ── Indexes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Extract_Index_ReturnsIndex()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id    INTEGER NOT NULL,
                email TEXT    NOT NULL
            );
            CREATE INDEX ix_users_email ON "{_schema}".users (email)
            """);

        // Act
        var idx = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Indexes!.Single();

        // Assert
        idx.Name.ShouldBe("ix_users_email");
        idx.ColumnNames.ShouldBe(["email"]);
        idx.IsUnique.ShouldBeFalse();
    }

    [Fact]
    public async Task Extract_UniqueIndex_SetsIsUniqueTrue()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id    INTEGER NOT NULL,
                email TEXT    NOT NULL
            );
            CREATE UNIQUE INDEX ix_users_email ON "{_schema}".users (email)
            """);

        // Act
        var idx = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Indexes!.Single();

        // Assert
        idx.IsUnique.ShouldBeTrue();
    }

    [Fact]
    public async Task Extract_CompositeIndex_ReturnsColumnsInOrder()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".events (
                id       INTEGER   NOT NULL,
                user_id  INTEGER   NOT NULL,
                happened TIMESTAMP NOT NULL
            );
            CREATE INDEX ix_events_user_time ON "{_schema}".events (user_id, happened)
            """);

        // Act
        var idx = (await Extractor().GetSchema([_schema]))
            .Schemas[0].Tables[0].Indexes!.Single();

        // Assert
        idx.ColumnNames.ShouldBe(["user_id", "happened"]);
    }

    [Fact]
    public async Task Extract_PrimaryKeyIndex_IsNotReturnedAsTableIndex()
    {
        // Arrange
        await Exec($"""
            CREATE TABLE "{_schema}".users (
                id INTEGER NOT NULL CONSTRAINT pk_users PRIMARY KEY
            )
            """);

        // Act
        var table = (await Extractor().GetSchema([_schema])).Schemas[0].Tables[0];

        // Assert
        table.Indexes.ShouldBeNull();
    }
}
