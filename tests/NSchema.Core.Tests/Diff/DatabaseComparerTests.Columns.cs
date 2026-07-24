using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Column-level changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_ColumnInCurrentButNotDesired_IsRemoved()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "email", Type = SqlType.Text }] },
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        var column = table!.Columns.ShouldHaveSingleItem();
        column.Name.ShouldBe("email");
        column.Kind.ShouldBe(ChangeKind.Remove);
        column.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_ColumnRename_SetsRenamedFrom()
    {
        var column = DiffColumn(new Column { Name = "mail", Type = SqlType.Text }, new Column { Name = "email", Type = SqlType.Text }, ColumnRename("mail", "email"));

        column!.RenamedFrom.ShouldBe("mail");
        column.Kind.ShouldBe(ChangeKind.Modify);
    }

    [Fact]
    public void Compare_ColumnTypeChange_IsReportedInIsolation()
    {
        var column = DiffColumn(new Column { Name = "total", Type = SqlType.Int }, new Column { Name = "total", Type = SqlType.BigInt });

        column!.Type.ShouldBe(new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt));
        column.Nullability.ShouldBeNull();
        column.Default.ShouldBeNull();
    }

    [Fact]
    public void Compare_ColumnDefaultChange_IsReported()
    {
        var column = DiffColumn(new Column { Name = "status", Type = SqlType.Text }, new Column { Name = "status", Type = SqlType.Text, DefaultExpression = "'new'" });

        column!.Default.ShouldBe(new ValueChange<SqlDefaultExpression>(null, "'new'"));
    }

    [Fact]
    public void Compare_ModifiedColumn_CarriesDesiredDefinition()
    {
        // The desired column rides along on a modified column's Definition so a dialect whose in-place ALTER COLUMN
        // must restate the whole column (SQL Server) can read the final type and nullability together.
        var column = DiffColumn(
            new Column { Name = "total", Type = SqlType.Int, IsNullable = false },
            new Column { Name = "total", Type = SqlType.BigInt, IsNullable = false });

        column!.Definition.ShouldNotBeNull();
        column.Definition!.Type.ShouldBe(SqlType.BigInt);
        column.Definition.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Compare_IdentityOptionsChange_IsReported_WhenBothColumnsAreIdentity()
    {
        var current = new Column { Name = "id", Type = SqlType.Int, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) };
        var desired = new Column { Name = "id", Type = SqlType.Int, IsIdentity = true, IdentityOptions = new IdentityOptions(100, 1, 1) };

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), new IdentityOptions(100, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityEnabled_ReportsChangeFromNullToDesiredOptions()
    {
        var current = new Column { Name = "id", Type = SqlType.Int };
        var desired = new Column { Name = "id", Type = SqlType.Int, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) };

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 1)));
    }

    [Fact]
    public void Compare_IdentityDisabled_ReportsChangeFromCurrentOptionsToNull()
    {
        var current = new Column { Name = "id", Type = SqlType.Int, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) };
        var desired = new Column { Name = "id", Type = SqlType.Int };

        var column = DiffColumn(current, desired);

        column!.Identity.ShouldBe(new ValueChange<IdentityOptions>(new IdentityOptions(1, 1, 1), null));
    }

    [Fact]
    public void Compare_UnchangedColumn_ProducesNoDiff()
        => DiffColumn(new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "id", Type = SqlType.Int }).ShouldBeNull();

    [Fact]
    public void Compare_GenerationExpressionAdded_IsReported()
    {
        var column = DiffColumn(
            new Column { Name = "area", Type = SqlType.Int },
            new Column { Name = "area", Type = SqlType.Int, GeneratedExpression = "w * h" });

        column!.Generated.ShouldBe(new ValueChange<SqlText>(null, "w * h"));
    }

    [Fact]
    public void Compare_GenerationExpressionChanged_IsReported()
    {
        var column = DiffColumn(
            new Column { Name = "area", Type = SqlType.Int, GeneratedExpression = "w * h" },
            new Column { Name = "area", Type = SqlType.Int, GeneratedExpression = "w * h * 2" });

        column!.Generated.ShouldBe(new ValueChange<SqlText>("w * h", "w * h * 2"));
    }

    [Fact]
    public void Compare_UnchangedGeneratedColumn_ProducesNoDiff()
        => DiffColumn(
            new Column { Name = "area", Type = SqlType.Int, GeneratedExpression = "w * h" },
            new Column { Name = "area", Type = SqlType.Int, GeneratedExpression = "w * h" }).ShouldBeNull();
}
