using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Schemas;
using NSchema.Model.Services;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The dependency graph: one edge meaning ("requires, to exist"), walkable both ways. The walks are the
/// callers' business — the graph only knows the edges.
/// </summary>
public class DependencyGraphTests
{
    private static SqlIdentifier Id(string value) => new(value);

    private static DependencyNode Table(string schema, string table) =>
        new(new ObjectAddress(Id(schema), Id(table)), DependencyKind.Table);

    private static DependencyNode View(string schema, string view) =>
        new(new ObjectAddress(Id(schema), Id(view)), DependencyKind.View);

    private static DependencyNode ForeignKey(string schema, string table, string name) =>
        new(new MemberAddress(Id(schema), Id(table), Id(name)), DependencyKind.ForeignKey);

    private static Table WithForeignKey(string name, string constraint, string toSchema, string toTable) =>
        new Table
        {
            Name = Id(name),
            Columns = [new Column { Name = Id("id"), Type = SqlType.Int }],
            ForeignKeys = [new ForeignKey { Name = Id(constraint), ColumnNames = [Id("id")], References = new ObjectAddress(Id(toSchema), Id(toTable)), ReferencedColumnNames = [Id("id")] }],
        };

    /// <summary>app.users, and billing.orders pointing an FK at it.</summary>
    private static Database CrossSchemaForeignKey() => new Database
    {
        Schemas = [
        new Schema { Name = Id("app"), Tables = [new Table { Name = Id("users"), Columns = [new Column { Name = Id("id"), Type = SqlType.Int }] }] },
        new Schema { Name = Id("billing"), Tables = [WithForeignKey("orders", "fk_orders_user", "app", "users")] },
    ],
    };

    [Fact]
    public void DependentsOf_ReferencedTable_IsTheConstraint_NotTheTableThatOwnsIt()
    {
        // Arrange — the case a partial teardown hits: dropping app.users orphans billing.orders' FK. What
        // must go is the constraint; billing.orders keeps its rows.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act
        var dependents = graph.DependentsOf(Table("app", "users"));

        // Assert
        dependents.ShouldHaveSingleItem().ShouldBe(ForeignKey("billing", "orders", "fk_orders_user"));
    }

    [Fact]
    public void DependentsOf_TableReadByAView_IsTheView()
    {
        // Arrange — a view's dependency is embedded in its body, so there is nothing to sever but the view.
        var database = new Database
        {
            Schemas = [
            new Schema { Name = Id("app"),
                Tables = [new Table { Name = Id("users"), Columns = [new Column { Name = Id("id"), Type = SqlType.Int }] }],
                Views = [new View { Name = Id("active_users"), Body = "select * from app.users",
                    DependsOn = [new ObjectAddress(Id("app"), Id("users"))] }] },
        ],
        };
        var graph = new DependencyGraph(database);

        // Act
        var dependents = graph.DependentsOf(Table("app", "users"));

        // Assert
        dependents.ShouldHaveSingleItem().ShouldBe(View("app", "active_users"));
    }

    [Fact]
    public void DependenciesOf_Constraint_IsTheTableItPointsAt_NotItsOwner()
    {
        // Arrange — containment is not an edge, so the FK does not "require" billing.orders.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act
        var dependencies = graph.DependenciesOf(ForeignKey("billing", "orders", "fk_orders_user"));

        // Assert
        dependencies.ShouldHaveSingleItem().ShouldBe(Table("app", "users"));
    }

    [Fact]
    public void DependentsOf_SomethingNothingNeeds_IsEmpty()
    {
        // Arrange
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act & Assert — the walk terminates: a constraint has no dependents of its own.
        graph.DependentsOf(ForeignKey("billing", "orders", "fk_orders_user")).ShouldBeEmpty();
        graph.DependentsOf(Table("billing", "orders")).ShouldBeEmpty();
    }

