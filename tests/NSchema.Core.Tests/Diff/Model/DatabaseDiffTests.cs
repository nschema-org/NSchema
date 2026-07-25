using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Tests.Diff.Model;

public sealed class DatabaseDiffTests
{
    private static DeploymentScript Deployment(string name) =>
        new(name, "SELECT 1;", null, DeploymentPhase.Pre);

    private static ChangeScript Change(string name) =>
        new(name, "UPDATE 1;", new ChangeTarget("app", "users", "email", ChangeTrigger.AddColumn));

    private static DatabaseDiff WithChangeScript(ChangeScript change)
    {
        var column = ColumnDiff.Added(new Column { Name = "email", Type = SqlType.Text }) with { MigrationScript = change };
        var table = TableDiff.Modified("app", "users") with { Columns = [column] };
        return new DatabaseDiff([SchemaDiff.Containing("app") with { Tables = [table] }]);
    }

    [Fact]
    public void ChangeScripts_WalksTheNodesTheyRideOn()
    {
        // Act
        var change = Change("backfill");

        // Assert
        WithChangeScript(change).ChangeScripts().ShouldBe(new[] { change });
    }

    [Fact]
    public void AllScripts_IsChangeScriptsThenDeploymentScripts()
    {
        // Arrange
        var change = Change("backfill");
        var deploy = Deployment("seed");

        // Act
        var diff = WithChangeScript(change) with { DeploymentScripts = [deploy] };

        // Assert
        diff.AllScripts().ShouldBe(new Script[] { change, deploy });
    }

    [Fact]
    public void IsEmpty_TrueForNoChangesAndNoDeploymentScripts()
        => new DatabaseDiff([]).IsEmpty.ShouldBeTrue();

    [Fact]
    public void IsEmpty_FalseWhenADeploymentScriptRuns()
    {
        var diff = new DatabaseDiff([]) with { DeploymentScripts = [Deployment("seed")] };

        diff.IsEmpty.ShouldBeFalse();
    }

