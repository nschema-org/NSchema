using Microsoft.Extensions.DependencyInjection;
using NSchema.Deployment.Plugins;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Operations;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Refresh</c> and the offline <c>Plan</c>-against-captured-state workflow through a real
/// <see cref="NSchemaApplication"/> backed by an on-disk file state store.
/// </summary>
public sealed class RefreshEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _statePath;

    public RefreshEndToEndTests()
    {
        Directory.CreateDirectory(_tempDir);
        _statePath = Path.Combine(_tempDir, "state.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteNsql(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static Database LiveSchema() => new Database { Schemas = [new Schema { Name = "app", Tables = [new Table { Name = "users", Columns = [new Column { Name = "id", Type = SqlType.Int }] }] }] };

    [Fact]
    public async Task Refresh_WritesLiveSchemaToStateStore()
    {
        var store = new RecordingStateStore();
        using var app = NSchemaApplication.CreateBuilder()
            .UseStateStore(store)
            .Tap(b => b.Services.AddSingleton<IDatabaseIntrospector>(new InMemoryIntrospector(LiveSchema())))
            .UseSqlDialect<StubSqlDialect>().Build();

        await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken);

        store.Written.ShouldNotBeNull().Database.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task Refresh_WhenTheDatabaseIsUnreachable_FailsWithADiagnostic()
    {
        // Arrange — the reported bug: an unreachable database used to escape refresh as a bare exception, leaving the
        // operator with a message and no indication of what to do about it. The provider reports it instead, carrying
        // the inner cause the way a real one does.
        var unreachable = Diagnostic.Error("postgres",
            "Could not read the live database: Failed to connect to 127.0.0.1:5432 -> Connection refused");
        using var app = NSchemaApplication.CreateBuilder()
            .UseStateStore(new RecordingStateStore())
            .Tap(b => b.Services.AddSingleton<IDatabaseIntrospector>(new FailingIntrospector(unreachable)))
            .UseSqlDialect<StubSqlDialect>().Build();

        // Act
        var result = await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert — a reportable failure carrying both levels of the cause, not a thrown exception.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Failed to connect to 127.0.0.1:5432"));
        result.Errors.ShouldContain(d => d.Message.Contains("Connection refused"));
    }

    private sealed class FailingIntrospector(Diagnostic diagnostic) : IDatabaseIntrospector
    {
        public ValueTask<Result<Database>> GetDatabase(PlanningScope scope, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Failure<Database>(diagnostic));
    }

    [Fact]
    public async Task Refresh_ThenOfflinePlan_AgainstCapturedState_SeesNoChanges()
    {
        // 1. Capture the live schema to the store via Refresh.
        using (var capture = NSchemaApplication.CreateBuilder()
            .UseFileState(_statePath)
            .Tap(b => b.Services.AddSingleton<IDatabaseIntrospector>(new InMemoryIntrospector(LiveSchema())))
            .Build())
        {
            await capture.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken);
        }

        // 2. Plan offline against the captured state with a matching desired schema — no live database involved.
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var planner = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions())
            .UseFileState(_statePath)
            .AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired))
            .UseSqlDialect<StubSqlDialect>().Build();

        var result = await planner.Operations.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.IsEmpty.ShouldBeTrue();
    }
}
