using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Publications;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Policies;

namespace NSchema.Tests.Project.Policies;

public sealed class ReplicationPolicyTests
{
    private readonly ReplicationPolicy _sut = new();

    private static readonly ObjectAddress _events = new("app", "events");

    private static Table Events(PrimaryKey? key = null, ReplicaIdentity? identity = null) => new()
    {
        Name = "events",
        PrimaryKey = key,
        ReplicaIdentity = identity,
        Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "kind", Type = SqlType.Text }],
    };

    private static PrimaryKey Key() => new() { Name = "events_pk", ColumnNames = ["id"] };

    private static Database Db(Table table, Publication publication) => new()
    {
        Schemas = [new Schema { Name = "app", Tables = [table] }],
        Publications = [publication],
    };

    private static Publication Feed(PublishedTable entry, PublishedOperations operations = PublishedOperations.All) =>
        new() { Name = "feed", Tables = [entry], Operations = operations };

    [Fact]
    public void KeyedTable_IsQuiet()
        => _sut.Validate(Db(Events(Key()), Feed(new PublishedTable(_events)))).ShouldBeEmpty();

    [Fact]
    public void UnkeyedTable_PublishingUpdates_Warns()
        => _sut.Validate(Db(Events(), Feed(new PublishedTable(_events)))).ShouldHaveSingleItem().Code.ShouldBe("unidentified-published-table");

    [Fact]
    public void UnkeyedTable_PublishingOnlyInserts_IsQuiet()
        => _sut.Validate(Db(Events(), Feed(new PublishedTable(_events), PublishedOperations.Insert))).ShouldBeEmpty();

    [Fact]
    public void UnkeyedTable_WithFullIdentity_IsQuiet()
        => _sut.Validate(Db(Events(identity: ReplicaIdentity.Full), Feed(new PublishedTable(_events)))).ShouldBeEmpty();

    [Fact]
    public void KeyedTable_WithNothingIdentity_Warns()
        => _sut.Validate(Db(Events(Key(), ReplicaIdentity.Nothing), Feed(new PublishedTable(_events)))).ShouldHaveSingleItem().Code.ShouldBe("unidentified-published-table");

    [Fact]
    public void TableCoveredBySchema_IsChecked()
        => _sut.Validate(Db(Events(), new Publication { Name = "feed", Schemas = ["app"] })).ShouldHaveSingleItem().Code.ShouldBe("unidentified-published-table");

    [Fact]
    public void FilterOnAColumnOutsideTheKey_Warns()
    {
        // Act
        var diagnostic = _sut.Validate(Db(Events(Key()), Feed(new PublishedTable(_events, Filter: "kind <> 'noise' AND id > 0")))).ShouldHaveSingleItem();

        // Assert
        diagnostic.Code.ShouldBe("filter-outside-identity");
        diagnostic.Message.ShouldContain("'kind'");
        diagnostic.Message.ShouldNotContain("'id'");
    }

    [Fact]
    public void FilterOnAColumnInTheIdentityIndex_IsQuiet()
    {
        // Arrange
        var table = Events(identity: ReplicaIdentity.UsingIndex("ux_events"));
        table.Indexes.Add(new TableIndex { Name = "ux_events", IsUnique = true, Columns = ["id", "kind"] });

        // Act
        var diagnostics = _sut.Validate(Db(table, Feed(new PublishedTable(_events, Filter: "kind <> 'noise'"))));

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