    [Fact]
    public void ForeignKey_PointingOutsideTheDatabase_ProducesNoEdge()
    {
        // Arrange — a reference to something not here (unmanaged, or simply absent) is ignored rather than
        // inventing a node, as the linearizer's sort does.
        var database = new Database { Schemas = [new Schema { Name = Id("billing"), Tables = [WithForeignKey("orders", "fk_orders_user", "elsewhere", "users")] }] };
        var graph = new DependencyGraph(database);

        // Assert
        graph.DependenciesOf(ForeignKey("billing", "orders", "fk_orders_user")).ShouldBeEmpty();
    }

    [Fact]
    public void SelfReferencingForeignKey_DependsOnItsOwnTable()
    {
        // Arrange — a self-reference is an ordinary edge; cycles are the caller's problem, not the graph's.
        var database = new Database { Schemas = [new Schema { Name = Id("app"), Tables = [WithForeignKey("nodes", "fk_nodes_parent", "app", "nodes")] }] };
        var graph = new DependencyGraph(database);

        // Assert
        graph.DependenciesOf(ForeignKey("app", "nodes", "fk_nodes_parent")).ShouldHaveSingleItem().ShouldBe(Table("app", "nodes"));
        graph.DependentsOf(Table("app", "nodes")).ShouldHaveSingleItem().ShouldBe(ForeignKey("app", "nodes", "fk_nodes_parent"));
    }

    [Fact]
    public void AllDependentsOf_CollectsTransitively_ExcludingTheSeeds()
    {
        // Arrange — app.users ← app.active_users (view) ← app.recent_users (view on the view).
        var database = new Database
        {
            Schemas = [
            new Schema { Name = Id("app"),
                Tables = [new Table { Name = Id("users"), Columns = [new Column { Name = Id("id"), Type = SqlType.Int }] }],
                Views = [
                    new View { Name = Id("active_users"), Body = "select * from app.users",
                        DependsOn = [new ObjectAddress(Id("app"), Id("users"))] },
                    new View { Name = Id("recent_users"), Body = "select * from app.active_users",
                        DependsOn = [new ObjectAddress(Id("app"), Id("active_users"))] },
                ] },
        ],
        };
        var graph = new DependencyGraph(database);

        // Act — dropping app.users costs both views, not just the one that names it.
        var closure = graph.AllDependentsOf([Table("app", "users")]);

        // Assert
        closure.ShouldBe([View("app", "active_users"), View("app", "recent_users")], ignoreOrder: true);
    }

    [Fact]
    public void AllDependentsOf_MutuallyReferencingTables_CostsOnlyTheConstraintPointingAtTheSeed()
    {
        // Arrange — mutual foreign keys are legal and common. They are only a cycle if containment is an
        // edge (a needs fk_a_b needs b needs fk_b_a needs a); it isn't, so the walk stays finite by shape.
        var database = new Database
        {
            Schemas = [
            new Schema { Name = Id("app"), Tables = [WithForeignKey("a", "fk_a_b", "app", "b"), WithForeignKey("b", "fk_b_a", "app", "a")] },
        ],
        };
        var graph = new DependencyGraph(database);

        // Act
        var closure = graph.AllDependentsOf([Table("app", "a")]);

        // Assert — dropping app.a costs the constraint aimed at it. Its own fk_a_b goes with the table it
        // belongs to, which is the tree's business, not the graph's.
        closure.ShouldHaveSingleItem().ShouldBe(ForeignKey("app", "b", "fk_b_a"));
    }

