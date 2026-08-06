using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Model.Indexes;
using NSchema.Model.Services;
using NSchema.Model.Views;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Materialized views (View with IsMaterialized + Indexes)
    // -------------------------------------------------------------------------

    private static View Matview(string name, string body, ObjectMemberCollection<TableIndex>? indexes = null, string? comment = null) =>
        new View { Name = name, Body = body, IsMaterialized = true, Indexes = indexes ?? [], Comment = comment };

    [Fact]
    public void Compare_NewMaterializedView_IsAddWithMaterializedFlag()
    {
        // Act
        var diff = DiffViews([], [Matview("daily", "SELECT 1")]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Add);
        diff.IsMaterialized.ShouldBeTrue();
        diff.Definition!.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void Compare_MaterializedViewBodyChange_RequiresRecreate()
    {
        // Act
        // There is no CREATE OR REPLACE MATERIALIZED VIEW, so a body change must drop + recreate.
        var diff = DiffViews([Matview("daily", "SELECT 1")], [Matview("daily", "SELECT 2")]);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Modify);
        diff.RequiresRecreate.ShouldBeTrue();
        diff.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_PlainViewBodyChange_DoesNotRequireRecreate()
    {
        // Act
        var diff = DiffViews([View("v", "SELECT 1")], [View("v", "SELECT 2")]);

        // Assert
        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldNotBeNull(); // an in-place CREATE OR REPLACE
    }

    [Fact]
    public void Compare_ViewToMaterializedFlip_RequiresRecreate()
    {
        // Act
        var diff = DiffViews([View("v", "SELECT 1")], [Matview("v", "SELECT 1")]);

        // Assert
        diff!.RequiresRecreate.ShouldBeTrue();
        diff.IsMaterialized.ShouldBeTrue();
        // The flip is carried explicitly so the plan can drop the view as what it currently is.
        diff.Materialized.ShouldNotBeNull()
            .ShouldSatisfyAllConditions(m => m.Old.ShouldBe(false), m => m.New.ShouldBe(true));
    }

    [Fact]
    public void Compare_MaterializedViewBodyChange_DoesNotReportMaterializedFlip()
    {
        // Act
        var diff = DiffViews([Matview("daily", "SELECT 1")], [Matview("daily", "SELECT 2")]);

        // Assert
        diff!.Materialized.ShouldBeNull();
    }

    [Fact]
    public void Compare_MaterializedViewIndexAdded_IsInPlaceIndexDiff()
    {
        var diff = DiffViews(
            [Matview("daily", "SELECT 1")],
            [Matview("daily", "SELECT 1", indexes: [new TableIndex { Name = "daily_ix", Columns = ["x"] }])]);

        diff!.RequiresRecreate.ShouldBeFalse();
        diff.Definition.ShouldBeNull(); // body unchanged
        diff.Indexes.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public void Compare_MaterializedViewBodyAndIndexChange_RecreatesWithIndexesOnDefinition()
    {
        var diff = DiffViews(
            [Matview("daily", "SELECT 1", indexes: [new TableIndex { Name = "a", Columns = ["x"] }])],
            [Matview("daily", "SELECT 2", indexes: [new TableIndex { Name = "b", Columns = ["y"] }])]);

        diff!.RequiresRecreate.ShouldBeTrue();
        diff.Indexes.ShouldBeEmpty(); // not diffed in place during a recreate
        diff.Definition!.Indexes.ShouldHaveSingleItem().Name.ShouldBe("b"); // rebuilt with the definition
    }

    [Fact]
    public void Compare_RemovedMaterializedView_CarriesMaterializedFlag()
    {
        // Act
        var diff = DiffViews([Matview("daily", "SELECT 1")], []);

        // Assert
        diff!.Change.ShouldBe(ChangeKind.Remove);
        diff.IsMaterialized.ShouldBeTrue();
    }
}
