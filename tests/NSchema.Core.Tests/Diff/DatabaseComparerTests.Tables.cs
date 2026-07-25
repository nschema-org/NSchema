using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Table removal: partial vs full schemas and explicit drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_FullSchema_DropsCurrentTableNotInDesired()
    {
        // Arrange
        var current = Db(new Schema { Name = "app", Tables = [new Table { Name = "stale", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] });
        var desired = Db(new Schema { Name = "app", Tables = [new Table { Name = "fresh", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] });

        // Act
        var tables = Compare(current, desired).Schemas.Single().Tables;

        // Assert
        tables.Single(t => t.Name.Value.Equals("stale")).Kind.ShouldBe(ChangeKind.Remove);
    }

    // -------------------------------------------------------------------------
    // Table-level changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_TableRename_SetsRenamedFrom()
    {
        // Arrange
        var table = DiffTable(
            new Table { Name = "people", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] },

            // Act
            TableRename("people", "users"));

        // Assert
        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_AmbiguousRename_FallsBackToAddAndKeep()
    {
        // 'people' is renamed to 'users' while 'people' is also still declared. The aligner skips the rename
        // (raising an error diagnostic — see DatabaseAlignerTests), so the compare sees a plain add.
        var current = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "people", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
        ],
        });
        var desired = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "people", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
        ],
        });

        var table = Compare(current, desired, TableRename("people", "users")).Schemas.Single().Tables.ShouldHaveSingleItem();

        table.Name.ShouldBe("users");
        table.Kind.ShouldBe(ChangeKind.Add);
        table.RenamedFrom.ShouldBeNull();
    }

    [Fact]
    public void Compare_PlainRename_IsNotTreatedAsAmbiguous()
    {
        // Arrange
        // A rename whose old name is gone and whose new name is free is unambiguous and must still work.
        var table = DiffTable(
            new Table { Name = "people", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] },

            // Act
            TableRename("people", "users"));

        // Assert
        table.ShouldNotBeNull();
        table.Kind.ShouldBe(ChangeKind.Modify);
        table.RenamedFrom.ShouldBe("people");
    }

    [Fact]
    public void Compare_TableCommentChange_FoldsCommentValueChange()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }], Comment = "old" },
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }], Comment = "new" });

        table!.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewTable_FoldsForeignKeysIndexesGrantsAndIndexComment()
    {
        var desired = new Table
        {
            Name = "orders",
            Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "user_id", Type = SqlType.Int }],
            ForeignKeys = [new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] }],
            Indexes = [new TableIndex { Name = "orders_user_ix", Columns = ["user_id"], Comment = "lookup" }],
            Grants = [new TableGrant("reader", TablePrivilege.Select)],
        };

        var table = Compare(Db(new Schema { Name = "app" }),
            Db(new Schema { Name = "app", Tables = [desired] })).Schemas.Single().Tables.Single();

        table.ForeignKeys.ShouldHaveSingleItem().ShouldBe(ForeignKeyDiff.Added(new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] }));
        table.Grants.ShouldHaveSingleItem().Privileges.ShouldBe(TablePrivilege.Select);
        // A new index carries both its definition and a folded comment change.
        table.Indexes.Select(i => i.Kind).ShouldBe([ChangeKind.Add, ChangeKind.Modify]);
        table.Indexes[1].Comment.ShouldBe(new ValueChange<string>(null, "lookup"));
    }
}
