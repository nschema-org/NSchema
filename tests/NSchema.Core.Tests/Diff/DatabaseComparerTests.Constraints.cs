using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff;

public partial class DatabaseComparerTests
{
    // -------------------------------------------------------------------------
    // Primary keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_PrimaryKeyAdded_EmitsAddConstraint()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] }, Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        table!.PrimaryKeys.ShouldHaveSingleItem().ShouldBe(
            PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] }));
    }

    [Fact]
    public void Compare_PrimaryKeyDropped_EmitsRemoveConstraint()
    {
        var table = DiffTable(
            new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] }, Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        table!.PrimaryKeys.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_PrimaryKeyChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] }, Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id", "tenant"] }, Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        table!.PrimaryKeys.Select(c => c.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_PrimaryKeyCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"], Comment = "old" }, Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"], Comment = "new" }, Columns = [new Column { Name = "id", Type = SqlType.Int }] });

        var pk = table!.PrimaryKeys.ShouldHaveSingleItem();
        pk.Change.ShouldBe(ChangeKind.Modify);
        pk.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    // -------------------------------------------------------------------------
    // Foreign keys
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_ForeignKeyRemoved_EmitsRemoveConstraint()
    {
        var fk = new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] };
        var table = DiffTable(
            new Table { Name = "orders", Columns = [new Column { Name = "user_id", Type = SqlType.Int }], ForeignKeys = [fk] },
            new Table { Name = "orders", Columns = [new Column { Name = "user_id", Type = SqlType.Int }] });

        table!.ForeignKeys.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_ForeignKeyDefinitionChanged_EmitsRemoveAndAdd()
    {
        var table = DiffTable(
            new Table
            {
                Name = "orders",
                Columns = [new Column { Name = "user_id", Type = SqlType.Int }],
                ForeignKeys = [new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] }],
            },
            new Table
            {
                Name = "orders",
                Columns = [new Column { Name = "user_id", Type = SqlType.Int }],
                ForeignKeys = [new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"], OnDelete = ReferentialAction.Cascade }],
            });

        table!.ForeignKeys.Select(c => c.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Unique constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_UniqueConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }] },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
            });

        table!.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }));
    }

    [Fact]
    public void Compare_UniqueConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
            },
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }] });

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Change.ShouldBe(ChangeKind.Remove);
        unique.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_UniqueConstraintColumnsChanged_EmitsRemoveThenAdd()
    {
        ObjectMemberCollection<Column> Columns() => [new Column { Name = "email", Type = SqlType.Text }, new Column { Name = "tenant", Type = SqlType.Int }];
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = Columns(),
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
            },
            new Table
            {
                Name = "users",
                Columns = Columns(),
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email", "tenant"] }],
            });

        table!.UniqueConstraints.Select(c => c.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_UniqueConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"], Comment = "old" }],
            },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"], Comment = "new" }],
            });

        var unique = table!.UniqueConstraints.ShouldHaveSingleItem();
        unique.Change.ShouldBe(ChangeKind.Modify);
        unique.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_NewUniqueConstraintWithComment_FoldsCommentAsModify()
    {
        // Arrange
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }] },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"], Comment = "lookup" }],
            });

        // Act
        table!.UniqueConstraints.Select(c => (c.Change, c.Comment?.New))

        // Assert
            .ShouldBe([(ChangeKind.Add, null), (ChangeKind.Modify, "lookup")]);
    }

    [Fact]
    public void Compare_UniqueConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
            },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "email", Type = SqlType.Text }],
                UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
            });

        table.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Check constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_CheckConstraintAdded_EmitsAdd()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "age", Type = SqlType.Int }] },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
            });

        table!.Checks.ShouldHaveSingleItem().ShouldBe(
            CheckConstraintDiff.Added(new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }));
    }

    [Fact]
    public void Compare_CheckConstraintRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
            },
            new Table { Name = "users", Columns = [new Column { Name = "age", Type = SqlType.Int }] });

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Change.ShouldBe(ChangeKind.Remove);
        check.Definition.ShouldBeNull();
    }

    [Fact]
    public void Compare_CheckConstraintExpressionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
            },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age > 0" }],
            });

        table!.Checks.Select(c => c.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_CheckConstraintCommentOnlyChanged_EmitsModifyNotRecreate()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0", Comment = "old" }],
            },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0", Comment = "new" }],
            });

        var check = table!.Checks.ShouldHaveSingleItem();
        check.Change.ShouldBe(ChangeKind.Modify);
        check.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_CheckConstraintUnchanged_ProducesNoDiff()
    {
        var table = DiffTable(
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
            },
            new Table
            {
                Name = "users",
                Columns = [new Column { Name = "age", Type = SqlType.Int }],
                CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
            });

        table.ShouldBeNull();
    }

    [Fact]
    public void Compare_NewTable_FoldsUniqueAndCheckConstraintsAsAdds()
    {
        // On a new table the primary key is created inline (carried on Definition), but unique and check
        // constraints arrive as separate adds, mirroring how foreign keys fold.
        var desired = new Table
        {
            Name = "users",
            Columns = [new Column { Name = "email", Type = SqlType.Text }, new Column { Name = "age", Type = SqlType.Int }],
            UniqueConstraints = [new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }],
            CheckConstraints = [new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }],
        };

        var table = Compare(Db(new Schema { Name = "app" }),
            Db(new Schema { Name = "app", Tables = [desired] })).Schemas.Single().Tables.Single();

        table.Change.ShouldBe(ChangeKind.Add);
        table.UniqueConstraints.ShouldHaveSingleItem().ShouldBe(
            UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] }));
        table.Checks.ShouldHaveSingleItem().ShouldBe(
            CheckConstraintDiff.Added(new CheckConstraint { Name = "users_age_chk", Expression = "age >= 0" }));
    }

    // -------------------------------------------------------------------------
    // Indexes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IndexRemoved_EmitsRemove()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "users_email_ix", Columns = ["email"] }] },
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }] });

        table!.Indexes.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_IndexDefinitionChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "users_email_ix", Columns = ["email"] }] },
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "users_email_ix", Columns = ["email"], IsUnique = true }] });

        table!.Indexes.Select(i => i.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexCommentOnlyChange_EmitsModify()
    {
        var table = DiffTable(
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "users_email_ix", Columns = ["email"], Comment = "old" }] },
            new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "users_email_ix", Columns = ["email"], Comment = "new" }] });

        var index = table!.Indexes.ShouldHaveSingleItem();
        index.Change.ShouldBe(ChangeKind.Modify);
        index.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_IndexMethodChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = "t", Columns = [new Column { Name = "tags", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "t_tags_ix", Columns = ["tags"] }] },
            new Table { Name = "t", Columns = [new Column { Name = "tags", Type = SqlType.Text }], Indexes = [new TableIndex { Name = "t_tags_ix", Columns = ["tags"], Method = "gin" }] });

        table!.Indexes.Select(i => i.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexIncludeChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = "t", Columns = [new Column { Name = "a", Type = SqlType.Int }, new Column { Name = "b", Type = SqlType.Int }], Indexes = [new TableIndex { Name = "t_ix", Columns = ["a"] }] },
            new Table { Name = "t", Columns = [new Column { Name = "a", Type = SqlType.Int }, new Column { Name = "b", Type = SqlType.Int }], Indexes = [new TableIndex { Name = "t_ix", Columns = ["a"], Include = ["b"] }] });

        table!.Indexes.Select(i => i.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    [Fact]
    public void Compare_IndexKeyOrderingChanged_EmitsRemoveThenAdd()
    {
        var table = DiffTable(
            new Table { Name = "t", Columns = [new Column { Name = "a", Type = SqlType.Int }], Indexes = [new TableIndex { Name = "t_ix", Columns = ["a"] }] },
            new Table { Name = "t", Columns = [new Column { Name = "a", Type = SqlType.Int }], Indexes = [new TableIndex { Name = "t_ix", Columns = [new IndexColumn("a", Sort: IndexSort.Descending)] }] });

        table!.Indexes.Select(i => i.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);
    }

    // -------------------------------------------------------------------------
    // Exclusion constraints
    // -------------------------------------------------------------------------

    private static ExclusionConstraint NoOverlap(string? method = "gist", string? comment = null) =>
        new ExclusionConstraint { Name = "no_overlap", Elements = [new ExclusionElement("=", "room"), new ExclusionElement("&&", "during")], Method = method, Comment = comment };

    private static Table Bookings(params ExclusionConstraint[] exclusions) =>
        new Table { Name = "bookings", Columns = [new Column { Name = "room", Type = SqlType.Int }, new Column { Name = "during", Type = SqlType.Int }], ExclusionConstraints = [.. exclusions] };

    [Fact]
    public void Compare_ExclusionAdded_EmitsAdd()
    {
        // Act
        var table = DiffTable(Bookings(), Bookings(NoOverlap()));

        // Assert
        var exclusion = table!.ExclusionConstraints.ShouldHaveSingleItem();
        exclusion.Change.ShouldBe(ChangeKind.Add);
        exclusion.Definition!.Method.ShouldBe("gist");
    }

    [Fact]
    public void Compare_ExclusionDropped_EmitsRemove()
        => DiffTable(Bookings(NoOverlap()), Bookings())!
            .ExclusionConstraints.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Remove);

    [Fact]
    public void Compare_ExclusionUnchanged_ProducesNoDiff()
        => DiffTable(Bookings(NoOverlap()), Bookings(NoOverlap())).ShouldBeNull();

    [Fact]
    public void Compare_ExclusionDefinitionChanged_EmitsRemoveThenAdd()
        => DiffTable(Bookings(NoOverlap(method: "gist")), Bookings(NoOverlap(method: "spgist")))!
            .ExclusionConstraints.Select(e => e.Change).ShouldBe([ChangeKind.Remove, ChangeKind.Add]);

    [Fact]
    public void Compare_ExclusionCommentOnlyChange_EmitsModify()
    {
        // Act
        var exclusion = DiffTable(Bookings(NoOverlap(comment: "old")), Bookings(NoOverlap(comment: "new")))!

        // Assert
            .ExclusionConstraints.ShouldHaveSingleItem();
        exclusion.Change.ShouldBe(ChangeKind.Modify);
        exclusion.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }
}