    [Fact]
    public void AllDependentsOf_ADiamond_CollectsEachNodeOnce()
    {
        // Arrange — two views read app.users and a third reads both, so 'summary' is reachable by two paths.
        var database = new Database
        {
            Schemas = [
            new Schema { Name = Id("app"),
                Tables = [new Table { Name = Id("users"), Columns = [new Column { Name = Id("id"), Type = SqlType.Int }] }],
                Views = [
                    new View { Name = Id("active"), Body = "select * from app.users", DependsOn = [new ObjectAddress(Id("app"), Id("users"))] },
                    new View { Name = Id("recent"), Body = "select * from app.users", DependsOn = [new ObjectAddress(Id("app"), Id("users"))] },
                    new View { Name = Id("summary"), Body = "select * from app.active, app.recent",
                        DependsOn = [new ObjectAddress(Id("app"), Id("active")), new ObjectAddress(Id("app"), Id("recent"))] },
                ] },
        ],
        };
        var graph = new DependencyGraph(database);

        // Act
        var closure = graph.AllDependentsOf([Table("app", "users")]);

        // Assert
        closure.ShouldBe([View("app", "active"), View("app", "recent"), View("app", "summary")], ignoreOrder: true);
    }

    [Fact]
    public void AllDependentsOf_ManySeeds_DoesNotReportOneSeedAsAnothersCost()
    {
        // Arrange — a partial teardown seeds with everything it is already dropping, and wants only the extra.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Act
        var closure = graph.AllDependentsOf([Table("app", "users"), ForeignKey("billing", "orders", "fk_orders_user")]);

        // Assert
        closure.ShouldBeEmpty();
    }

    [Fact]
    public void StatedDependentsOf_ExcludesWhatIsReachedOnlyThroughAnInferredEdge()
    {
        // Arrange — app.users has a foreign key pointing at it (the model states that) and a view reading it
        // (scanned out of the body). The full walk sees both; the stated-only walk sees just the constraint.
        var database = new Database
        {
            Schemas = [
            new Schema { Name = Id("app"), Tables = [new Table { Name = Id("users"), Columns = [new Column { Name = Id("id"), Type = SqlType.Int }] }],
                Views = [new View { Name = Id("summary"), Body = "select * from app.users", DependsOn = [new ObjectAddress(Id("app"), Id("users"))] }] },
            new Schema { Name = Id("billing"), Tables = [WithForeignKey("orders", "fk_orders_user", "app", "users")] },
        ],
        };
        var graph = new DependencyGraph(database);
        var seed = new[] { Table("app", "users") };

        // Assert
        graph.AllDependentsOf(seed).ShouldBe([ForeignKey("billing", "orders", "fk_orders_user"), View("app", "summary")], ignoreOrder: true);
        graph.StatedDependentsOf(seed).ShouldHaveSingleItem().ShouldBe(ForeignKey("billing", "orders", "fk_orders_user"));
    }

    [Fact]
    public void At_FindsNodesByAddress_RegardlessOfKind()
    {
        // Arrange — the seed for a walk is an address; the graph says what is actually there.
        var graph = new DependencyGraph(CrossSchemaForeignKey());

        // Assert
        graph.At(new ObjectAddress(Id("app"), Id("users"))).ShouldHaveSingleItem().ShouldBe(Table("app", "users"));
        graph.At(new ObjectAddress(Id("app"), Id("nope"))).ShouldBeEmpty();
    }

    private static DependencyNode Enum(string schema, string name) =>
        new(new ObjectAddress(Id(schema), Id(name)), DependencyKind.Enum);

    private static DependencyNode Domain(string schema, string name) =>
        new(new ObjectAddress(Id(schema), Id(name)), DependencyKind.Domain);

    private static DependencyNode Composite(string schema, string name) =>
        new(new ObjectAddress(Id(schema), Id(name)), DependencyKind.CompositeType);

    private static DependencyNode ColumnNode(string schema, string table, string column) =>
        new(new MemberAddress(Id(schema), Id(table), Id(column)), DependencyKind.Column);

    private static Schema WithStatusEnum(string schema) => new()
    {
        Name = Id(schema),
        Enums = [new EnumType { Name = Id("status"), Values = ["new", "done"] }],
    };

    private static Table WithTypedColumn(string table, string column, SqlType type) => new()
    {
        Name = Id(table),
        Columns = [new Column { Name = Id(column), Type = type }],
    };

