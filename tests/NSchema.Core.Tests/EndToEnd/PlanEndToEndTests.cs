using Microsoft.Extensions.DependencyInjection;
using NSchema.Deployment.Plugins;
using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Operations;
using NSchema.State;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Plan</c> through a real <see cref="NSchemaApplication"/>: desired schema from a DDL file on disk,
/// current schema from an in-memory provider, asserting on the structured diff the pipeline reports.
/// </summary>
public sealed class PlanEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public PlanEndToEndTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteNsql(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplicationBuilder NewBuilder(Database current)
    {
        var builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions());
        builder.Services.AddSingleton<IDatabaseIntrospector>(new InMemoryIntrospector(current));
        // Planning requires a state store; nothing here needs to outlive the app, so an in-memory one suffices.
        builder.UseEphemeralState();
        return builder;
    }

    [Fact]
    public async Task Plan_ReportsStructuredDiffBetweenCurrentAndDesired()
    {
        // Arrange
        // Current: app.users(id). Desired: app.users(id, email) + a new app.orders table.
        var current = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }] };

        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL,
                email text NOT NULL
            );
            CREATE TABLE app.orders
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();

        // A plan diffs recorded state against the project, so the refresh is what puts the current schema on record.
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);

        var schema = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("app");

        // Act
        var users = schema.Tables.Single(t => t.Name.Value.Equals("users"));

        // Assert
        users.Change.ShouldBe(ChangeKind.Modify);
        users.Columns.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            c => c.Name.ShouldBe("email"),
            c => c.Change.ShouldBe(ChangeKind.Add));

        schema.Tables.Single(t => t.Name.Value.Equals("orders")).Change.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public async Task Plan_WithNoChanges_ReportsEmptyDiff()
    {
        var schema = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }] };

        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(schema).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();

        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);

        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_WithDialectRegistered_ProducesSql()
    {
        var current = new Database { Schemas = [] };
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current)
            .AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired))
            .UseSqlDialect<StubSqlDialect>()
            .Build();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);

        // The stub emits one statement per action; creating a schema yields a CreateSchema action.
        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Plan_WithoutProvider_Fails()
    {
        var current = new Database { Schemas = [] };
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).Build();

        var result = await app.Operations.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        // A dialect is required for planning — there is no SQL-less plan.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Planning requires a database provider"));
    }

    [Fact]
    public async Task Plan_PartialTeardown_SeversWhatItCostsOutsideItsScope()
    {
        // Arrange
        // A scoped teardown is the one plan that can be asked to remove something another schema depends on.
        // billing.orders keeps its rows; only the constraint aimed into app goes.
        var current = new Database
        {
            Schemas = [
            new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] },
            new Schema { Name = "billing", Tables = [
                new Table { Name = "orders", Columns = [new Column { Name = "id", Type = SqlType.Int }],
                    ForeignKeys = [
                        new ForeignKey { Name = "fk_orders_user", ColumnNames = ["id"],
                            References = new ObjectAddress("app", "users"), ReferencedColumnNames = ["id"] },
                    ] },
            ] },
        ],
        };
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // A teardown destroys what NSchema manages; management is normally established by an apply, adopted
        // here through state surgery. billing stays unmanaged — the plan may only sever, never tear it down.
        await Manage(app, new IdentitySet(
            DatabaseObjects: [DatabaseAddress.Schema("app")],
            SchemaObjects: [new ObjectAddress("app", "users") with { Kind = SchemaObjectKind.Table }]));

        var result = await app.Operations.Plan(
            new PlanArguments { Target = PlanTarget.Empty, Scope = PlanningScope.To(DatabaseAddress.Schema("app")) },
            TestContext.Current.CancellationToken);

        var diff = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff;

        // app goes entirely...
        diff.Schemas.Single(s => s.Name.Value.Equals("app")).Change.ShouldBe(ChangeKind.Remove);

        // Act
        // ...and billing is disturbed, but not torn down: the table survives, minus the constraint.
        var billing = diff.Schemas.Single(s => s.Name.Value.Equals("billing"));

        // Assert
        billing.Change.ShouldBe(ChangeKind.Touched);
        var orders = billing.Tables.ShouldHaveSingleItem();
        orders.Change.ShouldBe(ChangeKind.Modify);
        orders.ForeignKeys.ShouldHaveSingleItem().Change.ShouldBe(ChangeKind.Remove);

        // And the reach outside the scope is announced rather than done quietly.
        result.Diagnostics.ShouldContain(d => d.Code == "severed-out-of-scope" && d.Message.Contains("billing.orders.fk_orders_user"));
    }

    [Fact]
    public async Task Plan_ObjectTargeted_ConvergesTheTargetAlone()
    {
        // Targeting one object out of a wider project: its sibling stays out of the diff, the container's
        // creation rides along as a dependency, and management is claimed for exactly what the plan covers.
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app; CREATE TABLE app.users (id int); CREATE TABLE app.orders (id int);");

        using var app = NewBuilder(new Database()).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();

        var users = new ObjectAddress("app", "users");
        var result = await app.Operations.Plan(
            new PlanArguments { Scope = PlanningScope.To([users]) },
            TestContext.Current.CancellationToken);

        var plan = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull();

        var schema = plan.Diff.Schemas.ShouldHaveSingleItem();
        schema.Change.ShouldBe(ChangeKind.Add);
        schema.Tables.ShouldHaveSingleItem().Name.ShouldBe("users");

        plan.Managed.Schemas.Select(s => s.Name).ShouldBe(["app"]);
        plan.Managed.SchemaObjects.ShouldHaveSingleItem().ShouldBe(users with { Kind = SchemaObjectKind.Table });
    }

    [Fact]
    public async Task Plan_Teardown_DiffsTheManagedSchemaDownToNothing()
    {
        // The managed schema is the recorded state, so the refresh records the live schema before tearing down.
        var current = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }] };
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        // A teardown destroys what NSchema manages; management is normally established by an apply, adopted
        // here through state surgery.
        await Manage(app, new IdentitySet(
            DatabaseObjects: [DatabaseAddress.Schema("app")],
            SchemaObjects: [new ObjectAddress("app", "users") with { Kind = SchemaObjectKind.Table }]));

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Empty }, TestContext.Current.CancellationToken);

        // A teardown is fully destructive, so the default policy blocks it — it must stay possible, but it does
        // not have to be easy. The block still carries the complete artifact, so review loses nothing.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Source == "destructive-actions");

        // The teardown plan drops the managed table.
        var schema = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.Schemas.ShouldHaveSingleItem();
        schema.Tables.ShouldContain(t => t.Name.Value.Equals("users") && t.Change == ChangeKind.Remove);
    }

    /// <summary>Adopts identities into management through state surgery: pull, mutate, push.</summary>
    private static async Task Manage(NSchemaApplication app, IdentitySet managed)
    {
        var read = await app.State.Read(new StateReadArguments(), TestContext.Current.CancellationToken);
        var state = read.Value.ShouldNotBeNull().State.ShouldNotBeNull();
        (await app.State.Write(new StateWriteArguments(state with { Managed = managed }), TestContext.Current.CancellationToken))
            .IsSuccess.ShouldBeTrue();
    }
}
