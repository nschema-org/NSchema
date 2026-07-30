using Microsoft.Extensions.DependencyInjection;
using NSchema.Apply;
using NSchema.Deployment.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Operations;
using NSchema.State.Locks;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Apply</c> through a real <see cref="NSchemaApplication"/> with the database boundary faked: a stub
/// generator/executor stand in for the dialect and connection, and the on-disk state store captures the result.
/// </summary>
public sealed class ApplyEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly RecordingSqlExecutor _executor = new();
    private readonly RecordingStateStore _store = new();

    public ApplyEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteNsql(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplication BuildApp(Database current, string desiredPath) =>
        NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions())
            .AddProjectSource(Path.GetDirectoryName(desiredPath)!, Path.GetFileName(desiredPath))
            .UseStateStore(_store)
            .UseSqlDialect<StubSqlDialect>()
            .Tap(b =>
            {
                b.Services.AddSingleton<ISqlExecutor>(_executor);
                b.Services.AddSingleton<IDatabaseIntrospector>(new InMemoryIntrospector(current));
            })
            .Build();

    [Fact]
    public async Task Apply_GeneratesSql_Executes_AndRefreshesState()
    {
        // Current live DB: an empty app schema. Desired: app.users(id) — i.e. create the table.
        var current = new Database { Schemas = [new Schema { Name = "app" }] };
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = BuildApp(current, desired);

        // The CLI-style flow: hold the lock, refresh so state reflects the live database, plan, apply, release.
        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // The plan reached the executor as a non-empty SQL plan.
        _executor.Executed.ShouldNotBeNull().ShouldNotBeEmpty();
        // The plan exposes the same SQL the caller previews before applying.
        plan.Plan!.Statements.ShouldBe(_executor.Executed!);
        // Post-apply state was captured to the store.
        _store.Written.ShouldNotBeNull().Database.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task Apply_RequiredColumnAddWithMatchedMigration_ExecutesNullableAddBackfillThenTighten()
    {
        // Current live DB: a populated-shaped app.users(id). Desired: the same table gaining a NOT NULL,
        // defaultless email column, with a SCRIPT block declaring the backfill.
        var current = new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }] };
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL,
                email text NOT NULL
            );
            SCRIPT backfill_emails RUN ON ADD COLUMN app.users.email AS $$
            UPDATE app.users SET email = 'unknown@example.com';
            $$;
            """);

        using var app = BuildApp(current, desired);

        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // The add was decomposed around the backfill: nullable add, the block's SQL, then the tighten — in order.
        var statements = _executor.Executed.ShouldNotBeNull().Select(s => s.Sql).ToList();
        statements.ShouldBe([
            "-- AddColumn",
            "UPDATE app.users SET email = 'unknown@example.com';",
            "-- AlterColumn",
        ]);
    }

    [Fact]
    public async Task Apply_TemplateMigration_FiresPerSchemaWhereTheChangeIsPlanned()
    {
        // A template with a migration applied to two schemas: sales.events already exists without the new column
        // (the migration matches and the add decomposes, with {schema} bound), while billing.events is brand new
        // (created empty, so its instance is unmatched and reports as inert).
        var current = new Database
        {
            Schemas = [
            new Schema { Name = "sales", Tables = [new Table { Name = "events", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] },
            new Schema { Name = "billing" },
        ],
        };
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE audit
            BEGIN
                CREATE TABLE events
                (
                    id int NOT NULL,
                    actor text NOT NULL
                );
                SCRIPT backfill_actors RUN ON ADD COLUMN events.actor AS $$
                UPDATE {schema}.events SET actor = 'system';
                $$;
            END;
            APPLY TEMPLATE audit IN SCHEMA sales, billing;
            """);

        using var app = BuildApp(current, desired);

        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);
        var plan = result.Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // Sales decomposes around the token-substituted backfill; billing just creates its (empty) table.
        var statements = _executor.Executed.ShouldNotBeNull().Select(s => s.Sql).ToList();
        statements.ShouldBe([
            "-- CreateTable",
            "-- AddColumn",
            "UPDATE sales.events SET actor = 'system';",
            "-- AlterColumn",
        ]);

        // Billing's instance is inert this run and says so; sales' matched instance reports nothing.
        var inert = result.Diagnostics.Where(d => d.Code == "dead-migration").ShouldHaveSingleItem();
        inert.Message.ShouldContain("'billing.backfill_actors'");
        inert.Message.ShouldContain("billing.events.actor");
    }

    [Fact]
    public async Task Apply_RunOnceScript_RunsOnce_ThenLaterPlansSkipIt()
    {
        // A run-once seed script: the first plan includes and records it, the next plan skips it.
        var current = new Database { Schemas = [new Schema { Name = "app" }] };
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            SCRIPT seed_currencies RUN ONCE ON POST DEPLOYMENT AS $$
            INSERT INTO app.currencies VALUES ('GBP');
            $$;
            """);

        using var app = BuildApp(current, desired);

        // First run: the pending script is planned, executed, and recorded (the CLI-style flow threads
        // plan.RunOnceScripts into the apply).
        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var first = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        first.Plan!.Statements.Select(s => s.Sql).ShouldContain("INSERT INTO app.currencies VALUES ('GBP');");
        first.Plan!.Diff.AllScripts().ShouldHaveSingleItem().Name.ShouldBe("seed_currencies");
        await app.Operations.Apply(new ApplyArguments { Plan = first.Plan! }, TestContext.Current.CancellationToken);

        _store.Written.ShouldNotBeNull().Scripts.ShouldHaveSingleItem().Script.Name.ShouldBe("seed_currencies");

        // Second run: the script is skipped, and no longer up for recording.
        var second = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);
        second.Value!.Plan!.Statements.Select(s => s.Sql).ShouldNotContain("INSERT INTO app.currencies VALUES ('GBP');");
        second.Value!.Plan!.Diff.AllScripts().ShouldBeEmpty();
        second.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Apply_WithNoChanges_ShortCircuitsWithoutExecutingButStillCapturesState()
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

        using var app = BuildApp(schema, desired);

        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        // The plan carries an empty diff/sql: there is nothing to apply.
        plan.Plan.ShouldNotBeNull().Diff.IsEmpty.ShouldBeTrue();
        plan.Plan!.IsEmpty.ShouldBeTrue();

        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // Nothing to apply: the empty plan never reaches the executor...
        _executor.Executed.ShouldBeNull();
        // ...but a first run against an already-matching database still initialises the store.
        _store.Written.ShouldNotBeNull().Database.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }
}
