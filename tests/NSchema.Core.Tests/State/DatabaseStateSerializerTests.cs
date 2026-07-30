using System.Text;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.State;
using NSchema.State.Domain;

namespace NSchema.Tests.State;

public sealed class DatabaseStateSerializerTests
{
    private static readonly IDatabaseStateSerializer _sut = new DatabaseStateSerializer();

    private static string Json(Database schema) => Encoding.UTF8.GetString(_sut.Serialize(new DatabaseState(schema)).Span);

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsAllFeatures()
    {
        // Arrange
        var original = TestData.RichSchema();

        // Act: a read + write cycle must reproduce the exact same document.
        var json = Json(original);
        var roundTripped = _sut.Deserialize(_sut.Serialize(new DatabaseState(original))).Database;

        // Assert
        Json(roundTripped).ShouldBe(json);
    }

    [Theory]
    [MemberData(nameof(SqlTypeHelpers.AllShapes), MemberType = typeof(SqlTypeHelpers))]
    public void RoundTrip_PreservesSqlType(SqlType type)
    {
        // Arrange
        var schema = new Database
        {
            Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "t", Columns = [new Column { Name = "c", Type = type }] }] }],
        };

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(new DatabaseState(schema))).Database;

        // Assert
        roundTripped.Schemas[0].Tables[0].Columns[0].Type.ShouldBe(type);
    }

    [Fact]
    public Task Serialize_MatchesSnapshot()
        // The verified snapshot pins the on-disk format. VerifyJson reformats both sides consistently,
        // so whitespace and indentation don't matter — but a domain change that alters the serialized
        // shape fails here. When that's intentional, accept the .received.txt as the new baseline (and
        // bump DatabaseStateEnvelope.CurrentVersion if the on-disk format itself changed).
        => VerifyJson(Json(TestData.RichSchema()));

    [Fact]
    public void Serialize_WritesEnumsAsNames()
    {
        // Arrange
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "app", Tables = [
                new Table { Name = "users", ForeignKeys = [
                    new ForeignKey { Name = "fk", ColumnNames = ["org_id"], References = new ObjectAddress("app", "orgs"), ReferencedColumnNames = ["id"], OnDelete = ReferentialAction.Cascade },
                ] },
            ] },
        ],
        };

        // Act
        var json = Json(schema);

        // Assert: enums serialize as readable names, not integers.
        json.ShouldContain("\"Cascade\"");
        json.ShouldNotContain("\"onDelete\": 1");
    }

    [Fact]
    public void Serialize_WritesDefaultAndNullMembers()
    {
        // The state store is a fact store: a member at its default value must still be recorded, so that
        // "absent" can never be mistaken for "present and equal to today's default". This is the inverse
        // of the user-facing serializer, which omits defaults. See DomainModelSerializationContractTests.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "app", Tables = [
                new Table { Name = "t", Columns = [new Column { Name = "c", Type = SqlType.Int }] },
            ] },
        ],
        };

        // Act
        var json = Json(schema);

        // Assert: false bools and null strings are written, not omitted.
        json.ShouldContain("\"isNullable\": false");
        json.ShouldContain("\"isIdentity\": false");
        json.ShouldContain("\"defaultExpression\": null");
    }

    [Fact]
    public void RoundTrip_PreservesExecutedScripts()
    {
        // Arrange
        var executed = new ScriptExecution(new ScriptReference(null, "api-login"), "abc123", new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero));
        var state = new DatabaseState(new Database { Schemas = [new Schema { Name = "app" }] }, [executed]);

        // Act
        var roundTripped = _sut.Deserialize(_sut.Serialize(state));

        // Assert
        roundTripped.Scripts.ShouldHaveSingleItem().ShouldBe(executed);
    }

    [Fact]
    public void Serialize_WritesTheLedgerAsScripts()
    {
        // Arrange
        // Pins the wire field name — renaming it silently empties every existing ledger.
        var state = new DatabaseState(
            new Database { Schemas = [] },
            [new ScriptExecution(new ScriptReference(null, "api-login"), "abc123", DateTimeOffset.UnixEpoch)]);

        // Act
        var json = Encoding.UTF8.GetString(_sut.Serialize(state).Span);

        // Assert
        json.ShouldContain("\"scripts\"");
    }

    [Fact]
    public Task Serialize_Ledger_MatchesSnapshot()
        // Pins the ledger entry's wire shape: the script address is structural ({schema, name}, schema null
        // when the script is global), beside the hash and timestamp.
        => VerifyJson(Encoding.UTF8.GetString(_sut.Serialize(new DatabaseState(new Database { Schemas = [] }, [
            new ScriptExecution(new ScriptReference(null, "api-login"), "abc123", DateTimeOffset.UnixEpoch),
            new ScriptExecution(new ScriptReference("sales", "seed"), "def456", DateTimeOffset.UnixEpoch),
        ])).Span));

    [Fact]
    public Task Serialize_ManagedSet_MatchesSnapshot()
        // Pins the managed set's wire shape: schema and extension names beside object identities, each an
        // enum-named kind and a structural address.
        => VerifyJson(Encoding.UTF8.GetString(_sut.Serialize(new DatabaseState(new Database { Schemas = [] })
        {
            Managed = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema("app"), DatabaseAddress.Extension("citext")],
                SchemaObjects: [new ObjectAddress("app", "users") with { Kind = SchemaObjectKind.Table }]),
        }).Span));

    [Fact]
    public Task Serialize_ManagedSetAcrossEveryLevel_MatchesSnapshot()
        // Pins the managed set at all three levels and across object kinds — including names a dotted path
        // could not carry unquoted, which is why each address is structural on the wire.
        => VerifyJson(Encoding.UTF8.GetString(_sut.Serialize(new DatabaseState(new Database { Schemas = [] })
        {
            Managed = new IdentitySet(
                DatabaseObjects: [DatabaseAddress.Schema("app"), DatabaseAddress.Schema("my.schema"), DatabaseAddress.Extension("citext"), DatabaseAddress.Extension("uuid-ossp")],
                SchemaObjects:
                [
                    new ObjectAddress("app", "users", SchemaObjectKind.Table),
                    new ObjectAddress("app", "active_users", SchemaObjectKind.View),
                    new ObjectAddress("app", "user_id_seq", SchemaObjectKind.Sequence),
                    new ObjectAddress("my.schema", "Order Details", SchemaObjectKind.Table),
                ]),
        }).Span));

    [Fact]
    public void RoundTrip_PreservesTheManagedSet()
    {
        // Arrange — every level, including names that would need quoting if written as a path.
        var managed = new IdentitySet(
            DatabaseObjects: [DatabaseAddress.Schema("app"), DatabaseAddress.Schema("my.schema"), DatabaseAddress.Extension("citext")],
            SchemaObjects:
            [
                new ObjectAddress("app", "users", SchemaObjectKind.Table),
                new ObjectAddress("my.schema", "Order Details", SchemaObjectKind.View),
            ]);

        // Act
        var restored = _sut
            .Deserialize(_sut.Serialize(new DatabaseState(new Database { Schemas = [] }) { Managed = managed }))
            .Managed;

        // Assert — kinds and hostile names survive the wire.
        restored.DatabaseObjects.ShouldBe(managed.DatabaseObjects);
        restored.SchemaObjects.ShouldBe(managed.SchemaObjects);
    }

    [Fact]
    public void Deserialize_PayloadWithoutManagedSet_ReadsAsNothingManaged()
    {
        // Arrange
        // A state file written before the managed set existed must still load.
        const string json =
            """
            { "version": 1, "database": { "schemas": [] }, "scripts": [] }
            """;

        // Act
        var state = _sut.Deserialize(Encoding.UTF8.GetBytes(json));

        // Assert
        state.Managed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_PayloadWithoutScripts_ReadsAsAnEmptyLedger()
    {
        // A state file written before the ledger existed must still load.
        const string json =
            """
            { "version": 1, "schema": { "schemas": [], "droppedSchemas": [] } }
            """;

        _sut.Deserialize(Encoding.UTF8.GetBytes(json)).Scripts.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_LegacyExecutedScriptsField_ReadsAsAnEmptyLedger()
    {
        // The 4.x field name is deliberately not read (no dual-read shim): a pre-5.0 ledger is refreshed or
        // untainted by hand under the state-format compat policy's major-version rules.
        const string json =
            """
            {
              "version": 1,
              "schema": { "schemas": [], "droppedSchemas": [] },
              "executedScripts": [ { "name": "api-login", "hash": "abc123", "executedUtc": "2026-07-10T12:00:00+00:00" } ]
            }
            """;

        _sut.Deserialize(Encoding.UTF8.GetBytes(json)).Scripts.ShouldBeEmpty();
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
        var act = () => _sut.Deserialize(Encoding.UTF8.GetBytes(json));

        // Assert
        act.ShouldThrow<NotSupportedException>();
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsStateDeserializationException()
    {
        // Arrange: not valid JSON at all.
        var act = () => _sut.Deserialize(Encoding.UTF8.GetBytes("{ not json"));

        // Act + Assert: a parse failure surfaces as the dedicated exception with the cause preserved.
        var ex = act.ShouldThrow<StateDeserializationException>();
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void Deserialize_StructurallyValidButInvalidModel_ThrowsStateDeserializationException()
    {
        // Arrange: well-formed JSON whose column type has no name, so SqlType's constructor would NRE
        // deep inside the parameterized-constructor converter. The dedicated exception wraps that.
        const string json =
            """
            {
              "version": 1,
              "database": {
                "schemas": [
                  { "name": "app", "tables": [
                    { "name": "t", "columns": [ { "name": "c", "type": {} } ] }
                  ] }
                ],
                "droppedSchemas": []
              }
            }
            """;

        // Act
        var act = () => _sut.Deserialize(Encoding.UTF8.GetBytes(json));

        // Assert: the caller gets a meaningful exception instead of a bare NullReferenceException.
        var ex = act.ShouldThrow<StateDeserializationException>();
        ex.InnerException.ShouldNotBeNull();
    }
}