    [Fact]
    public void DependentsOf_EnumUsedByAColumn_IsTheColumn_NotItsTable()
    {
        // Arrange — the column names its type qualified, so the edge is stated. What the drop costs is the
        // column: its table is not required to go, but the column cannot outlive its type.
        var schema = WithStatusEnum("app");
        schema.Tables.Add(WithTypedColumn("orders", "state", SqlType.Custom(Id("app"), "status")));
        var graph = new DependencyGraph(new Database { Schemas = [schema] });

        // Act & Assert
        graph.DependentsOf(Enum("app", "status")).ShouldHaveSingleItem().ShouldBe(ColumnNode("app", "orders", "state"));
        graph.StatedDependentsOf([Enum("app", "status")]).ShouldHaveSingleItem().ShouldBe(ColumnNode("app", "orders", "state"));
    }

    [Fact]
    public void DependentsOf_BareTypeName_BindsToTheOnlyMatch_AsAGuess()
    {
        // Arrange — a bare name would be resolved by the engine's search path, which NSchema does not know;
        // a unique match is a good guess, so the edge exists but is hedged.
        var schema = WithStatusEnum("app");
        schema.Tables.Add(WithTypedColumn("orders", "state", SqlType.Custom("status")));
        var graph = new DependencyGraph(new Database { Schemas = [schema] });

        // Act & Assert
        graph.DependentsOf(Enum("app", "status")).ShouldHaveSingleItem().ShouldBe(ColumnNode("app", "orders", "state"));
        graph.StatedDependentsOf([Enum("app", "status")]).ShouldBeEmpty();
    }

    [Fact]
    public void DependentsOf_BareTypeName_AmbiguousAcrossSchemas_BindsNothing()
    {
        // Arrange — two schemas declare a 'status'; guessing between them could sever the wrong thing.
        var app = WithStatusEnum("app");
        app.Tables.Add(WithTypedColumn("orders", "state", SqlType.Custom("status")));
        var graph = new DependencyGraph(new Database { Schemas = [app, WithStatusEnum("sales")] });

        // Act & Assert
        graph.DependentsOf(Enum("app", "status")).ShouldBeEmpty();
        graph.DependentsOf(Enum("sales", "status")).ShouldBeEmpty();
    }

    [Fact]
    public void DependentsOf_BuiltInColumnTypes_ProduceNoEdges()
    {
        // Arrange — 'int' names no declared type, so nothing binds.
        var schema = WithStatusEnum("app");
        schema.Tables.Add(WithTypedColumn("orders", "id", SqlType.Int));
        var graph = new DependencyGraph(new Database { Schemas = [schema] });

        // Assert
        graph.DependentsOf(Enum("app", "status")).ShouldBeEmpty();
    }

    [Fact]
    public void DependentsOf_EnumUnderADomain_IsTheDomain()
    {
        // Arrange — the domain's base type is part of its declaration, so the edge is stated.
        var schema = WithStatusEnum("app");
        schema.Domains.Add(new DomainType { Name = Id("tracked_status"), DataType = SqlType.Custom(Id("app"), "status") });
        var graph = new DependencyGraph(new Database { Schemas = [schema] });

        // Assert
        graph.DependentsOf(Enum("app", "status")).ShouldHaveSingleItem().ShouldBe(Domain("app", "tracked_status"));
    }

    [Fact]
    public void DependentsOf_EnumInACompositeField_IsTheCompositeType()
    {
        // Arrange
        var schema = WithStatusEnum("app");
        schema.CompositeTypes.Add(new CompositeType
        {
            Name = Id("audit_entry"),
            Fields = [new CompositeField(Id("state"), SqlType.Custom(Id("app"), "status")), new CompositeField(Id("at"), SqlType.DateTime)],
        });
        var graph = new DependencyGraph(new Database { Schemas = [schema] });

        // Assert
        graph.DependentsOf(Enum("app", "status")).ShouldHaveSingleItem().ShouldBe(Composite("app", "audit_entry"));
    }

