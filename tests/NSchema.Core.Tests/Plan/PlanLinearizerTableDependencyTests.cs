using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Pins the order the linearizer gives table creates and drops: a table is created after the tables its
/// foreign keys point at, and dropped before them. The foreign keys are read from the databases either side of
/// the migration, so each table under test is declared on the side the change comes from.
/// </summary>
public sealed class PlanLinearizerTableDependencyTests
{
    private readonly PlanLinearizer _linearizer = new();
    private readonly MigrationSides _sides = new();

    // -- builders --------------------------------------------------------------

    /// <summary>A table whose foreign keys point at each of <paramref name="references"/> ("schema.table").</summary>
    private static Table TableReferencing(string name, params string[] references) => new()
    {
        Name = name,
        ForeignKeys = [.. references.Select((reference, i) => new ForeignKey
        {
            Name = $"fk_{name}_{i}",
            ColumnNames = ["id"],
            References = Address(reference),
            ReferencedColumnNames = ["id"],
        })],
    };

    private static ObjectAddress Address(string qualified) =>
        qualified.Split('.') is [var schema, var name] ? new ObjectAddress(schema, name) : new ObjectAddress("app", qualified);

    /// <summary>A table the project declares and the database does not, shaped the way the comparer builds one.</summary>
    private TableDiff Added(string schema, string name, params string[] references)
    {
        var table = _sides.Creating(schema, TableReferencing(name, references));
        return TableDiff.Added(schema, table) with { ForeignKeys = [.. table.ForeignKeys.Select(ForeignKeyDiff.Added)] };
    }

    /// <summary>A table the database has and the project does not.</summary>
    private TableDiff Removed(string schema, string name, params string[] references)
    {
        _sides.Dropping(schema, TableReferencing(name, references));
        return TableDiff.Removed(schema, name);
    }

    private IReadOnlyList<MigrationAction> Linearize(params SchemaDiff[] schemas) =>
        _linearizer.Linearize(new DatabaseDiff(schemas), _sides.Dependencies, DialectCapabilities.Standard);

    /// <summary>Linearizes for a dialect that keeps every foreign key on the table declaring it.</summary>
    private IReadOnlyList<MigrationAction> LinearizeInline(params SchemaDiff[] schemas) =>
        _linearizer.Linearize(new DatabaseDiff(schemas), _sides.Dependencies, new DialectCapabilities(CanAlterForeignKeys: false));

    private static IReadOnlyList<string> DropOrder(IReadOnlyList<MigrationAction> plan) =>
        [.. plan.OfType<DropTable>().Select(d => d.Table.Name.Value)];

    private static IReadOnlyList<string> CreateOrder(IReadOnlyList<MigrationAction> plan) =>
        [.. plan.OfType<CreateTable>().Select(c => c.Table.Name.Value)];

    /// <summary>Where the first action of the given kind lands in the plan.</summary>
    private static int Index<T>(IReadOnlyList<MigrationAction> plan) where T : MigrationAction =>
        plan.Select((action, i) => (action, i)).First(x => x.action is T).i;

    // -------------------------------------------------------------------------

