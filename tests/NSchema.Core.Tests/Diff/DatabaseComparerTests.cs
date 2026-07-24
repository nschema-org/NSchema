using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Backends;
using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Services;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql;
using DatabaseComparer = NSchema.Diff.Model.Services.DatabaseComparer;

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

    /// <summary>An address in the <c>app</c> schema.</summary>
    private static ObjectAddress App(string name) => new("app", name);

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
        new(ObjectRenames: [new ObjectRenameDirective(App(from) with { Kind = ObjectKind.Table }, to)]);

    /// <summary>Directives renaming column <c>app.t.&lt;from&gt;</c> to <paramref name="to"/>.</summary>
    private static ProjectDirectives ColumnRename(string from, string to, string table = "t") =>
        new(MemberRenames: [new MemberRenameDirective(new MemberAddress("app", table, from), to)]);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null) =>
        new View { Name = name, Body = body, DependsOn = ViewDependencyExtractor.Extract(body, "app"), Comment = comment };

    [Fact]
    public void Compare_BothEmpty_ProducesEmptyDiff()
    {
        var diff = Compare(Db(), Db());

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
        schema.Kind.ShouldBeNull(); // only its tables changed
        schema.Tables.Select(t => t.Name).ShouldBe(["audit_log", "orders"]); // ordered by name
        schema.Tables.Single(t => t.Name.Value.Equals("orders")).Kind.ShouldBe(ChangeKind.Modify);
        schema.Tables.Single(t => t.Name.Value.Equals("audit_log")).Kind.ShouldBe(ChangeKind.Remove);
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

        schema.Kind.ShouldBeNull();
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void Compare_CreateTable_AddsEveryColumnWithDefinitionAndFoldedComment()
    {
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

        var table = Compare(current, desired).Schemas.Single().Tables.Single();

        table.Kind.ShouldBe(ChangeKind.Add);
        table.Columns.Select(c => c.Name).ShouldBe(["id", "email"]);
        table.Columns.ShouldAllBe(c => c.Kind == ChangeKind.Add && c.Definition != null);
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
        column.Kind.ShouldBe(ChangeKind.Modify);
        column.Nullability.ShouldBe(new ValueChange<bool>(false, true));
        column.Comment.ShouldBe(new ValueChange<string>("old", "new"));
    }

    [Fact]
    public void Compare_GroupsIndexesConstraintsAndGrantsUnderTheirTable()
    {
        DatabaseMemberCollection<Column> Columns() => [new Column { Name = "id", Type = SqlType.Int }, new Column { Name = "user_id", Type = SqlType.Int }];
        var current = Db(new Schema { Name = "app", Tables = [new Table { Name = "orders", Columns = Columns() }] });
        var desired = Db(new Schema
        {
            Name = "app",
            Tables = [
            new Table { Name = "orders",
                Columns = Columns(),
                PrimaryKey = new PrimaryKey { Name = "orders_pkey", ColumnNames = ["id"] },
                ForeignKeys = [new ForeignKey { Name = "orders_user_fk", ColumnNames = ["user_id"], References = new("app", "users"), ReferencedColumnNames = ["id"] }],
                UniqueConstraints = [new UniqueConstraint { Name = "orders_user_uq", ColumnNames = ["user_id"] }],
                CheckConstraints = [new CheckConstraint { Name = "orders_id_chk", Expression = "id > 0" }],
                Indexes = [new TableIndex { Name = "orders_user_ix", Columns = ["user_id"] }],
                Grants = [new TableGrant("reader", TablePrivilege.Insert)] },
        ],
        });

        var table = Compare(current, desired).Schemas.Single().Tables.Single();

        table.PrimaryKey.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_pkey")]);
        table.ForeignKeys.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_user_fk")]);
        table.UniqueConstraints.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_user_uq")]);
        table.Checks.Select(c => (c.Kind, c.Name.Value)).ShouldBe([(ChangeKind.Add, "orders_id_chk")]);
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
            SchemaRenames: [new SchemaRenameDirective(new SchemaAddress("app_old"), new SchemaAddress("app"))]);

        var schema = Compare(current, desired, directives).Schemas.ShouldHaveSingleItem();

        schema.Kind.ShouldBe(ChangeKind.Modify);
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
        schema.Kind.ShouldBe(ChangeKind.Remove);
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

        schema.Kind.ShouldBe(ChangeKind.Remove);
        schema.Tables.Select(t => (t.Name.Value, t.Kind)).ShouldBe([("gadgets", ChangeKind.Remove), ("widgets", ChangeKind.Remove)]);
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

        schema.Kind.ShouldBe(ChangeKind.Add);
        schema.Comment.ShouldBe(new ValueChange<string>(null, "analytics"));
        schema.Grants.ShouldHaveSingleItem().ShouldBe(new GrantChange(ChangeKind.Add, "reader", null));
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Kind.ShouldBe(ChangeKind.Add);
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
        // Identifiers are case-sensitive: an introspected "Users" and a declared "users" are different
        // tables (the planner warns about the near-miss before the diff turns it into a create).
        var current = Db(new Schema { Name = "App", Tables = [new Table { Name = "Users", Columns = [new Column { Name = "ID", Type = SqlType.Int }] }] });
        var desired = Db(new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] });

        var diff = Compare(current, desired);

        diff.IsEmpty.ShouldBeFalse();
        diff.Schemas.Select(s => s.Name.Value).ShouldBe(["App", "app"], ignoreOrder: true);
    }

    [Fact]
    public void Compare_CaseVariantColumnReferences_AreAChange()
    {
        // References inside definitions (primary-key and index column lists) are identifiers too, so a
        // casing difference between the introspected and declared spelling is a change.
        Table Build(string id, string email) => new Table
        {
            Name = "users",
            Columns = [new Column { Name = id, Type = SqlType.Int }, new Column { Name = email, Type = SqlType.Text }],
            PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = [id] },
            Indexes = [new TableIndex { Name = "users_email_ix", Columns = [email] }],
        };

        var diff = DiffTable(Build("ID", "Email"), Build("id", "email"));

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
            SchemaRenames: [new SchemaRenameDirective(new SchemaAddress(sales), new SchemaAddress("core"))],
            ObjectRenames: [new ObjectRenameDirective(new ObjectAddress(sales, "users") with { Kind = ObjectKind.Table }, "people")],
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
