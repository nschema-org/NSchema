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

    public static TheoryData<SqlType> AllSqlTypes() =>
    [
        SqlType.Boolean, SqlType.TinyInt, SqlType.SmallInt, SqlType.Int, SqlType.BigInt,
        SqlType.Float, SqlType.Double, SqlType.Text, SqlType.Date, SqlType.Time,
        SqlType.DateTime, SqlType.DateTimeOffset, SqlType.Guid,
        SqlType.Decimal(18, 2), SqlType.Char(8), SqlType.NChar(4), SqlType.Binary(16),
        SqlType.VarChar(255), SqlType.VarChar(), SqlType.NVarChar(64), SqlType.NVarChar(),
        SqlType.VarBinary(32), SqlType.VarBinary(), SqlType.Custom("jsonb"),
    ];

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
    public void Serialize_ProducesExpectedFormat()
    {
        // Arrange: a golden snapshot. Compared structurally (DeepEquals), so whitespace and property
        // order don't matter — but a domain refactor that changes the on-disk shape will fail here,
        // which is the signal to bump SchemaStateEnvelope.CurrentVersion deliberately.
        var schema = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("users",
                    primaryKey: new PrimaryKey("users_pkey", ["id"]),
                    columns:
                    [
                        Column.Create("id", SqlType.Int, isIdentity: true),
                        Column.Create("balance", SqlType.Decimal(18, 2), isNullable: true),
                    ],
                    indexes: [TableIndex.Create("ix_users_balance", ["balance"])]),
            ]),
        ]);

        const string expected =
            """
            {
              "stateFormatVersion": 1,
              "schema": {
                "schemas": [
                  {
                    "name": "app",
                    "isPartial": false,
                    "tables": [
                      {
                        "name": "users",
                        "primaryKey": { "name": "users_pkey", "columnNames": ["id"] },
                        "columns": [
                          { "name": "id", "type": { "kind": "int" }, "isNullable": false, "isIdentity": true },
                          { "name": "balance", "type": { "kind": "decimal", "precision": 18, "scale": 2 }, "isNullable": true, "isIdentity": false }
                        ],
                        "foreignKeys": [],
                        "indexes": [
                          { "name": "ix_users_balance", "columnNames": ["balance"], "isUnique": false }
                        ],
                        "grants": []
                      }
                    ],
                    "droppedTables": [],
                    "grants": []
                  }
                ],
                "droppedSchemas": []
              }
            }
            """;

        // Act
        var actual = SchemaStateSerializer.Serialize(schema);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)).ShouldBeTrue();
    }

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
            { "stateFormatVersion": 9999, "schema": { "schemas": [], "droppedSchemas": [] } }
            """;

        // Act
        var act = () => SchemaStateSerializer.Deserialize(json);

        // Assert
        act.ShouldThrow<NotSupportedException>();
    }
}
