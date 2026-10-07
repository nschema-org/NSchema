using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Publications;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.Publications;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins where publication actions fall in a plan: taken off what they read before it changes or goes, and
/// put back once it exists.
/// </summary>
public sealed class PlanLinearizerPublicationTests
{
    private readonly PlanLinearizer _linearizer = new();
    private readonly DatabaseComparer _comparer = new(NullLogger<DatabaseComparer>.Instance, new SqlEquivalence());

    private static readonly ObjectAddress _orders = new("sales", "orders");

    private IReadOnlyList<MigrationAction> Plan(Database current, Database desired, ProjectDirectives? directives = null)
    {
        var aligned = DatabaseAligner.Align(current, desired, directives ?? ProjectDirectives.Empty).Require();
        var diff = _comparer.Compare(aligned, desired);
        return _linearizer.Linearize(diff, new PlanDependencies(current, desired), DialectCapabilities.Standard);
    }

    private static Database Db(Table? orders, params Publication[] publications) => new()
    {
        Schemas = orders is null ? [] : [new Schema { Name = "sales", Tables = [orders] }],
        Publications = [.. publications],
    };

    private static Table Orders(SqlType statusType, params string[] extra) => new()
    {
        Name = "orders",
        Columns =
        [
            new Column { Name = "id", Type = SqlType.Int },
            new Column { Name = "status", Type = statusType },
            .. extra.Select(name => new Column { Name = name, Type = SqlType.Int }),
        ],
    };

    private static Publication Feed(PublishedTable entry) => new() { Name = "feed", Tables = [entry] };

    private static int IndexOf<T>(IReadOnlyList<MigrationAction> actions) where T : MigrationAction =>
        actions.Select((a, i) => (a, i)).Single(x => x.a is T).i;

    [Fact]
    public void CreatedPublication_FollowsTheTableItPublishes()
    {
        // Act
        var actions = Plan(Db(null), Db(Orders(SqlType.Text), Feed(new PublishedTable(_orders))));

        // Assert
        IndexOf<CreatePublication>(actions).ShouldBeGreaterThan(IndexOf<CreateTable>(actions));
    }

    [Fact]
    public void RemovedEntry_PrecedesTheDropOfTheColumnItLists()
    {
        // Act
        var actions = Plan(
            Db(Orders(SqlType.Text, "legacy"), Feed(new PublishedTable(_orders, ["id", "legacy"]))),
            Db(Orders(SqlType.Text), Feed(new PublishedTable(_orders, ["id"]))));

        // Assert — the entry changes, so it is removed before the column goes and added back after.
        IndexOf<DropPublicationTable>(actions).ShouldBeLessThan(IndexOf<DropColumn>(actions));
        IndexOf<AddPublicationTable>(actions).ShouldBeGreaterThan(IndexOf<DropColumn>(actions));
    }

    [Fact]
    public void RetypedColumn_ReadByAFilter_IsRepublishedAroundTheChange()
    {
        // Arrange
        var entry = new PublishedTable(_orders, Filter: "status <> 'draft'");

        // Act
        var actions = Plan(Db(Orders(SqlType.VarChar(20)), Feed(entry)), Db(Orders(SqlType.Text), Feed(entry)));

        // Assert
        IndexOf<DropPublicationTable>(actions).ShouldBeLessThan(IndexOf<AlterColumn>(actions));
        IndexOf<AddPublicationTable>(actions).ShouldBeGreaterThan(IndexOf<AlterColumn>(actions));
        actions.OfType<AddPublicationTable>().ShouldHaveSingleItem().Table.ShouldBe(entry);
    }

    [Fact]
    public void RetypedColumn_NotReadByTheEntry_LeavesThePublicationAlone()
    {
        // Arrange
        var entry = new PublishedTable(_orders, ["id"]);

        // Act
        var actions = Plan(Db(Orders(SqlType.VarChar(20)), Feed(entry)), Db(Orders(SqlType.Text), Feed(entry)));

        // Assert
        actions.OfType<DropPublicationTable>().ShouldBeEmpty();
    }

    [Fact]
    public void AllTablesSwitch_DropsAndCreates()
    {
        // Act
        var actions = Plan(Db(null, Feed(new PublishedTable(_orders))), Db(null, new Publication { Name = "feed", AllTables = true }));

        // Assert
        IndexOf<DropPublication>(actions).ShouldBeLessThan(IndexOf<CreatePublication>(actions));
    }

    [Fact]
    public void RenamedPublication_RenamesBeforeItsOtherChanges()
    {
        // Act
        var actions = Plan(
            Db(Orders(SqlType.Text), new Publication { Name = "legacy_feed" }),
            Db(Orders(SqlType.Text), Feed(new PublishedTable(_orders))),
            new ProjectDirectives(PublicationRenames: [new PublicationRenameDirective(DatabaseAddress.Publication("legacy_feed"), DatabaseAddress.Publication("feed"))]));

        // Assert
        IndexOf<RenamePublication>(actions).ShouldBeLessThan(IndexOf<AddPublicationTable>(actions));
        actions.OfType<AddPublicationTable>().ShouldHaveSingleItem().PublicationName.ShouldBe("feed");
    }

    [Fact]
    public void ReplicaIdentityUsingANewIndex_FollowsTheIndex()
    {
        // Arrange
        var current = Orders(SqlType.Text);
        var desired = Orders(SqlType.Text);
        desired.Indexes.Add(new NSchema.Model.Indexes.TableIndex { Name = "ux_orders_id", IsUnique = true, Columns = ["id"] });
        desired.ReplicaIdentity = ReplicaIdentity.UsingIndex("ux_orders_id");

        // Act
        var actions = Plan(Db(current), Db(desired));

        // Assert
        IndexOf<SetReplicaIdentity>(actions).ShouldBeGreaterThan(IndexOf<NSchema.Plan.Domain.Indexes.CreateIndex>(actions));
    }
}
