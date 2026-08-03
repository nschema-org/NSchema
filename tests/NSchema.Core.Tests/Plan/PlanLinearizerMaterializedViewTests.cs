using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Views;
using NSchema.Model.Indexes;
using NSchema.Model.Views;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Indexes;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Views;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins how the linearizer turns materialized-view diffs into actions: a recreate is a drop + create (no
/// CREATE OR REPLACE MATERIALIZED VIEW), in-place index changes become index actions against the view, and the
/// materialized flag flows onto the view actions.
/// </summary>
public sealed class PlanLinearizerMaterializedViewTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> Linearize(ViewDiff view) =>
        _linearizer.Linearize(new DatabaseDiff([SchemaDiff.Containing("app") with { Views = [view] }]), PlanDependencies.None, DialectCapabilities.Standard);

    [Fact]
    public void RecreatedMaterializedView_EmitsDropAndCreateBothMaterialized()
    {
        var mv = new View { Name = "daily", Body = "SELECT 2", IsMaterialized = true };
        var actions = Linearize(ViewDiff.Modified("app", "daily") with
        {
            Definition = mv,
            IsMaterialized = true,
            RequiresRecreate = true,
        });

        actions.OfType<DropView>().ShouldHaveSingleItem().IsMaterialized.ShouldBeTrue();
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void RecreatedMaterializedView_DropsBeforeItCreates()
    {
        // Arrange
        var mv = new View { Name = "daily", Body = "SELECT 2", IsMaterialized = true };
        var actions = Linearize(ViewDiff.Modified("app", "daily") with
        {
            Definition = mv,
            IsMaterialized = true,
            RequiresRecreate = true,
        });

        var drop = actions.Select((a, i) => (a, i)).Single(x => x.a is DropView).i;

        // Act
        var create = actions.Select((a, i) => (a, i)).Single(x => x.a is CreateView).i;

        // Assert
        drop.ShouldBeLessThan(create);
    }

    [Fact]
    public void InPlaceIndexChange_EmitsIndexActionsAgainstTheView()
    {
        var actions = Linearize(ViewDiff.Modified("app", "daily") with
        {
            IsMaterialized = true,
            Indexes = [
                IndexDiff.Added(new TableIndex { Name = "daily_ix", Columns = ["x"] }),
                IndexDiff.Removed("old_ix"),
            ],
        });

        actions.OfType<CreateIndex>().ShouldHaveSingleItem().Table.Name.ShouldBe("daily");
        actions.OfType<DropIndex>().ShouldHaveSingleItem().Index.Member.ShouldBe("old_ix");
        actions.OfType<CreateView>().ShouldBeEmpty(); // body unchanged, no recreate
    }

    [Fact]
    public void RenamedMaterializedView_IndexDropTargetsOldName()
    {
        // Arrange
        // The index drop sorts before RenameView, so it runs while the view still carries its old name; the
        // index create sorts after and targets the new one.
        var actions = Linearize(ViewDiff.Modified("app", "daily") with
        {
            RenamedFrom = "nightly",
            IsMaterialized = true,
            Indexes = [
                IndexDiff.Added(new TableIndex { Name = "daily_ix", Columns = ["x"] }),
                IndexDiff.Removed("old_ix"),
            ],
        });

        actions.OfType<DropIndex>().ShouldHaveSingleItem().Index.Object.ShouldBe("nightly");
        actions.OfType<CreateIndex>().ShouldHaveSingleItem().Table.Name.ShouldBe("daily");
        var dropIndex = actions.Select((a, i) => (a, i)).Single(x => x.a is DropIndex).i;

        // Act
        var rename = actions.Select((a, i) => (a, i)).Single(x => x.a is RenameView).i;

        // Assert
        dropIndex.ShouldBeLessThan(rename);
    }

    [Fact]
    public void RenamedRecreatedView_DropsOldNameAndSkipsRename()
    {
        // A rename accompanying a recreate is subsumed by it: the old name is dropped and the definition
        // recreates the view under the new one.
        var mv = new View { Name = "daily", Body = "SELECT 2", IsMaterialized = true };
        var actions = Linearize(ViewDiff.Modified("app", "daily") with
        {
            RenamedFrom = "nightly",
            Definition = mv,
            IsMaterialized = true,
            RequiresRecreate = true,
        });

        actions.OfType<RenameView>().ShouldBeEmpty();
        actions.OfType<DropView>().ShouldHaveSingleItem().View.Name.ShouldBe("nightly");
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.Name.ShouldBe("daily");
    }

    [Fact]
    public void ViewToMaterializedFlip_DropsAsPlainAndCreatesAsMaterialized()
    {
        // The view being dropped is still the current (plain) one; only the recreate is materialized.
        var mv = new View { Name = "v", Body = "SELECT 1", IsMaterialized = true };
        var actions = Linearize(ViewDiff.Modified("app", "v") with
        {
            Definition = mv,
            IsMaterialized = true,
            Materialized = new ValueChange<bool>(false, true),
            RequiresRecreate = true,
        });

        actions.OfType<DropView>().ShouldHaveSingleItem().IsMaterialized.ShouldBeFalse();
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void MaterializedToViewFlip_DropsAsMaterialized()
    {
        var actions = Linearize(ViewDiff.Modified("app", "v") with
        {
            Definition = new View { Name = "v", Body = "SELECT 1" },
            IsMaterialized = false,
            Materialized = new ValueChange<bool>(true, false),
            RequiresRecreate = true,
        });

        actions.OfType<DropView>().ShouldHaveSingleItem().IsMaterialized.ShouldBeTrue();
        actions.OfType<CreateView>().ShouldHaveSingleItem().View.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void PlainViewBodyChange_EmitsOnlyReplaceNoDrop()
    {
        var actions = Linearize(ViewDiff.Modified("app", "v")
            with
        { Definition = new View { Name = "v", Body = "SELECT 2" } });

        actions.OfType<ReplaceView>().ShouldHaveSingleItem();
        actions.OfType<CreateView>().ShouldBeEmpty();
        actions.OfType<DropView>().ShouldBeEmpty();
    }
}
