using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private static NSchemaHost Build(
        IMigrationPlanProvider planProvider,
        IMigrationExecutor executor,
        IHostApplicationLifetime lifetime,
        MigrationOptions? options = null
    ) => new(Options.Create(options ?? new MigrationOptions()), Substitute.For<IMigrationReporter>(), Substitute.For<IMigrationPlanRenderer>(), lifetime, planProvider, executor);

    private static IMigrationPlanProvider PlanProviderReturning(MigrationPlan plan)
    {
        var p = Substitute.For<IMigrationPlanProvider>();
        p.ComputeMigrationPlan(Arg.Any<CancellationToken>()).Returns(Task.FromResult(plan));
        return p;
    }

    private static MigrationPlan EmptyPlan() => new([], DatabaseSchema.Create([]));

    [Fact]
    public async Task Execute_DryRun_InvokesExecutorWithDryRunFlag()
    {
        var executor = Substitute.For<IMigrationExecutor>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(EmptyPlan()),
            executor,
            lifetime,
            new MigrationOptions { DryRun = true });

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await executor.Received(1).Execute(Arg.Any<MigrationPlan>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NotDryRun_PassesPlanToExecutor()
    {
        var plan = new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([]));
        var executor = Substitute.For<IMigrationExecutor>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(plan),
            executor,
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await executor.Received(1).Execute(plan, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AlwaysStopsApplication_OnSuccess()
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(EmptyPlan()),
            Substitute.For<IMigrationExecutor>(),
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
            Substitute.For<IMigrationExecutor>(),
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(async () => await sut.ExecuteTask!);

        lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_NotDryRun_StillCallsExecutor_WhenPlanIsEmpty()
    {
        // The executor itself decides what to do with an empty plan; the host should still hand it over.
        var executor = Substitute.For<IMigrationExecutor>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = Build(
            PlanProviderReturning(EmptyPlan()),
            executor,
            lifetime);

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await executor.Received(1).Execute(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
