using NSchema.Diff.Domain;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Clustering
    // -------------------------------------------------------------------------

    private static Table Users(PrimaryKey? key = null, params TableIndex[] indexes) => new()
    {
        Name = "users",
        Columns = [new Column { Name = "id", Type = SqlType.Int }],
        PrimaryKey = key,
        Indexes = [.. indexes],
    };

    private static PrimaryKey Key(bool? clustered) =>
        new() { Name = "users_pk", ColumnNames = ["id"], Clustered = clustered };

    private static TableIndex Index(bool? clustered) =>
        new() { Name = "users_ix", Columns = [new IndexColumn("id")], Clustered = clustered };

    /// <summary>
    /// Clustering is the table's physical row order, and no engine alters it in place, so a change to it has
    /// to come out of the diff as a drop and a recreate rather than as a modification.
    /// </summary>
    [Fact]
    public void Compare_PrimaryKeyBecomesNonclustered_IsRecreated()
    {
        var diff = DiffTable(Users(Key(clustered: true)), Users(Key(clustered: false)));

        diff!.PrimaryKeys.Select(k => k.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexBecomesClustered_IsRecreated()
    {
        var diff = DiffTable(Users(null, Index(clustered: false)), Users(null, Index(clustered: true)));

        diff!.Indexes.Select(i => i.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    /// <summary>
    /// Unwritten is not the same as nonclustered — the engines disagree on the default, so the two have to
    /// stay distinguishable or a plan would silently reorder a table.
    /// </summary>
    [Fact]
    public void Compare_UnspecifiedAgainstNonclustered_IsAChange()
    {
        var diff = DiffTable(Users(Key(clustered: null)), Users(Key(clustered: false)));

        diff.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_SameClustering_ProducesNoDiff()
        => DiffTable(Users(Key(clustered: true)), Users(Key(clustered: true))).ShouldBeNull();
}
