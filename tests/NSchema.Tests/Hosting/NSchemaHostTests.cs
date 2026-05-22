using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private static NSchemaHost Build(
        IMigrationPlanProvider planProvider,
        ISqlPlanner planner,
        ISqlExecutor executor,
        IHostApplicationLifetime lifetime,
        MigrationOptions? options = null)
        => new(
            NullLogger<NSchemaHost>.Instance,
            Options.Create(options ?? new MigrationOptions()),
            lifetime,
            planProvider,
            planner,
            executor);

    private static IMigrationPlanProvider PlanProviderReturning(MigrationPlan plan)
    {
        var p = Substitute.For<IMigrationPlanProvider>();
        p.ComputeMigrationPlan(Arg.Any<CancellationToken>()).Returns(Task.FromResult(plan));
        return p;
    }

    private static ISqlPlanner PlannerReturning(SqlPlan plan)
    {
        var p = Substitute.For<ISqlPlanner>();
        p.Plan(Arg.Any<MigrationPlan>()).Returns(plan);
        return p;
    }

    [Fact]
    public async Task Execute_DryRun_DoesNotInvokeExecutor()
    {
        var executor = Substitute.For<ISqlExecutor>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(new MigrationPlan([])),
            PlannerReturning(new SqlPlan([new SqlStatement("SELECT 1;")])),
            executor,
            lifetime,
            new MigrationOptions { DryRun = true });

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await executor.DidNotReceive().Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NotDryRun_PassesPlannerOutputToExecutor()
    {
        var sqlPlan = new SqlPlan([new SqlStatement("SELECT 1;")]);
        var executor = Substitute.For<ISqlExecutor>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(new MigrationPlan([new CreateSchema("app")])),
            PlannerReturning(sqlPlan),
            executor,
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await executor.Received(1).Execute(sqlPlan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AlwaysStopsApplication_OnSuccess()
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(new MigrationPlan([])),
            PlannerReturning(new SqlPlan([])),
            Substitute.For<ISqlExecutor>(),
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_AlwaysStopsApplication_WhenPipelineThrows()
    {
        var planProvider = Substitute.For<IMigrationPlanProvider>();
        planProvider.ComputeMigrationPlan(Arg.Any<CancellationToken>())
            .Returns<Task<MigrationPlan>>(_ => throw new InvalidOperationException("boom"));
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            planProvider,
            PlannerReturning(new SqlPlan([])),
            Substitute.For<ISqlExecutor>(),
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(async () => await sut.ExecuteTask!);

        lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_NotDryRun_StillCallsExecutor_WhenPlanIsEmpty()
    {
        // The executor itself decides what to do with an empty plan; the host should still hand it over.
        var executor = Substitute.For<ISqlExecutor>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(new MigrationPlan([])),
            PlannerReturning(new SqlPlan([])),
            executor,
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await executor.Received(1).Execute(Arg.Any<SqlPlan>(), Arg.Any<CancellationToken>());
    }
}