    [Fact]
    public void DroppedTables_AreOrderedDependentsFirst()
    {
        // Arrange
        var schema = SchemaDiff.Removed("identity") with
        {
            Tables = [Removed("identity", "permissions"), Removed("identity", "role_permissions", "identity.permissions")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["role_permissions", "permissions"]);
    }

    [Fact]
    public void DroppedTables_AreOrderedAcrossSchemas()
    {
        // Arrange
        var identity = SchemaDiff.Removed("identity") with { Tables = [Removed("identity", "users")] };
        var staff = SchemaDiff.Removed("staff") with { Tables = [Removed("staff", "employees", "identity.users")] };

        // Act
        var plan = Linearize(identity, staff);

        // Assert
        DropOrder(plan).ShouldBe(["employees", "users"]);
    }

    [Fact]
    public void DroppedTables_WithoutDependencies_KeepTheirDeclaredOrder()
    {
        // Arrange
        var schema = SchemaDiff.Removed("app") with
        {
            Tables = [Removed("app", "alpha"), Removed("app", "beta"), Removed("app", "gamma")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["alpha", "beta", "gamma"]);
    }

    [Fact]
    public void CreatedTables_AreOrderedDependenciesFirst()
    {
        // Arrange
        var schema = SchemaDiff.Added("clients") with
        {
            Tables = [Added("clients", "insurer_references", "clients.insurers"), Added("clients", "insurers")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert
        CreateOrder(plan).ShouldBe(["insurers", "insurer_references"]);
    }

    [Fact]
    public void CreatedTables_AreOrderedAcrossSchemas()
    {
        // Arrange
        var staff = SchemaDiff.Added("staff") with { Tables = [Added("staff", "employees", "identity.users")] };
        var identity = SchemaDiff.Added("identity") with { Tables = [Added("identity", "users")] };

        // Act
        var plan = Linearize(staff, identity);

        // Assert
        CreateOrder(plan).ShouldBe(["users", "employees"]);
    }

    [Fact]
    public void SelfReferencingTable_IsOrderedWithoutCycling()
    {
        // Arrange
        var schema = SchemaDiff.Removed("app") with { Tables = [Removed("app", "nodes", "app.nodes")] };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["nodes"]);
    }

    [Fact]
    public void MutuallyReferencingTables_AreDropped_WithTheUnorderableForeignKeyCutFirst()
    {
        // Arrange — no drop order satisfies both foreign keys, so one of them has to go before either table can.
        var schema = SchemaDiff.Removed("app") with
        {
            Tables = [Removed("app", "left", "app.right"), Removed("app", "right", "app.left")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert — the key on the table dropped second is the one no order can keep.
        var severed = plan.OfType<DropForeignKey>().ShouldHaveSingleItem().ForeignKey;
        severed.Object.ShouldBe(DropOrder(plan)[1]);
        Index<DropForeignKey>(plan).ShouldBeLessThan(Index<DropTable>(plan));
    }

    [Fact]
    public void MutuallyReferencingTables_AreCreated_WithTheUnorderableForeignKeyAddedAfterwards()
    {
        // Arrange — the second table does not exist yet when the first is created, so its key cannot ride along.
        var schema = SchemaDiff.Added("app") with
        {
            Tables = [Added("app", "left", "app.right"), Added("app", "right", "app.left")],
        };

        // Act
        var plan = Linearize(schema);

        // Assert — the table created first is the one that cannot carry its key inline.
        var first = CreateOrder(plan)[0];
        plan.OfType<AddForeignKey>().ShouldHaveSingleItem().Table.Name.ShouldBe(first);
        plan.OfType<CreateTable>().Single(t => t.Table.Name.Value.Equals(first)).Table.ForeignKeys.ShouldBeEmpty();
        plan.OfType<CreateTable>().Single(t => !t.Table.Name.Value.Equals(first)).Table.ForeignKeys.ShouldHaveSingleItem();
    }

    [Fact]
    public void MutuallyReferencingTables_OnADialectThatCannotAlterKeys_KeepThemInline()
    {
        // Arrange — nothing can be added afterwards, so the keys stay on the tables and the database resolves
        // the forward reference itself.
        var schema = SchemaDiff.Added("app") with
        {
            Tables = [Added("app", "left", "app.right"), Added("app", "right", "app.left")],
        };

        // Act
        var plan = LinearizeInline(schema);

        // Assert
        plan.OfType<AddForeignKey>().ShouldBeEmpty();
        plan.OfType<CreateTable>().ShouldAllBe(t => t.Table.ForeignKeys.Count == 1);
    }

    [Fact]
    public void MutuallyReferencingTables_OnADialectThatCannotAlterKeys_AreDroppedWithoutCuttingOne()
    {
        // Arrange
        var schema = SchemaDiff.Removed("app") with
        {
            Tables = [Removed("app", "left", "app.right"), Removed("app", "right", "app.left")],
        };

        // Act
        var plan = LinearizeInline(schema);

        // Assert
        plan.OfType<DropForeignKey>().ShouldBeEmpty();
        DropOrder(plan).Count.ShouldBe(2);
    }

    [Fact]
    public void CreatedTable_ReferencingOneAlreadyThere_KeepsItsForeignKeyInline()
    {
        // Arrange — nothing in the plan has to happen first, so the key rides the CREATE TABLE as usual.
        var schema = SchemaDiff.Containing("app") with { Tables = [Added("app", "orders", "app.customers")] };

        // Act
        var plan = Linearize(schema);

        // Assert
        plan.OfType<AddForeignKey>().ShouldBeEmpty();
        plan.OfType<CreateTable>().ShouldHaveSingleItem().Table.ForeignKeys.ShouldHaveSingleItem();
    }

    [Fact]
    public void TablesTheMigrationDoesNotTouch_DoNotOrderAnything()
    {
        // Arrange — the referenced table stays, so only the referencing one is in the plan to be ordered.
        _sides.Dropping("app", TableReferencing("kept"));
        var schema = SchemaDiff.Containing("app") with { Tables = [Removed("app", "orders", "app.kept")] };

        // Act
        var plan = Linearize(schema);

        // Assert
        DropOrder(plan).ShouldBe(["orders"]);
    }
}
