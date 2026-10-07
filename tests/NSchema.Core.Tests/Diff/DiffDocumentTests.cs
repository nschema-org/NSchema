using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Publications;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Publications;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Diff;

public sealed class DiffDocumentTests
{
    // -------------------------------------------------------------------------
    // Helpers — read a diff and assert over the structured document it produces.
    // -------------------------------------------------------------------------

    /// <summary>Asserts a content line exists with the given kind whose text contains the snippet.</summary>
    private static void ShouldHaveLine(DatabaseDiff diff, ChangeKind kind, string textContains)
        => DiffDocument.From(diff).Lines.ShouldContain(line => line.Change == kind && line.Text.Contains(textContains));

    private static DatabaseDiff DiffOf(IReadOnlyList<SchemaDiff>? schemas = null) => new(schemas ?? []);

    private static SchemaDiff Schema(
        string name,
        ChangeKind? kind = null,
        SqlIdentifier? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<TableDiff>? tables = null
    ) => SchemaForKind(kind, name) with
    {
        RenamedFrom = renamedFrom,
        Comment = comment,
        Grants = grants ?? [],
        Tables = tables ?? [],
    };

    /// <summary>The empty diff for a schema kind (null = the schema itself is untouched).</summary>
    private static SchemaDiff SchemaForKind(ChangeKind? kind, string name) => kind switch
    {
        ChangeKind.Add => SchemaDiff.Added(name),
        ChangeKind.Remove => SchemaDiff.Removed(name),
        ChangeKind.Modify => SchemaDiff.Modified(name),
        _ => SchemaDiff.Containing(name),
    };

    private static TableDiff Table(
        string name,
        ChangeKind kind,
        string schema = "app",
        SqlIdentifier? renamedFrom = null,
        ValueChange<string>? comment = null,
        IReadOnlyList<ColumnDiff>? columns = null,
        IReadOnlyList<GrantChange>? grants = null,
        IReadOnlyList<IndexDiff>? indexes = null,
        IReadOnlyList<PrimaryKeyDiff>? primaryKey = null,
        IReadOnlyList<ForeignKeyDiff>? foreignKeys = null,
        IReadOnlyList<UniqueConstraintDiff>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraintDiff>? checks = null)
        => ForKind(kind, schema, name) with
        {
            RenamedFrom = renamedFrom,
            Comment = comment,
            Columns = columns ?? [],
            Grants = grants ?? [],
            Indexes = indexes ?? [],
            PrimaryKeys = primaryKey ?? [],
            ForeignKeys = foreignKeys ?? [],
            UniqueConstraints = uniqueConstraints ?? [],
            Checks = checks ?? [],
        };

    /// <summary>The empty diff for a kind, so a test can then set only the members it cares about.</summary>
    private static TableDiff ForKind(ChangeKind kind, string schema, string name) => kind switch
    {
        ChangeKind.Add => TableDiff.Added(schema, new Table { Name = name }),
        ChangeKind.Remove => TableDiff.Removed(schema, name),
        _ => TableDiff.Modified(schema, name),
    };

    private static ColumnDiff AddColumn(Column definition, ValueChange<string>? comment = null)
        => comment is null ? ColumnDiff.Added(definition) : ColumnDiff.Added(definition) with { Comment = comment };

    private static ColumnDiff RemoveColumn(Column definition) => ColumnDiff.Removed(definition);

    private static ColumnDiff ModifyColumn(
        string name,
        SqlIdentifier? renamedFrom = null,
        ValueChange<SqlType>? type = null,
        ValueChange<bool>? nullability = null,
        ValueChange<SqlDefaultExpression>? @default = null,
        ValueChange<IdentityOptions>? identity = null,
        ValueChange<string>? comment = null)
        => ColumnDiff.Modified(new Column
        {
            Name = name,
            Type = type?.New ?? SqlType.Text,
            IsNullable = nullability?.New ?? false,
        }) with
        {
            RenamedFrom = renamedFrom,
            Type = type,
            Nullability = nullability,
            Default = @default,
            Identity = identity,
            Comment = comment,
        };

    /// <summary>Wraps a single table-changing schema (null schema kind) for brevity.</summary>
    private static DatabaseDiff WithTable(TableDiff table)
        => DiffOf([Schema("app", tables: [table])]);

    /// <summary>Wraps a single view-changing schema (null schema kind) for brevity.</summary>
    private static DatabaseDiff WithView(ViewDiff view)
        => DiffOf([SchemaDiff.Containing("app") with { Views = [view] }]);

