using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class SchemaStateSerializerTests
{
    /// <summary>
    /// A schema exercising every domain feature, for round-trip coverage.
    /// </summary>
    private static DatabaseSchema RichSchema() => DatabaseSchema.Create(
        schemas:
        [
            SchemaDefinition.Create(
                name: "app",
                oldName: "legacy_app",
                isPartial: true,
                comment: "application schema",
                tables:
                [
                    Table.Create(
                        name: "users",
                        oldName: "members",
                        primaryKey: new PrimaryKey("users_pkey", ["id"]),
                        comment: "all users",
                        columns:
                        [
                            Column.Create("id", SqlType.BigInt, isIdentity: true,
                                identityOptions: new IdentityOptions(1, 1, 1)),
                            Column.Create("name", SqlType.VarChar(255), comment: "display name"),
                            Column.Create("balance", SqlType.Decimal(18, 2), isNullable: true, defaultExpression: "0"),
                            Column.Create("code", SqlType.Char(8), oldName: "short_code"),
                            Column.Create("metadata", SqlType.Custom("jsonb"), isNullable: true),
                        ],
                        foreignKeys:
                        [
                            ForeignKey.Create("users_org_fk", ["org_id"], "app", "orgs", ["id"],
                                ReferentialAction.Cascade, ReferentialAction.SetNull),
                        ],
                        indexes:
                        [
                            TableIndex.Create("users_name_ix", ["name"], isUnique: true,
                                comment: "unique names", predicate: "name IS NOT NULL"),
                        ],
                        grants: [new TableGrant("readers", TablePrivilege.All)]),
                ],
                droppedTables: ["old_table"],
                grants: [new SchemaGrant("app_role")]),
        ],
        droppedSchemas: ["scratch"]);

    private static readonly SqlType[] _sqlTypes =
    [
        SqlType.Boolean, SqlType.TinyInt, SqlType.SmallInt, SqlType.Int, SqlType.BigInt,
        SqlType.Float, SqlType.Double, SqlType.Text, SqlType.Date, SqlType.Time,
        SqlType.DateTime, SqlType.DateTimeOffset, SqlType.Guid,
        SqlType.Decimal(18, 2), SqlType.Char(8), SqlType.NChar(4), SqlType.Binary(16),
        SqlType.VarChar(255), SqlType.VarChar(), SqlType.NVarChar(64), SqlType.NVarChar(),
        SqlType.VarBinary(32), SqlType.VarBinary(), SqlType.Custom("jsonb"),
    ];

    public static TheoryData<SqlType> AllSqlTypes()
    {
        var data = new TheoryData<SqlType>();
        foreach (var type in _sqlTypes)
        {
            data.Add(type);
        }
        return data;
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsAllFeatures()
    {
        // Arrange
        var original = RichSchema();

        // Act: a read + write cycle must reproduce the exact same document.
        var json = SchemaStateSerializer.Serialize(original);
        var roundTripped = SchemaStateSerializer.Deserialize(json);

        // Assert
        SchemaStateSerializer.Serialize(roundTripped).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(AllSqlTypes))]
    public void RoundTrip_PreservesSqlType(SqlType type)
    {
        // Arrange
        var schema = DatabaseSchema.Create(
            [SchemaDefinition.Create("app", tables: [Table.Create("t", columns: [Column.Create("c", type)])])]);

        // Act
        var roundTripped = SchemaStateSerializer.Deserialize(SchemaStateSerializer.Serialize(schema));

        // Assert
        roundTripped.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(type);
    }

    [Fact]
    public void AllSqlTypes_CoversEveryConcreteSqlType()
    {
        // Every concrete SqlType in the domain must appear in AllSqlTypes, so RoundTrip_PreservesSqlType
        // exercises it. Declaring a new SqlType without adding it here will fail this test.
        var declared = typeof(SqlType).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SqlType)))
            .ToHashSet();

        var covered = _sqlTypes.Select(type => type.GetType()).ToHashSet();

        var missing = declared.Except(covered).Select(type => type.Name).Order().ToList();
        missing.ShouldBeEmpty($"AllSqlTypes is missing: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Serialize_MatchesSnapshot()
    {
        // The snapshot file pins the on-disk format. It's compared structurally (DeepEquals), so
        // whitespace and property order don't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, regenerate it via RefreshSnapshot and bump
        // SchemaStateEnvelope.CurrentVersion if the on-disk format itself changed.
        var path = SnapshotPath();
        File.Exists(path).ShouldBeTrue($"Snapshot not found at '{path}'. Run the RefreshSnapshot test to generate it.");

        var actual = SchemaStateSerializer.Serialize(RichSchema());
        var expected = File.ReadAllText(path);

        JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)).ShouldBeTrue(
            "Serialized output no longer matches the snapshot. If this change is intentional, regenerate it with " +
            "the RefreshSnapshot test (and bump SchemaStateEnvelope.CurrentVersion if the on-disk format changed).");
    }

    [Fact(Skip = "Enable locally to regenerate the serialization snapshot, then commit the updated file.")]
    public void RefreshSnapshot()
    {
        var path = SnapshotPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SchemaStateSerializer.Serialize(RichSchema()));
    }

    // Resolves the snapshot next to this source file, so RefreshSnapshot writes back to the
    // source-controlled copy. Reads from the source tree, which assumes tests run on the build machine.
    private static string SnapshotPath([CallerFilePath] string thisFilePath = "")
        => Path.Combine(Path.GetDirectoryName(thisFilePath)!, "Snapshots", "rich-schema.json");

    [Fact]
    public void Serialize_WritesEnumsAsNames()
    {
        // Arrange
        var schema = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("users", foreignKeys:
                [
                    ForeignKey.Create("fk", ["org_id"], "app", "orgs", ["id"], ReferentialAction.Cascade),
                ]),
            ]),
        ]);

        // Act
        var json = SchemaStateSerializer.Serialize(schema);

        // Assert: enums serialize as readable names, not integers.
        json.ShouldContain("\"Cascade\"");
        json.ShouldNotContain("\"onDelete\": 1");
    }

    [Fact]
    public void Deserialize_FutureFormatVersion_Throws()
    {
        // Arrange
        const string json =
            """
            { "version": 9999, "schema": { "schemas": [], "droppedSchemas": [] } }
            """;

        // Act
        var act = () => SchemaStateSerializer.Deserialize(json);

        // Assert
        act.ShouldThrow<NotSupportedException>();
    }
}