    /// <summary>
    /// app.users, with billing.orders pointing an FK at it and billing.summary reading it.
    /// </summary>
    private static Database CurrentDatabase() => new Database
    {
        Schemas = [
        new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] },
        new Schema { Name = "billing",
            Tables = [
                new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }],
                    ForeignKeys = [new ForeignKey { Name = "fk_orders_user", ColumnNames = ["id"], References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] }] },
            ],
            Views = [new View { Name = "summary", Body = "select * from app.users", DependsOn = [new ObjectAddress("app", "users")] }] },
    ],
    };

    /// <summary>
    /// The difference a teardown produces before any scope is applied: everything goes.
    /// </summary>
    private static DatabaseDiff TeardownDiff() => new(
    [
        SchemaDiff.Removed("app") with { Tables = [TableDiff.Removed("app", "users")] },
        SchemaDiff.Removed("billing") with
        {
            Tables = [TableDiff.Removed("billing", "orders")],
            Views = [ViewDiff.Removed("billing", "summary")],
        },
    ]);

    [Fact]
    public void ScopedTo_Unscoped_LeavesTheDifferenceAlone()
    {
        // Act
        var result = TeardownDiff().ScopedTo(PlanningScope.All, CurrentDatabase());

        // Assert
        result.Value!.Schemas.Count.ShouldBe(2);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_SeversTheOutOfScopeForeignKey_WithoutDroppingItsTable()
    {
        // Act — tear down app only. billing.orders keeps its rows; only the constraint aimed at app.users goes.
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Kind.ShouldBeNull(); // the run is not about billing; it just cannot avoid it
        var orders = billing.Tables.ShouldHaveSingleItem();
        orders.Kind.ShouldBe(ChangeKind.Modify);
        orders.ForeignKeys.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            f => f.Name.ShouldBe("fk_orders_user"),
            f => f.Kind.ShouldBe(ChangeKind.Remove));
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_DropsTheOutOfScopeViewThatReadsIt()
    {
        // Act — a view's dependency is embedded in its body, so there is nothing to sever but the view.
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Views.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            v => v.Name.ShouldBe("summary"),
            v => v.Kind.ShouldBe(ChangeKind.Remove));
    }

    [Fact]
    public void ScopedTo_ScopedTeardown_StillTearsDownTheScopedSchema()
    {
        // Act
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.Single(s => s.Name == "app");
        app.Kind.ShouldBe(ChangeKind.Remove);
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_WhenItReachesOutOfScope_AssertsWhatTheModelStates_AndHedgesWhatItGuessed()
    {
        // Act — a plan that touches what it was not asked to touch must announce it, not do it quietly. And
        // the two edge kinds are not equally trustworthy: the foreign key names its table outright, while the
        // view was scanned out of SQL nobody parsed.
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), CurrentDatabase());

        // Assert
        result.Diagnostics.Count.ShouldBe(2);

        var stated = result.Diagnostics.Single(d => d.Message.Contains("billing.orders.fk_orders_user"));
        stated.Source.ShouldBe("scope");
        stated.Message.ShouldNotContain("billing.summary");

        var inferred = result.Diagnostics.Single(d => d.Message.Contains("billing.summary"));
        inferred.Source.ShouldBe("scope");
        inferred.Message.ShouldContain("does not parse SQL");
    }

    [Fact]
    public void ScopedTo_ScopeThatDisturbsNothing_WidensNothing_AndIsQuiet()
    {
        // Arrange — tearing billing down costs app nothing: the dependencies point the other way.
        var result = TeardownDiff().ScopedTo(PlanningScope.To(new SchemaAddress("billing")), CurrentDatabase());

        // Assert
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("billing");
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_NarrowingOnly_DoesNotInventChangesForUnmanagedSchemas()
    {
        // Arrange — the ordinary case: an unrestricted plan derives its scope from what it manages, and
        // comparing whole states manufactures a removal for everything else. Narrowing must discard those.
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Containing("app") with { Tables = [TableDiff.Added("app", new Table { Name = "users" })] },
            SchemaDiff.Removed("unmanaged"),
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To(new SchemaAddress("app")), CurrentDatabase());

        // Assert
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        result.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>
    /// The difference that removes app's status enum, and nothing else.
    /// </summary>
    private static DatabaseDiff EnumRemovalDiff() => new(
    [
        SchemaDiff.Containing("app") with { Enums = [EnumDiff.Removed("app", "status")] },
    ]);

    /// <summary>
    /// app.status (an enum), with billing.orders.state declared against it — qualified or bare.
    /// </summary>
    private static Database DatabaseWithEnumTypedColumn(SqlType columnType) => new()
    {
        Schemas = [
        new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["new", "done"] }] },
        new Schema { Name = "billing",
            Tables = [new Table { Name = "orders", Columns = [new Column { Name = "state", Type = columnType }] }] },
    ],
    };

    [Fact]
    public void ScopedTo_RemovalAnOutOfScopeColumnDependsOn_Blocks_InsteadOfWidening()
    {
        // Arrange — closure severs definitions, never data: a column stands for its table's rows, so there is
        // no minimal sever. The plan is blocked, and still carried for review.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom("app", "status"));

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), current);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app"); // nothing widened into billing
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.ColumnBlocksRemoval([new MemberAddress("billing", "orders", "state")]));
    }

    [Fact]
    public void ScopedTo_RemovalABareTypedColumnAppearsToDependOn_Hedges_InsteadOfBlocking()
    {
        // Arrange — the edge was bound by name alone, and a wrong guess must not block a plan that need not
        // be. The plan proceeds with a warning; if the guess was right, the database rejects it at apply.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom("status"));

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.InferredColumnMayBlockRemoval([new MemberAddress("billing", "orders", "state")]));
    }

    [Fact]
    public void ScopedTo_RemovalAnOutOfScopeDomainIsBuiltOn_DropsTheDomain()
    {
        // Arrange — a domain is a definition, so it severs the way a view does.
        var current = new Database
        {
            Schemas = [
            new Schema { Name = "app", Enums = [new EnumType { Name = "status", Values = ["new", "done"] }] },
            new Schema { Name = "billing",
                Domains = [new DomainType { Name = "tracked_status", DataType = SqlType.Custom("app", "status") }] },
        ],
        };

        // Act
        var result = EnumRemovalDiff().ScopedTo(PlanningScope.To(new SchemaAddress("app")), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Kind.ShouldBeNull();
        billing.Domains.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            d => d.Name.ShouldBe("tracked_status"),
            d => d.Kind.ShouldBe(ChangeKind.Remove));
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.SeveredOutOfScope([new ObjectAddress("billing", "tracked_status")]));
    }

    private static ObjectAddress Target(string schema, string name) => new(schema, name);

    [Fact]
    public void ScopedTo_ObjectTargeted_KeepsTheTargetsChanges_AndDropsItsSiblingsAndTheSchemasOwnFacets()
    {
        // Arrange — a schema-level facet (comment, grants, the schema's own removal) sits below the schema,
        // not below any object, so targeting an object never drags it in.
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Containing("app") with
            {
                Comment = new ValueChange<string>("old", "new"),
                Tables = [
                    TableDiff.Modified("app", "users"),
                    TableDiff.Modified("app", "orders"),
                ],
            },
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("app", "users")]), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.ShouldHaveSingleItem();
        app.Comment.ShouldBeNull();
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    private static DatabaseDiff AddedInvoicesReferencing(ObjectAddress references)
    {
        var foreignKey = new ForeignKey
        {
            Name = "fk_invoices_customer",
            ColumnNames = ["customer_id"],
            References = references,
            ReferencedColumnNames = ["id"],
        };
        return new DatabaseDiff(
        [
            SchemaDiff.Containing("billing") with
            {
                Tables = [
                TableDiff.Added("billing", new Table
                    {
                        Name = "invoices",
                        Columns = [new Column { Name = "customer_id", Type = SqlType.Int }],
                        ForeignKeys = [foreignKey],
                    }) with
                {
                    ForeignKeys = [ForeignKeyDiff.Added(foreignKey)],
                },
            ],
            },
        ]);
    }

    [Fact]
    public void ScopedTo_AddedForeignKeyWhoseTargetIsOutOfScopeAndAbsent_IsLeftOutWithAWarning()
    {
        // Arrange — the table is created in scope, but references app.customers, which this run will neither
        // create (out of scope) nor find (absent from the current database). A plan that emitted it would fail.
        var diff = AddedInvoicesReferencing(new ObjectAddress("app", "customers"));

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("billing", "invoices")]), CurrentDatabase());

        // Assert — the table is still created, just without the constraint, and the omission is reported.
        result.IsSuccess.ShouldBeTrue();
        var table = result.Value!.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.ForeignKeys.ShouldBeEmpty();
        table.Definition!.ForeignKeys.ShouldBeEmpty(); // a created table carries its constraints inline
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.ForeignKeyTargetOutOfScope([new MemberAddress("billing", "invoices", "fk_invoices_customer")]));
    }

    [Fact]
    public void ScopedTo_AddedForeignKeyWhoseTargetIsOutOfScopeButPresent_IsKept()
    {
        // Arrange — app.users already exists, so the constraint applies cleanly even unscoped.
        var diff = AddedInvoicesReferencing(new ObjectAddress("app", "users"));

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("billing", "invoices")]), CurrentDatabase());

        // Assert
        var table = result.Value!.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.ForeignKeys.ShouldHaveSingleItem();
        table.Definition!.ForeignKeys.ShouldHaveSingleItem();
        result.Diagnostics.ShouldBeEmpty();
    }

    private static DatabaseDiff AddsColumnTyped(SqlType type) => new(
    [
        SchemaDiff.Containing("billing") with
        {
            Tables = [
            TableDiff.Modified("billing", "orders") with
            {
                Columns = [
                ColumnDiff.Added(new Column { Name = "state", Type = type }),
            ],
            },
        ],
        },
    ]);

    [Fact]
    public void ScopedTo_AdditionTypedByAnOutOfScopeAbsentType_Blocks()
    {
        // Arrange — the column names app.status, which this run will neither create (out of scope) nor find.
        // A type is part of the column's shape, so unlike a constraint there is nothing to leave out.
        var diff = AddsColumnTyped(SqlType.Custom("app", "status"));

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("billing", "orders")]), CurrentDatabase());

        // Assert — blocked, with the plan still carried for review.
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.TypeTargetOutOfScope(
                [new MemberAddress("billing", "orders", "state")],
                [new ObjectAddress("app", "status")]));
    }

    [Fact]
    public void ScopedTo_AdditionTypedByAnOutOfScopeTypeThatExists_IsAllowed()
    {
        // Arrange — app.status is already in the database, so the column applies cleanly even unscoped.
        var current = DatabaseWithEnumTypedColumn(SqlType.Custom("app", "status"));

        // Act
        var result = AddsColumnTyped(SqlType.Custom("app", "status"))
            .ScopedTo(PlanningScope.To([Target("billing", "orders")]), current);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_AdditionTypedByABareName_IsNotBlocked()
    {
        // Arrange — a bare name is resolved against what already exists, so it can only ever reach something
        // present. Nothing to block on, and a guess must not block a plan that need not be.
        var result = AddsColumnTyped(SqlType.Custom("status"))
            .ScopedTo(PlanningScope.To([Target("billing", "orders")]), CurrentDatabase());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ScopedTo_ForeignKeyAddedToAnExistingTable_IsLeftOutWhenItsTargetIsUnreachable()
    {
        // Arrange — a modified table carries the constraint as its own change rather than inline.
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Containing("billing") with
            {
                Tables = [
                TableDiff.Modified("billing", "orders") with
                {
                    ForeignKeys = [
                    ForeignKeyDiff.Added(new ForeignKey
                    {
                        Name = "fk_orders_customer",
                        ColumnNames = ["customer_id"],
                        References = new ObjectAddress("app", "customers"),
                        ReferencedColumnNames = ["id"],
                    }),
                ],
                },
            ],
            },
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("billing", "orders")]), CurrentDatabase());

        // Assert
        var table = result.Value!.Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();
        table.ForeignKeys.ShouldBeEmpty();
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.ForeignKeyTargetOutOfScope([new MemberAddress("billing", "orders", "fk_orders_customer")]));
    }

    [Fact]
    public void ScopedTo_ObjectTargeted_TheContainersCreationRidesAlong()
    {
        // Arrange — a targeted object cannot exist without its schema, so the container's Add is a
        // dependency of the target, not a schema-level facet to strip.
        var diff = new DatabaseDiff(
        [
            SchemaDiff.Added("app") with
            {
                Tables = [
                    TableDiff.Added("app", new Table { Name = "users" }),
                    TableDiff.Added("app", new Table { Name = "orders" }),
                ],
            },
        ]);

        // Act
        var result = diff.ScopedTo(PlanningScope.To([Target("app", "users")]), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.ShouldHaveSingleItem();
        app.Kind.ShouldBe(ChangeKind.Add);
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");
    }

    [Fact]
    public void ScopedTo_ObjectTargetedTeardown_DoesNotRemoveTheContainer_AndStillSeversBeyondTheTarget()
    {
        // Act — target one table of the teardown: the schema itself stays, and the out-of-scope closure
        // works exactly as it does for a schema-granular scope.
        var result = TeardownDiff().ScopedTo(PlanningScope.To([Target("app", "users")]), CurrentDatabase());

        // Assert
        var app = result.Value!.Schemas.Single(s => s.Name == "app");
        app.Kind.ShouldBeNull(); // the container is not covered, so it is not removed
        app.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");

        var billing = result.Value.Schemas.Single(s => s.Name == "billing");
        billing.Tables.ShouldHaveSingleItem().ForeignKeys.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        billing.Views.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        result.Diagnostics.Count.ShouldBe(2);
    }

    [Fact]
    public void ScopedTo_ATargetedDependent_IsInScopeThroughItsOwner_AndNotSevered()
    {
        // Act — billing.orders is targeted too, so its removal is part of the plan and its constraint goes
        // with the table; only the untargeted view is severed.
        var result = TeardownDiff().ScopedTo(
            PlanningScope.To([Target("app", "users"), Target("billing", "orders")]), CurrentDatabase());

        // Assert
        var billing = result.Value!.Schemas.Single(s => s.Name == "billing");
        billing.Tables.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        billing.Views.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);
        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(
            DiffDiagnostics.InferredSeveredOutOfScope([new ObjectAddress("billing", "summary")]));
    }
}