    // -------------------------------------------------------------------------
    // Empty / summary
    // -------------------------------------------------------------------------

    [Fact]
    public void From_EmptyDiff_IsEmptyWithZeroSummary()
    {
        // Act
        var document = DiffDocument.From(DiffOf());

        // Assert
        document.IsEmpty.ShouldBeTrue();
        document.Lines.ShouldBeEmpty();
        document.Summary.ShouldBe(new DiffSummary(0, 0, 0));
    }

    [Fact]
    public void From_PopulatesSummaryCounts()
    {
        var diff = DiffOf(
        [
            Schema("new_schema", ChangeKind.Add),
            Schema("app", tables:
            [
                Table("orders", ChangeKind.Modify, columns: [ModifyColumn("total", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt))]),
                Table("audit", ChangeKind.Remove),
            ]),
        ]);

        // new_schema (add); orders table + its total column (modify ×2); audit table (remove).
        DiffDocument.From(diff).Summary.ShouldBe(new DiffSummary(1, 2, 1));
    }

    // -------------------------------------------------------------------------
    // Schema
    // -------------------------------------------------------------------------

    [Fact]
    public void From_SchemaAdd_EmitsAddHeader()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Add)]), ChangeKind.Add, "schema app");

    [Fact]
    public void From_SchemaRemove_EmitsRemoveHeader()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Remove)]), ChangeKind.Remove, "schema app");

    [Fact]
    public void From_SchemaRename_EmitsArrow()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Modify, renamedFrom: "legacy")]), ChangeKind.Modify, "schema legacy → app");

    [Fact]
    public void From_SchemaComment_AppendsNewCommentSuffix()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Add, comment: new ValueChange<string>(null, "primary"))]), ChangeKind.Add, "schema app (\"primary\")");

    [Fact]
    public void From_SchemaWithNullKind_SkipsHeaderButEmitsTables()
    {
        // Act
        var lines = DiffDocument.From(WithTable(Table("users", ChangeKind.Add))).Lines;

        // Assert
        lines.ShouldNotContain(line => line.Text.Contains("schema app"));
        lines.ShouldContain(line => line.Change == ChangeKind.Add && line.Text == "table app.users");
    }

    [Fact]
    public void From_SchemaGrantAdd_EmitsGrantUsage()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Add, grants: [new GrantChange(ChangeKind.Add, "reader", null)])]), ChangeKind.Add, "grant usage to reader");

    [Fact]
    public void From_SchemaGrantRemove_EmitsRevokeUsage()
        => ShouldHaveLine(DiffOf([Schema("app", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, "reader", null)])]), ChangeKind.Remove, "revoke usage from reader");

    // -------------------------------------------------------------------------
    // Table
    // -------------------------------------------------------------------------

    [Fact]
    public void From_TableAdd_EmitsSchemaObjectAddress()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Add)), ChangeKind.Add, "table app.users");

    [Fact]
    public void From_TableRename_EmitsArrowWithSchemaQualifier()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, renamedFrom: "people")), ChangeKind.Modify, "table app.people → users");

    [Fact]
    public void From_TableComment_AppendsChangedCommentSuffix()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, comment: new ValueChange<string>("old", "new"))), ChangeKind.Modify, "table app.users (\"old\" → \"new\")");

    [Fact]
    public void From_AddedTable_SeparatesColumnBlockFromTrailingBlockWithSpacer()
    {
        // Arrange
        var table = Table("users", ChangeKind.Add,
            columns: [AddColumn(new Column { Name = "id", Type = SqlType.Int })],
            indexes: [IndexDiff.Added(new TableIndex { Name = "users_id_ix", Columns = ["id"] })]);

        var lines = DiffDocument.From(WithTable(table)).Lines;
        var columnIndex = IndexOf(lines, line => line.Text.Contains("id int not null"));
        var indexIndex = IndexOf(lines, line => line.Text.Contains("index users_id_ix"));

        columnIndex.ShouldBeGreaterThanOrEqualTo(0);
        indexIndex.ShouldBeGreaterThan(columnIndex);

        // Act
        // Exactly one kindless spacer line separates the column block from the trailing index block.
        var between = lines.Skip(columnIndex + 1).Take(indexIndex - columnIndex - 1).ToList();

        // Assert
        between.ShouldHaveSingleItem().Change.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Columns
    // -------------------------------------------------------------------------

    [Fact]
    public void From_ColumnAdd_EmitsDefinitionAndCommentSuffix()
    {
        var column = AddColumn(new Column { Name = "id", Type = SqlType.Int }, comment: new ValueChange<string>(null, "identifier"));

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Add, columns: [column])), ChangeKind.Add, "id int not null (\"identifier\")");
    }

    [Fact]
    public void From_ColumnAdd_NullableEmitsNull()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Add, columns: [AddColumn(new Column { Name = "bio", Type = SqlType.Text, IsNullable = true })])), ChangeKind.Add, "bio text null");

    [Fact]
    public void From_ColumnRemove_EmitsDefinition()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, columns: [RemoveColumn(new Column { Name = "id", Type = SqlType.Int })])), ChangeKind.Remove, "id int not null");

    [Fact]
    public void From_ColumnRename_EmitsArrow()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, columns: [ModifyColumn("email", renamedFrom: "mail")])), ChangeKind.Modify, "rename column: mail → email");

    [Fact]
    public void From_ColumnTypeChange_EmitsOldToNew()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("total", type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt))])), ChangeKind.Modify, "total type: int → bigint");

    [Fact]
    public void From_ColumnNullabilityChange_EmitsWords()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("email", nullability: new ValueChange<bool>(false, true))])), ChangeKind.Modify, "email nullable: not null → null");

    [Fact]
    public void From_ColumnDefaultChange_EmitsNoneForNull()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("status", @default: new ValueChange<SqlDefaultExpression>(null, "'active'"))])), ChangeKind.Modify, "status default: <none> → 'active'");

    [Fact]
    public void From_ColumnIdentityChange_EmitsOptionParts()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(1, 1, 2)))])), ChangeKind.Modify, "id identity: <none> → start=1, min=1, step=2");

    [Fact]
    public void From_ColumnIdentityChange_EmitsDefaultWhenNoParts()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", identity: new ValueChange<IdentityOptions>(null, new IdentityOptions(null, null, null)))])), ChangeKind.Modify, "id identity: <none> → <default>");

    [Fact]
    public void From_ColumnCommentChange_EmitsQuotedValues()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify,
                columns: [ModifyColumn("id", comment: new ValueChange<string>("old", "new"))])), ChangeKind.Modify, "id comment: \"old\" → \"new\"");

    [Fact]
    public void From_ColumnWithMultipleChanges_EmitsEachOnItsOwnLine()
    {
        var column = ModifyColumn("email",
            type: new ValueChange<SqlType>(SqlType.VarChar(50), SqlType.Text),
            nullability: new ValueChange<bool>(true, false));

        var diff = WithTable(Table("users", ChangeKind.Modify, columns: [column]));

        ShouldHaveLine(diff, ChangeKind.Modify, "email type: varchar(50) → text");
        ShouldHaveLine(diff, ChangeKind.Modify, "email nullable: null → not null");
    }

    // -------------------------------------------------------------------------
    // Constraints, indexes, table grants
    // -------------------------------------------------------------------------

    [Fact]
    public void From_PrimaryKeyConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, primaryKey: [PrimaryKeyDiff.Added(new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] })])), ChangeKind.Add, "primary key users_pkey");

    [Fact]
    public void From_ForeignKeyConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("orders", ChangeKind.Modify, foreignKeys: [ForeignKeyDiff.Removed("orders_user_fk")])), ChangeKind.Remove, "foreign key orders_user_fk");

    [Fact]
    public void From_UniqueConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, uniqueConstraints: [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })])), ChangeKind.Add, "unique constraint users_email_uq");

    [Fact]
    public void From_CheckConstraint_EmitsLabel()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, checks: [CheckConstraintDiff.Removed("users_age_chk")])), ChangeKind.Remove, "check constraint users_age_chk");

    [Fact]
    public void From_ConstraintCommentChange_EmitsCommentDiff()
    {
        var unique = UniqueConstraintDiff.CommentChanged("users_email_uq", new ValueChange<string>("old", "new"));

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, uniqueConstraints: [unique])), ChangeKind.Modify, "unique constraint users_email_uq comment: \"old\" → \"new\"");
    }

    [Fact]
    public void From_IndexAdd_EmitsName()
    {
        var index = IndexDiff.Added(new TableIndex { Name = "users_email_ux", Columns = ["email"], IsUnique = true });

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, indexes: [index])), ChangeKind.Add, "index users_email_ux");
    }

    [Fact]
    public void From_IndexCommentModify_EmitsOldToNew()
    {
        var index = IndexDiff.CommentChanged("users_email_ux", new ValueChange<string>(null, "speed"));

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, indexes: [index])), ChangeKind.Modify, "index users_email_ux comment: <none> → \"speed\"");
    }

    [Fact]
    public void From_TableGrantAdd_EmitsPrivilegeAndRole()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Add, "reader", TablePrivilege.Insert)])), ChangeKind.Add, "grant INSERT to reader");

    [Fact]
    public void From_TableGrantRemove_EmitsPrivilegeAndRole()
        => ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, grants: [new GrantChange(ChangeKind.Remove, "reader", TablePrivilege.Insert)])), ChangeKind.Remove, "revoke INSERT from reader");

    [Theory]
    // Select and its alias ReadOnly share value 1, so a single case proves the alias renders as "SELECT".
    [InlineData(TablePrivilege.Select, "SELECT")]
    [InlineData(TablePrivilege.AppendOnly, "SELECT, INSERT")]  // composite
    [InlineData(TablePrivilege.Select | TablePrivilege.Delete, "SELECT, DELETE")]
    [InlineData(TablePrivilege.All, "SELECT, INSERT, UPDATE, DELETE")]
    [InlineData(TablePrivilege.None, "no privileges")]
    public void From_TableGrant_DecomposesPrivilegeFlags(TablePrivilege privileges, string expected)
    {
        var grant = new GrantChange(ChangeKind.Add, "reader", privileges);

        ShouldHaveLine(WithTable(Table("users", ChangeKind.Modify, grants: [grant])), ChangeKind.Add, $"grant {expected} to reader");
    }

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void From_ViewAdd_EmitsSchemaObjectAddress()
        => ShouldHaveLine(WithView(ViewDiff.Added("app", new View { Name = "active_users", Body = "SELECT 1" })), ChangeKind.Add, "view app.active_users");

    [Fact]
    public void From_ViewAdd_AppendsCommentSuffix()
        => ShouldHaveLine(WithView(ViewDiff.Added("app", new View { Name = "active_users", Body = "SELECT 1" })
                with
        { Comment = new ValueChange<string>(null, "active") }), ChangeKind.Add, "view app.active_users (\"active\")");

    [Fact]
    public void From_ViewBodyReplace_EmitsModifyHeader()
        => ShouldHaveLine(WithView(ViewDiff.Modified("app", "daily_totals") with { Definition = new View { Name = "daily_totals", Body = "SELECT sum(x) FROM app.sales" } }), ChangeKind.Modify, "view app.daily_totals");

    [Fact]
    public void From_ViewCommentOnlyChange_EmitsCommentDiff()
        => ShouldHaveLine(WithView(ViewDiff.Modified("app", "summary") with { Comment = new ValueChange<string>("old", "new") }), ChangeKind.Modify, "view app.summary comment: \"old\" → \"new\"");

    [Fact]
    public void From_ViewRename_EmitsArrowWithSchemaQualifier()
        => ShouldHaveLine(WithView(ViewDiff.Modified("app", "report") with { RenamedFrom = "legacy_report" }), ChangeKind.Modify, "view app.legacy_report → report");

    [Fact]
    public void From_ViewToMaterializedFlip_EmitsLabelTransition()
        => ShouldHaveLine(WithView(ViewDiff.Modified("app", "totals") with
        {
            Definition = new View { Name = "totals", Body = "SELECT 1", IsMaterialized = true },
            IsMaterialized = true,
            Materialized = new ValueChange<bool>(false, true),
            RequiresRecreate = true,
        }),
            ChangeKind.Modify, "view → materialized view app.totals");

    [Fact]
    public void From_MaterializedToViewFlip_EmitsLabelTransition()
        => ShouldHaveLine(WithView(ViewDiff.Modified("app", "totals") with
        {
            Materialized = new ValueChange<bool>(true, false),
            RequiresRecreate = true,
        }),
            ChangeKind.Modify, "materialized view → view app.totals");

    [Fact]
    public void From_ViewRemove_EmitsRemoveHeader()
        => ShouldHaveLine(WithView(ViewDiff.Removed("app", "stale_view")), ChangeKind.Remove, "view app.stale_view");

    // -------------------------------------------------------------------------
    // Document shape
    // -------------------------------------------------------------------------

    [Fact]
    public void From_CarriesChangeKindOnContentLines_WithoutMarkersInText()
    {
        // Arrange
        var diff = WithTable(Table("users", ChangeKind.Add, columns: [AddColumn(new Column { Name = "id", Type = SqlType.Int })]));

        var document = DiffDocument.From(diff);

        // The table header is a depth-0 line tagged Add; its text carries no marker glyph or indentation.
        var header = document.Lines.Single(line => line.Text.StartsWith("table "));
        header.Change.ShouldBe(ChangeKind.Add);
        header.Depth.ShouldBe(0);
        header.Text.ShouldBe("table app.users");

        // Act
        // The column is a detail beneath it: same kind, one level deeper, still marker-free.
        var column = document.Lines.Single(line => line.Text.StartsWith("id "));

        // Assert
        column.Change.ShouldBe(ChangeKind.Add);
        column.Depth.ShouldBe(1);
        column.Text.ShouldNotContain("+");
    }

    [Fact]
    public void From_SpacerLinesAreKindlessAndEmpty()
    {
        // Act
        var document = DiffDocument.From(DiffOf([Schema("app", ChangeKind.Add)]));

        // Assert
        // Every blank spacer is a kindless, empty line — a formatter renders or ignores it as it sees fit.
        document.Lines.Where(line => line.Change is null).ShouldAllBe(line => line.Text == "");
    }

    // -------------------------------------------------------------------------
    // Replica identity
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null, ReplicaIdentityKind.Full, null, "replica identity: default → full")]
    [InlineData(ReplicaIdentityKind.Full, ReplicaIdentityKind.Nothing, null, "replica identity: full → nothing")]
    [InlineData(ReplicaIdentityKind.Nothing, ReplicaIdentityKind.Index, "ux_orders", "replica identity: nothing → using index ux_orders")]
    public void From_ReplicaIdentityChange_EmitsOldToNew(ReplicaIdentityKind? from, ReplicaIdentityKind to, string? index, string expected)
    {
        // Arrange
        var old = from is { } kind ? new ReplicaIdentity(kind) : null;
        var table = Table("orders", ChangeKind.Modify) with { ReplicaIdentity = new ValueChange<ReplicaIdentity>(old, new ReplicaIdentity(to, index)) };

        // Act & Assert
        ShouldHaveLine(WithTable(table), ChangeKind.Modify, expected);
    }

    [Fact]
    public void From_ReplicaIdentityResetToDefault_SaysDefault()
        => ShouldHaveLine(
            WithTable(Table("orders", ChangeKind.Modify) with { ReplicaIdentity = new ValueChange<ReplicaIdentity>(ReplicaIdentity.Full, null) }),
            ChangeKind.Modify, "replica identity: full → default");

    [Fact]
    public void From_AddedTableWithReplicaIdentity_SeparatesItFromTheColumnBlock()
    {
        // Arrange
        var table = Table("orders", ChangeKind.Add, columns: [AddColumn(new Column { Name = "id", Type = SqlType.Int })])
            with
        { ReplicaIdentity = new ValueChange<ReplicaIdentity>(null, ReplicaIdentity.Full) };

        // Act
        var lines = DiffDocument.From(WithTable(table)).Lines;

        // Assert — the identity belongs to the trailing block, after the spacer that closes the columns.
        var spacer = IndexOf(lines, line => line.Change is null);
        spacer.ShouldBeGreaterThan(0);
        IndexOf(lines, line => line.Text.Contains("replica identity")).ShouldBeGreaterThan(spacer);
    }

    // -------------------------------------------------------------------------
    // Publications
    // -------------------------------------------------------------------------

    private static readonly ObjectAddress _orders = new("sales", "orders");

    private static DatabaseDiff WithPublication(PublicationDiff publication) => new([]) { Publications = [publication] };

    [Fact]
    public void From_PublicationAdd_ListsWhatItPublishes()
    {
        // Arrange
        var diff = WithPublication(PublicationDiff.Added(new Publication
        {
            Name = "feed",
            Tables = [new PublishedTable(_orders, ["id", "status"], "status <> 'draft'")],
            Schemas = ["audit"],
            Operations = PublishedOperations.Insert,
        }));

        // Act & Assert
        ShouldHaveLine(diff, ChangeKind.Add, "publication feed");
        ShouldHaveLine(diff, ChangeKind.Add, "table sales.orders (id, status) where (status <> 'draft')");
        ShouldHaveLine(diff, ChangeKind.Add, "tables in schema audit");
        ShouldHaveLine(diff, ChangeKind.Add, "publish: insert");
    }

    [Fact]
    public void From_PublicationAdd_PublishingEverything_OmitsThePublishLine()
        => DiffDocument.From(WithPublication(PublicationDiff.Added(new Publication { Name = "feed" })))
            .Lines.ShouldNotContain(line => line.Text.Contains("publish:"));

    [Fact]
    public void From_PublicationForAllTables_SaysSo()
        => ShouldHaveLine(WithPublication(PublicationDiff.Added(new Publication { Name = "feed", AllTables = true })), ChangeKind.Add, "publication feed for all tables");

    [Fact]
    public void From_PublicationRemove_EmitsOnlyTheHeader()
    {
        // Act
        var lines = DiffDocument.From(WithPublication(PublicationDiff.Removed("feed"))).Lines;

        // Assert
        lines.ShouldHaveSingleItem().ShouldBe(new DiffLine(ChangeKind.Remove, 0, "publication feed"));
    }

    [Fact]
    public void From_PublicationRename_EmitsArrow()
        => ShouldHaveLine(WithPublication(PublicationDiff.Modified("feed") with { RenamedFrom = "legacy_feed" }), ChangeKind.Modify, "publication legacy_feed → feed");

    [Fact]
    public void From_PublicationRecreate_IsLabelledAndListsTheNewDefinition()
    {
        // Arrange
        var diff = WithPublication(PublicationDiff.Recreated(new Publication { Name = "feed", Tables = [new PublishedTable(_orders)] }, wasAllTables: true));

        // Act & Assert
        ShouldHaveLine(diff, ChangeKind.Modify, "publication (recreated) feed");
        ShouldHaveLine(diff, ChangeKind.Add, "table sales.orders");
    }

    [Fact]
    public void From_PublishedTableChanges_EmitOneLineEach()
    {
        // Arrange
        var diff = WithPublication(PublicationDiff.Modified("feed") with
        {
            Tables =
            [
                PublishedTableDiff.Added(new PublishedTable(new ObjectAddress("sales", "customers"))),
                PublishedTableDiff.Removed(new PublishedTable(new ObjectAddress("sales", "order_lines"))),
                PublishedTableDiff.Modified(new PublishedTable(_orders), new PublishedTable(_orders, ["id"])),
            ],
        });

        // Act & Assert
        ShouldHaveLine(diff, ChangeKind.Add, "table sales.customers");
        ShouldHaveLine(diff, ChangeKind.Remove, "table sales.order_lines");
        ShouldHaveLine(diff, ChangeKind.Modify, "table sales.orders → table sales.orders (id)");
    }

    [Fact]
    public void From_PublishedSchemaAndOperationChanges()
    {
        // Arrange
        var diff = WithPublication(PublicationDiff.Modified("feed") with
        {
            Schemas = [new PublishedSchemaChange(ChangeKind.Add, "archive"), new PublishedSchemaChange(ChangeKind.Remove, "audit")],
            Operations = new ValueChange<PublishedOperations>(PublishedOperations.All, PublishedOperations.None),
        });

        // Act & Assert
        ShouldHaveLine(diff, ChangeKind.Add, "tables in schema archive");
        ShouldHaveLine(diff, ChangeKind.Remove, "tables in schema audit");
        ShouldHaveLine(diff, ChangeKind.Modify, "publish: insert, update, delete, truncate → nothing");
    }

    [Fact]
    public void From_PublicationsCountInTheSummary()
        => DiffDocument.From(new DatabaseDiff([])
        {
            Publications = [PublicationDiff.Added(new Publication { Name = "a" }), PublicationDiff.Modified("b"), PublicationDiff.Removed("c")],
        }).Summary.ShouldBe(new DiffSummary(1, 1, 1));

    [Fact]
    public void From_PublicationsRenderAfterTheSchemas()
    {
        // Arrange
        var diff = DiffOf([Schema("app", ChangeKind.Add)]) with { Publications = [PublicationDiff.Removed("feed")] };

        // Act
        var lines = DiffDocument.From(diff).Lines;

        // Assert
        IndexOf(lines, line => line.Text.StartsWith("publication")).ShouldBeGreaterThan(IndexOf(lines, line => line.Text.StartsWith("schema")));
    }

    // The index of the first line matching the predicate, or -1.
    private static int IndexOf(IReadOnlyList<DiffLine> lines, Func<DiffLine, bool> predicate)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (predicate(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
