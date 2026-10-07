using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Publications;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Publications;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Publications (database-global) and replica identity
    // -------------------------------------------------------------------------

    private static readonly ObjectAddress _orders = new("sales", "orders");

    private PublicationDiff? DiffPublications(IReadOnlyList<Publication> current, IReadOnlyList<Publication> desired, ProjectDirectives? directives = null) =>
        Compare(new Database { Publications = [.. current] }, new Database { Publications = [.. desired] }, directives)
        .Publications.SingleOrDefault();

    private static Publication Feed(params PublishedTable[] tables) => new() { Name = "feed", Tables = [.. tables] };

    [Fact]
    public void Compare_NewPublication_IsAddCarryingDefinition()
    {
        var diff = DiffPublications([], [Feed(new PublishedTable(_orders))]);

        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.Definition!.Tables.ShouldHaveSingleItem().Table.ShouldBe(_orders);
    }

    [Fact]
    public void Compare_RemovedPublication_IsRemove()
        => DiffPublications([Feed()], [])!.Change.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_UnchangedPublication_ProducesNoDiff()
        => DiffPublications([Feed(new PublishedTable(_orders, ["id"], "id > 0"))], [Feed(new PublishedTable(_orders, ["id"], "id > 0"))]).ShouldBeNull();

    [Fact]
    public void Compare_TableKindOnOneSideOnly_IsNotAChange()
        => DiffPublications([Feed(new PublishedTable(ObjectAddress.Table("sales", "orders")))], [Feed(new PublishedTable(_orders))]).ShouldBeNull();

    [Fact]
    public void Compare_ColumnsListedInAnotherOrder_IsNotAChange()
        => DiffPublications([Feed(new PublishedTable(_orders, ["id", "status"]))], [Feed(new PublishedTable(_orders, ["status", "id"]))]).ShouldBeNull();

    [Fact]
    public void Compare_FilterDifferingOnlyCosmetically_IsNotAChange()
        => DiffPublications([Feed(new PublishedTable(_orders, Filter: "id  >  0"))], [Feed(new PublishedTable(_orders, Filter: "id > 0"))]).ShouldBeNull();

    [Fact]
    public void Compare_PublishedTables_AddedRemovedAndChanged()
    {
        // Arrange
        var lines = new ObjectAddress("sales", "order_lines");
        var customers = new ObjectAddress("sales", "customers");

        // Act
        var diff = DiffPublications(
            [Feed(new PublishedTable(_orders), new PublishedTable(lines))],
            [Feed(new PublishedTable(_orders, ["id"]), new PublishedTable(customers))]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.Tables.Select(t => (t.Table.Value, t.Change)).ShouldBe(
        [
            ("sales.customers", ChangeKind.Add),
            ("sales.order_lines", ChangeKind.Remove),
            ("sales.orders", ChangeKind.Modify),
        ]);
    }

    [Fact]
    public void Compare_PublishedSchemasAndOperations()
    {
        // Act
        var diff = DiffPublications(
            [new Publication { Name = "feed", Schemas = ["audit"] }],
            [new Publication { Name = "feed", Schemas = ["archive"], Operations = PublishedOperations.Insert }]);

        // Assert
        diff!.Schemas.ShouldBe([new PublishedSchemaChange(ChangeKind.Add, "archive"), new PublishedSchemaChange(ChangeKind.Remove, "audit")]);
        diff.Operations.ShouldBe(new ValueChange<PublishedOperations>(PublishedOperations.All, PublishedOperations.Insert));
    }

    [Fact]
    public void Compare_SwitchingToAllTables_Recreates()
    {
        // Act
        var diff = DiffPublications([Feed(new PublishedTable(_orders))], [new Publication { Name = "feed", AllTables = true }]);

        // Assert
        diff!.RequiresRecreate.ShouldBeTrue();
        diff.Definition!.AllTables.ShouldBeTrue();
    }

    [Fact]
    public void Compare_RenamedPublication_SetsRenamedFrom()
    {
        // Act
        var diff = DiffPublications(
            [new Publication { Name = "legacy_feed" }],
            [Feed()],
            new ProjectDirectives(PublicationRenames: [new PublicationRenameDirective(DatabaseAddress.Publication("legacy_feed"), DatabaseAddress.Publication("feed"))]));

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RenamedFrom.ShouldBe("legacy_feed");
    }

    [Fact]
    public void Compare_RenamedTable_MovesThePublishedEntryWithIt()
    {
        // Arrange
        Table Orders(string name) => new() { Name = name, Columns = [new Column { Name = "id", Type = SqlType.Int }] };
        var current = new Database
        {
            Schemas = [new Schema { Name = "sales", Tables = [Orders("legacy_orders")] }],
            Publications = [Feed(new PublishedTable(new ObjectAddress("sales", "legacy_orders"), ["id"]))],
        };
        var desired = new Database
        {
            Schemas = [new Schema { Name = "sales", Tables = [Orders("orders")] }],
            Publications = [Feed(new PublishedTable(_orders, ["id"]))],
        };

        // Act
        var diff = Compare(current, desired, new ProjectDirectives(ObjectRenames: [new ObjectRenameDirective(ObjectAddress.Table("sales", "legacy_orders"), "orders")]));

        // Assert — the rename is the table's; the publication follows it and so has nothing to change.
        diff.Publications.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ReplicaIdentityChange_CarriesOldAndNew()
    {
        // Act
        var diff = DiffTable(
            new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "t", Columns = [new Column { Name = "id", Type = SqlType.Int }], ReplicaIdentity = ReplicaIdentity.Full });

        // Assert
        diff!.ReplicaIdentity.ShouldBe(new ValueChange<ReplicaIdentity>(null, ReplicaIdentity.Full));
    }
}
