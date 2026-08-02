using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Diff.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Nsql;
using DatabaseComparer = NSchema.Diff.Domain.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// Covers the structured-diff projection the comparer now produces directly (formerly the responsibility of
/// DefaultDiffBuilder), driven from realistic schema inputs.
/// </summary>
public partial class DatabaseComparerTests
{
    private readonly DatabaseComparer _sut = new(NullLogger<DatabaseComparer>.Instance, new SqlEquivalence());

    private static Database Db(params Schema[] schemas) => new Database { Schemas = [.. schemas] };

    /// <summary>
    /// Compares two observations, optionally steered by directives (none = drift-style compare), running the
    /// full Align → Compare → Decorate pipeline the project comparer orchestrates.
    /// </summary>
    private DatabaseDiff Compare(Database current, Database desired, ProjectDirectives? directives = null)
    {
        var effective = directives ?? ProjectDirectives.Empty;
        var aligned = DatabaseAligner.Align(current, desired, effective);
        var diff = _sut.Compare(aligned.Require(), desired);
        return ChangeScriptDecorator.Decorate(diff, effective.ChangeScripts).Require();
    }

    /// <summary>Diffs two single-table <c>app</c> schemas, returning the table diff (null when unchanged).</summary>
    private TableDiff? DiffTable(Table current, Table desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", Tables = [current] }), Db(new Schema { Name = "app", Tables = [desired] }), directives)
        .Schemas.SingleOrDefault()?.Tables.SingleOrDefault();

    /// <summary>Diffs two single-column <c>app.t</c> tables, returning the column diff (null when unchanged).</summary>
    private ColumnDiff? DiffColumn(Column current, Column desired, ProjectDirectives? directives = null) =>
        DiffTable(new Table { Name = "t", Columns = [current] }, new Table { Name = "t", Columns = [desired] }, directives)?.Columns.SingleOrDefault();

    /// <summary>Diffs two <c>app</c> schemas holding the given views, returning the single view diff (null when unchanged).</summary>
    private ViewDiff? DiffViews(IReadOnlyList<View> current, IReadOnlyList<View> desired, ProjectDirectives? directives = null) =>
        Compare(Db(new Schema { Name = "app", Views = [.. current] }), Db(new Schema { Name = "app", Views = [.. desired] }), directives)
        .Schemas.SingleOrDefault()?.Views.SingleOrDefault();

    /// <summary>Directives renaming table <c>app.&lt;from&gt;</c> to <paramref name="to"/>.</summary>
    private static ProjectDirectives TableRename(string from, string to) =>
        new(ObjectRenames: [new ObjectRenameDirective(ObjectAddress.Table("app", from), to)]);

    /// <summary>Directives renaming column <c>app.t.&lt;from&gt;</c> to <paramref name="to"/>.</summary>
    private static ProjectDirectives ColumnRename(string from, string to, string table = "t") =>
        new(MemberRenames: [new MemberRenameDirective(new MemberAddress("app", table, from), to)]);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null) =>
        new View { Name = name, Body = body, DependsOn = ViewDependencyExtractor.Extract(body, "app"), Comment = comment };