    [Fact]
    public void AllDependentsOf_Enum_WalksThroughTheDomainToTheColumnsOnIt()
    {
        // Arrange — app.status ← app.tracked_status (domain) ← billing.orders.state (column on the domain).
        var app = WithStatusEnum("app");
        app.Domains.Add(new DomainType { Name = Id("tracked_status"), DataType = SqlType.Custom(Id("app"), "status") });
        var billing = new Schema { Name = Id("billing"), Tables = [WithTypedColumn("orders", "state", SqlType.Custom(Id("app"), "tracked_status"))] };
        var graph = new DependencyGraph(new Database { Schemas = [app, billing] });

        // Act — dropping the enum costs the domain, and the domain costs every column declared against it.
        var closure = graph.AllDependentsOf([Enum("app", "status")]);

        // Assert
        closure.ShouldBe([Domain("app", "tracked_status"), ColumnNode("billing", "orders", "state")], ignoreOrder: true);
    }

    [Fact]
    public void ObjectDependenciesOf_ComputedColumn_ReachesTheRoutineItCalls()
    {
        // Arrange — the AdventureWorks shape: a computed column calling a scalar function, spelled with
        // T-SQL brackets. The table must be created after the function, and the function dropped after it.
        var database = new Database
        {
            Schemas = [
            new Schema
            {
                Name = Id("dbo"),
                Routines = [new NSchema.Model.Routines.Routine { Name = Id("ufnLeadingZeros"), RoutineKind = NSchema.Model.Routines.RoutineKind.Function, Arguments = "@Value int", Definition = "RETURNS varchar(8) AS BEGIN RETURN '0' END" }],
                Tables = [new Table
                {
                    Name = Id("Customer"),
                    Columns = [
                        new Column { Name = Id("CustomerID"), Type = SqlType.Int },
                        new Column { Name = Id("AccountNumber"), Type = SqlType.VarChar(10), GeneratedExpression = "(isnull('AW'+[dbo].[ufnLeadingZeros]([CustomerID]),''))" },
                    ],
                }],
            },
        ],
        };
        var graph = new DependencyGraph(database);

        // Act & Assert — both directions of the same edge: creation needs, and drop cost.
        graph.ObjectDependenciesOf(new ObjectAddress(Id("dbo"), Id("Customer")))
            .ShouldContain(new ObjectAddress(Id("dbo"), Id("ufnLeadingZeros")));
        graph.ObjectDependentsOf(new ObjectAddress(Id("dbo"), Id("ufnLeadingZeros")))
            .ShouldContain(new ObjectAddress(Id("dbo"), Id("Customer")));
    }

    [Fact]
    public void ObjectDependenciesOf_CheckConstraint_ReachesTheRoutineItCalls()
    {
        // Arrange — an unqualified call resolves against the table's own schema.
        var database = new Database
        {
            Schemas = [
            new Schema
            {
                Name = Id("app"),
                Routines = [new NSchema.Model.Routines.Routine { Name = Id("is_valid"), RoutineKind = NSchema.Model.Routines.RoutineKind.Function, Arguments = "v text", Definition = "RETURNS boolean LANGUAGE sql AS $$ SELECT true $$" }],
                Tables = [new Table
                {
                    Name = Id("orders"),
                    Columns = [new Column { Name = Id("code"), Type = SqlType.Text }],
                    CheckConstraints = [new CheckConstraint { Name = Id("ck_code"), Expression = "is_valid(code)" }],
                }],
            },
        ],
        };
        var graph = new DependencyGraph(database);

        // Act & Assert
        graph.ObjectDependenciesOf(new ObjectAddress(Id("app"), Id("orders")))
            .ShouldContain(new ObjectAddress(Id("app"), Id("is_valid")));
    }
}
