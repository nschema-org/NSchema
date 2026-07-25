using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Policies;

namespace NSchema.Tests.Project.Policies;

public sealed class StructuralIntegrityPolicyTests
{
    private readonly StructuralIntegrityPolicy _sut = new();

    private static Column Col(string name) => new Column { Name = name, Type = SqlType.BigInt };

    private static Database Db(params Table[] tables) =>
        new Database { Schemas = [new Schema { Name = "public", Tables = [.. tables] }] };

    [Fact]
    public void NoDiagnostics_ForAConsistentSchema()
    {
        // Arrange
        var users = new Table { Name = "users", PrimaryKey = new PrimaryKey { Name = "users_pk", ColumnNames = ["id"] }, Columns = [Col("id")] };
        var orders = new Table
        {
            Name = "orders",
            PrimaryKey = new PrimaryKey { Name = "orders_pk", ColumnNames = ["id"] },
            Columns = [Col("id"), Col("user_id")],
            ForeignKeys = [new ForeignKey { Name = "orders_users_fk", ColumnNames = ["user_id"], References = new ObjectAddress("public", "users"), ReferencedColumnNames = ["id"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(users, orders)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenIndexNameIsReusedAcrossTablesInASchema()
    {
        // Arrange — index names are schema-scoped, so two tables can't both declare 'ix_updated_at'.
        var invoices = new Table
        {
            Name = "invoices",
            Columns = [Col("updated_at")],
            Indexes = [new TableIndex { Name = "ix_updated_at", Columns = [new IndexColumn("updated_at")] }],
        };
        var orders = new Table
        {
            Name = "orders",
            Columns = [Col("updated_at")],
            Indexes = [new TableIndex { Name = "ix_updated_at", Columns = [new IndexColumn("updated_at")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain("ix_updated_at");
        diagnostic.Message.ShouldContain("public.invoices");
        diagnostic.Message.ShouldContain("public.orders");
    }

    [Fact]
    public void Error_WhenAPrimaryKeyNameCollidesWithAnIndexName()
    {
        // Arrange — a PRIMARY KEY creates an index bearing its name, so it shares the schema-wide pool.
        var invoices = new Table { Name = "invoices", PrimaryKey = new PrimaryKey { Name = "shared_name", ColumnNames = ["id"] }, Columns = [Col("id")] };
        var orders = new Table
        {
            Name = "orders",
            Columns = [Col("id")],
            Indexes = [new TableIndex { Name = "shared_name", Columns = [new IndexColumn("id")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldContain("shared_name");
    }

    [Fact]
    public void NoDiagnostics_WhenTheSameIndexNameIsUsedInDifferentSchemas()
    {
        // Arrange
        Table Invoices() => new Table
        {
            Name = "invoices",
            Columns = [Col("updated_at")],
            Indexes = [new TableIndex { Name = "ix_updated_at", Columns = [new IndexColumn("updated_at")] }],
        };
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "billing", Tables = [Invoices()] },
            new Schema { Name = "ordering", Tables = [Invoices()] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenTableHasNoColumns()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table { Name = "empty" })).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("no columns"));
    }

    [Fact]
    public void Error_WhenColumnDeclaredTwice()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table { Name = "t", Columns = [Col("id"), Col("id")] })).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void NoError_WhenColumnsDifferOnlyInCase()
    {
        // Identifiers are case-sensitive: "id" and "ID" are two different columns, not a duplicate.
        var diagnostics = _sut.Validate(Db(new Table { Name = "t", Columns = [Col("id"), Col("ID")] })).ToList();

        diagnostics.ShouldNotContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void Error_WhenPrimaryKeyReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table { Name = "t", PrimaryKey = new PrimaryKey { Name = "pk", ColumnNames = ["missing"] }, Columns = [Col("id")] };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Primary key") && d.Message.Contains("missing"));
    }

    [Fact]
    public void Error_WhenIndexReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table { Name = "t", Columns = [Col("id")], Indexes = [new TableIndex { Name = "ix", Columns = ["nope"] }] };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Index") && d.Message.Contains("nope"));
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesUnknownLocalColumn()
    {
        // Arrange
        var table = new Table
        {
            Name = "t",
            Columns = [Col("id")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["ghost"], References = new ObjectAddress("public", "t"), ReferencedColumnNames = ["id"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("unknown local column 'ghost'"));
    }

    [Fact]
    public void Error_WhenForeignKeyArityMismatches()
    {
        // Arrange
        var table = new Table
        {
            Name = "t",
            PrimaryKey = new PrimaryKey { Name = "pk", ColumnNames = ["id"] },
            Columns = [Col("id"), Col("a"), Col("b")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["a", "b"], References = new ObjectAddress("public", "t"), ReferencedColumnNames = ["id"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("counts must match"));
    }

    [Fact]
    public void Warning_WhenForeignKeyTargetTableIsUndeclared()
    {
        // Arrange — the target may exist unmanaged (gradual adoption), so the finding advises rather than blocks.
        var table = new Table
        {
            Name = "t",
            Columns = [Col("id"), Col("ref")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["ref"], References = new ObjectAddress("public", "absent"), ReferencedColumnNames = ["id"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        var finding = diagnostics.ShouldHaveSingleItem();
        finding.Severity.ShouldBe(DiagnosticSeverity.Warning);
        finding.Message.ShouldContain("references table 'public.absent', which this project does not declare");
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetSchemaIsUnmanaged()
    {
        // Arrange — "external" schema is not present in the document at all.
        var table = new Table
        {
            Name = "t",
            Columns = [Col("id"), Col("ref")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["ref"], References = new ObjectAddress("external", "other"), ReferencedColumnNames = ["id"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesNonUniqueColumns()
    {
        // Arrange — target column exists but is neither a PK nor a unique index.
        var target = new Table { Name = "target", PrimaryKey = new PrimaryKey { Name = "pk", ColumnNames = ["id"] }, Columns = [Col("id"), Col("code")] };
        var source = new Table
        {
            Name = "source",
            Columns = [Col("id"), Col("code")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["code"], References = new ObjectAddress("public", "target"), ReferencedColumnNames = ["code"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void NoError_WhenForeignKeyReferencesUniqueIndex()
    {
        // Arrange
        var target = new Table
        {
            Name = "target",
            PrimaryKey = new PrimaryKey { Name = "pk", ColumnNames = ["id"] },
            Columns = [Col("id"), Col("code")],
            Indexes = [new TableIndex { Name = "uq", Columns = ["code"], IsUnique = true }],
        };
        var source = new Table
        {
            Name = "source",
            Columns = [Col("id"), Col("code")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["code"], References = new ObjectAddress("public", "target"), ReferencedColumnNames = ["code"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesPredicatedUniqueIndex()
    {
        // Arrange — a partial (predicated) unique index cannot back a foreign key.
        var target = new Table
        {
            Name = "target",
            PrimaryKey = new PrimaryKey { Name = "pk", ColumnNames = ["id"] },
            Columns = [Col("id"), Col("code")],
            Indexes = [new TableIndex { Name = "uq", Columns = ["code"], IsUnique = true, Predicate = "code IS NOT NULL" }],
        };
        var source = new Table
        {
            Name = "source",
            Columns = [Col("id"), Col("code")],
            ForeignKeys = [new ForeignKey { Name = "fk", ColumnNames = ["code"], References = new ObjectAddress("public", "target"), ReferencedColumnNames = ["code"] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void Error_WhenFunctionAndProcedureShareAName()
    {
        // Arrange — the parser and aggregation enforce this for parsed schemas; the policy is the catch-all
        // for JSON-sourced and code-built schemas.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "public", Routines = [
                new Routine { Name = "r", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int AS $$ SELECT 1 $$" },
                new Routine { Name = "r", RoutineKind = RoutineKind.Procedure, Arguments = "", Definition = "AS $$ SELECT 1 $$" },
            ] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("share a single name space"));
    }

    [Fact]
    public void Error_WhenFunctionDeclaredTwice()
    {
        // Arrange
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "public", Routines = [
                new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "RETURNS int AS $$ SELECT 1 $$" },
                new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "a int", Definition = "RETURNS int AS $$ SELECT 2 $$" },
            ] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert — overloading is not supported: one routine per name.
        diagnostics.ShouldContain(d => d.Message.Contains("declares routine 'f' more than once"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossObjectKinds()
    {
        // Arrange
        // A table and a view called 'foo' cannot coexist — they share one name space in the database.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "public",
                Tables = [new Table { Name = "foo", Columns = [Col("id")] }],
                Views = [new View { Name = "foo", Body = "SELECT 1" }] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("reuses the name 'foo'") && d.Message.Contains("table") && d.Message.Contains("view"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossTableAndEnum()
    {
        // Relations and types share pg_type (a relation has a row type), so a table and an enum collide too.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "public",
                Tables = [new Table { Name = "status", Columns = [Col("id")] }],
                Enums = [new EnumType { Name = "status", Values = ["a", "b"] }] },
        ],
        };

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("reuses the name 'status'"));
    }

    [Fact]
    public void Error_WhenSequenceDeclaredTwice()
    {
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "public", Sequences = [new Sequence { Name = "seq" }, new Sequence { Name = "seq" }] },
        ],
        };

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("declares sequence 'seq' more than once"));
    }

    [Fact]
    public void NoDiagnostics_WhenNamesDifferAcrossKinds()
    {
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = "public",
                Tables = [new Table { Name = "t", Columns = [Col("id")] }],
                Views = [new View { Name = "v", Body = "SELECT 1" }],
                Sequences = [new Sequence { Name = "s" }],
                CompositeTypes = [new CompositeType { Name = "c", Fields = [new CompositeField("f", SqlType.Int)] }],
                Enums = [new EnumType { Name = "e", Values = ["a"] }],
                Domains = [new DomainType { Name = "d", DataType = SqlType.Text }] },
        ],
        };

        _sut.Validate(schema).ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenColumnHasBothDefaultAndGenerated()
    {
        var table = new Table { Name = "t", Columns = [new Column { Name = "area", Type = SqlType.Int, DefaultExpression = "0", GeneratedExpression = "w * h" }] };

        _sut.Validate(Db(table)).ShouldContain(d =>
            d.Message.Contains("both a DEFAULT and a GENERATED") && d.Message.Contains("area"));
    }

    [Fact]
    public void NoDiagnostics_ForGeneratedColumnWithoutDefault()
    {
        var table = new Table
        {
            Name = "t",
            PrimaryKey = new PrimaryKey { Name = "t_pk", ColumnNames = ["id"] },
            Columns = [Col("id"), new Column { Name = "area", Type = SqlType.Int, GeneratedExpression = "w * h" }],
        };

        _sut.Validate(Db(table)).ShouldBeEmpty();
    }
}