    [Fact]
    public void Compare_BothEmpty_ProducesEmptyDiff()
    {
        // Act
        var diff = Compare(Db(), Db());

        // Assert
        diff.IsEmpty.ShouldBeTrue();
        diff.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_NestsTablesUnderSchema_OrderedByName()
    {
        var current = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "audit_log", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
        ],
        });
        var desired = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "shipped_at", Type = SqlType.DateTimeOffset }] },
        ],
        });

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("app");
        schema.Change.ShouldBe(ChangeKind.Touched); // only its tables changed
        schema.Tables.Select(t => t.Name).ShouldBe(["audit_log", "orders"]); // ordered by name
        schema.Tables.Single(t => t.Name.Value.Equals("orders")).Change.ShouldBe(ChangeKind.Modify);
        schema.Tables.Single(t => t.Name.Value.Equals("audit_log")).Change.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_Summary_CountsEveryChangedElementByKind()
    {
        var current = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
            new Table { Name = "audit_log", Columns = [new Column { Name = "id", Type = SqlType.Int }] },
        ],
        });
        var desired = Db(
            new Schema
            {
                Name = "app",
                Tables = [
                new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "shipped_at", Type = SqlType.DateTimeOffset }] },
            ],
            },
            new Schema { Name = "reporting" });

        // reporting schema (Add) + shipped_at column (Add); orders table (Modify); audit_log table (Remove).
        Compare(current, desired).GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 1, Removed: 1));
    }

    [Fact]
    public void Compare_TableChangeWithoutSchemaChange_LeavesSchemaKindNull()
    {
        var current = Db(new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] });
        var desired = Db(new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "email", Type = SqlType.Text }] }] });

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Change.ShouldBe(ChangeKind.Touched);
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void Compare_CreateTable_AddsEveryColumnWithDefinitionAndFoldedComment()
    {
        // Arrange
        var current = Db(new Schema { Name = "app" });
        var desired = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "users", Columns = [
                new Column { Name = "id", Type = SqlType.Int, IsNullable = false },
                new Column { Name = "email", Type = SqlType.Text, IsNullable = false, Comment = "login" },
            ] },
        ],
        });

        // Act
        var table = Compare(current, desired).Schemas.Single().Tables.Single();

        // Assert
        table.Change.ShouldBe(ChangeKind.Add);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "email"]);
        table.Columns.ShouldAllBe(c => c.Change == ChangeKind.Add && c.Definition != null);
        table.Columns.Single(c => c.Name.Value.Equals("email")).Comment.ShouldBe(new ValueChange<string>(null, "login"));
        table.Columns.Single(c => c.Name.Value.Equals("id")).Comment.ShouldBeNull();
    }

    [Fact]
    public void Compare_MergesMultipleChangesToOneColumnIntoASingleDiff()
    {
        var current = Db(new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text, IsNullable = false, Comment = "old" }] }] });
        var desired = Db(new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "email", Type = SqlType.Text, IsNullable = true, Comment = "new" }] }] });

        var column = Compare(current, desired).Schemas.Single().Tables.Single().Columns.ShouldHaveSingleItem();

        column.Name.ShouldBe("email");
        column.Change.ShouldBe(ChangeKind.Modify);
        column.Nullability.ShouldBe(new ValueChange<bool>(false, true));
        column.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_GroupsIndexesConstraintsAndGrantsUnderTheirTable()
    {
        // Arrange
        ObjectMemberCollection<Column> Columns() => [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "user_id", Type = SqlType.Int }];
        var current = Db(new Schema { Name = "app", Tables = [new Table { Name = "orders", Columns = Columns() }] });
        var desired = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "orders",
                Columns = Columns(),
                PrimaryKey = new PrimaryKey { Name = "orders_pkey", ColumnNames = ["id"] },
                ForeignKeys = [new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] }],
                UniqueConstraints = [new UniqueConstraint { Name = "orders_user_uq", ColumnNames = ["user_id"] }],
                CheckConstraints = [new CheckConstraint { Name = "orders_id_chk", Expression = "id > 0" }],
                Indexes = [new TableIndex { Name = "orders_user_ix", Columns = ["user_id"] }],
                Grants = [new TableGrant("reader", TablePrivilege.Insert)] },
        ],
        });

        // Act
        var table = Compare(current, desired).Schemas.Single().Tables.Single();

        // Assert
        table.PrimaryKeys.Select(c => (c.Change, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_pkey")]);
        table.ForeignKeys.Select(c => (c.Change, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_user_fk")]);
        table.UniqueConstraints.Select(c => (c.Change, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_user_uq")]);
        table.Checks.Select(c => (c.Change, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_id_chk")]);
        table.Indexes.ShouldHaveSingleItem().Name.ShouldBe("orders_user_ix");
        var grant = table.Grants.ShouldHaveSingleItem();
        grant.Role.ShouldBe("reader");
        grant.Privileges.ShouldBe(TablePrivilege.Insert);
    }

    [Fact]
    public void Compare_FoldsSchemaRenameCommentAndGrantsIntoSchemaDiff()
    {
        var current = Db(new Schema { Name = "app_old", Grants = [new SchemaGrant("writer")] });
        var desired = Db(new Schema { Name = "app", Grants = [new SchemaGrant("reader")], Comment = "new comment" });
        var directives = new ProjectDirectives(
            SchemaRenames: [new SchemaRenameDirective(DatabaseAddress.Schema("app_old"), DatabaseAddress.Schema("app"))]);

        var schema = Compare(current, desired, directives).Schemas.ShouldHaveSingleItem();

        schema.Change.ShouldBe(ChangeKind.Modify);
        schema.RenamedFrom.ShouldBe("app_old");
        schema.Comment.ShouldBe(new ValueChange<string>(null, "new comment"));
        schema.Grants.ShouldBe([
            new GrantChange(ChangeKind.Remove, "writer", null),
            new GrantChange(ChangeKind.Add, "reader", null),
        ]);
    }

    // -------------------------------------------------------------------------
    // Schema-level add / remove / sort / no-op
    // -------------------------------------------------------------------------

    [Fact]
    public void Compare_IdenticalSchemas_ProduceNoDiff()
    {
        var schema = new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] };

        Compare(Db(schema), Db(schema)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Compare_SchemaInCurrentButNotDesired_IsRemoved()
    {
        var current = Db(new Schema { Name = "app" }, new Schema { Name = "legacy" });
        var desired = Db(new Schema { Name = "app" });

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Name.ShouldBe("legacy");
        schema.Change.ShouldBe(ChangeKind.Remove);
        schema.Tables.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_RemovedSchemaWithTables_CarriesTheirRemovals()
    {
        // A removed schema must take its contained objects with it (rather than relying on DROP SCHEMA CASCADE), so
        // the diff carries a Remove for each nested table, ordered by name.
        var legacy = new Schema { Name = "legacy", Tables = [new Table { Name = "widgets" }, new Table { Name = "gadgets" }] };
        var current = Db(new Schema { Name = "app" }, legacy);
        var desired = Db(new Schema { Name = "app" });

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Change.ShouldBe(ChangeKind.Remove);
        schema.Tables.Select(t => (t.Name.Value, t.Change)).ShouldBe([("gadgets", ChangeKind.Remove), ("widgets", ChangeKind.Remove)]);
    }

    [Fact]
    public void Compare_NewImplicitSchema_IsFilled_NotCreated()
    {
        // Arrange — nothing declares the schema, so the project expects to find it; only its contents are new.
        var app = new Schema { Name = "app", IsImplicit = true, Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] };

        // Act
        var schema = Compare(Db(), Db(app)).Schemas.ShouldHaveSingleItem();

        // Assert
        schema.Change.ShouldBe(ChangeKind.Touched);
        schema.Tables.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public void Compare_RemovedImplicitSchema_IsEmptied_NotDropped()
    {
        // Arrange — a container the run does not own: its contents are still ours to remove, the schema is not.
        var legacy = new Schema { Name = "legacy", IsImplicit = true, Tables = [new Table { Name = "widgets" }] };

        // Act
        var schema = Compare(Db(legacy), Db()).Schemas.ShouldHaveSingleItem();

        // Assert
        schema.Change.ShouldBe(ChangeKind.Touched);
        schema.Tables.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Remove);
    }

    [Fact]
    public void Compare_NewImplicitSchema_DoesNotApplyItsCommentOrGrants()
    {
        // Arrange — a container the run does not own, carrying settings of its own.
        var app = new Schema
        {
            Name = "app",
            IsImplicit = true,
            Comment = "the database's own comment",
            Grants = [new SchemaGrant("reader")],
            Tables = [new Table { Name = "users" }],
        };

        // Act
        var schema = Compare(Db(), Db(app)).Schemas.ShouldHaveSingleItem();

        // Assert
        schema.Change.ShouldBe(ChangeKind.Touched);
        schema.Comment.ShouldBeNull();
        schema.Grants.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ImplicitSchema_DoesNotReportTheDatabasesOwnCommentAsDrift()
    {
        // Arrange — the engine comments its own schema; the project never declared one, so there is nothing to
        // reconcile. Reporting it would plan to strip a comment the database owns.
        var current = new Schema { Name = "public", IsImplicit = true, Comment = "standard public schema", Tables = [new Table { Name = "users" }] };
        var desired = new Schema { Name = "public", IsImplicit = true, Tables = [new Table { Name = "users" }, new Table { Name = "orders" }] };

        // Act
        var schema = Compare(Db(current), Db(desired)).Schemas.ShouldHaveSingleItem();

        // Assert
        schema.Change.ShouldBe(ChangeKind.Touched);
        schema.Comment.ShouldBeNull();
    }

    [Fact]
    public void Compare_NewSchema_FoldsCommentGrantsAndTablesWithDefinition()
    {
        var current = Db();
        var desired = Db(new Schema
        {
            Name = "reporting",

            Grants = [new SchemaGrant("reader")],
            Tables = [new Table { Name = "metrics", Columns = [new Column { Name = "id", Type = SqlType.Int }] }],
            Comment = "analytics",
        });

        var schema = Compare(current, desired).Schemas.ShouldHaveSingleItem();

        schema.Change.ShouldBe(ChangeKind.Add);
        schema.Comment.ShouldBe(new ValueChange<string>(null, "analytics"));
        schema.Grants.ShouldHaveSingleItem().ShouldBe(new GrantChange(ChangeKind.Add, "reader", null));
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Change.ShouldBe(ChangeKind.Add);
        table.Definition.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_OrdersResultSchemasByName()
    {
        var diff = Compare(Db(), Db(new Schema { Name = "zeta" }, new Schema { Name = "alpha" }));

        diff.Schemas.Select(s => s.Name).ShouldBe(["alpha", "zeta"]);
    }

    [Fact]
    public void Compare_CaseVariantNames_AreDifferentObjects()
    {
        // Arrange
        // Identifiers are case-sensitive: an introspected "Users" and a declared "users" are different
        // tables (the planner warns about the near-miss before the diff turns it into a create).
        var current = Db(new Schema { Name = "App", Tables = [new Table { Name = "Users", Columns = [new Column { Name = "ID", Type = SqlType.Int }] }] });
        var desired = Db(new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] });

        // Act
        var diff = Compare(current, desired);

        // Assert
        diff.IsEmpty.ShouldBeFalse();
        diff.Schemas.Select(s => s.Name.Value).ShouldBe(["App", "app"], ignoreOrder: true);
    }

    [Fact]
    public void Compare_CaseVariantColumnReferences_AreAChange()
    {
        // Arrange
        // References inside definitions (primary-key and index column lists) are identifiers too, so a
        // casing difference between the introspected and declared spelling is a change.
        Table Build(string id, string email) => new Table
        {
            Name = "users",
            Columns = [new Column { Name = id, Type = SqlType.Int }, new Column { Name = email, Type = SqlType.Text }],
            PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = [id] },
            Indexes = [new TableIndex { Name = "users_email_ix", Columns = [email] }],
        };

        // Act
        var diff = DiffTable(Build("ID", "Email"), Build("id", "email"));

        // Assert
        diff.ShouldNotBeNull();
    }

    [Fact]
    public void Compare_DirectivesUnderASchemaRename_AddressCurrentNames()
    {
        // Everything is renamed at once: schema sales→core, table users→people, column name→full_name.
        // Every directive addresses current reality (sales.users.name), and the comparer resolves the nested
        // lookups by the current names of each matched pair.
        var current = Db(new Schema { Name = "sales", Tables = [new Table { Name = "users", Columns = [new Column { Name = "name", Type = SqlType.Text }] }] });
        var desired = Db(new Schema { Name = "core", Tables = [new Table { Name = "people", Columns = [new Column { Name = "full_name", Type = SqlType.Text }] }] });
        SqlIdentifier sales = "sales";
        var directives = new ProjectDirectives(
            SchemaRenames: [new SchemaRenameDirective(DatabaseAddress.Schema(sales), DatabaseAddress.Schema("core"))],
            ObjectRenames: [new ObjectRenameDirective(ObjectAddress.Table(sales, "users"), "people")],
            MemberRenames: [new MemberRenameDirective(new MemberAddress(sales, "users", "name"), "full_name")]);

        var schema = Compare(current, desired, directives).Schemas.ShouldHaveSingleItem();

        schema.RenamedFrom.ShouldBe("sales");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.RenamedFrom.ShouldBe("users");
        var column = table.Columns.ShouldHaveSingleItem();
        column.RenamedFrom.ShouldBe("name");
        column.Name.ShouldBe("full_name");
    }
}
