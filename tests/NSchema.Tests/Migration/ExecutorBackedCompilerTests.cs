using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

[Obsolete("Tests will be removed in future versions")]
public sealed class ExecutorBackedCompilerTests
{
    private readonly IMigrationExecutor _executor = Substitute.For<IMigrationExecutor>();

    private static MigrationPlan SomePlan() => new([new CreateSchema("app")], DatabaseSchema.Create([]));

    [Fact]
    public async Task Compile_DoesNotInvokeExecutor()
    {
        // Arrange
        var sut = new ExecutorBackedCompiler(_executor);

        // Act
        await sut.Compile(SomePlan());

        // Assert
        await _executor.DidNotReceive().Apply(Arg.Any<MigrationPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_IsEmpty()
    {
        // Arrange: a legacy executor reports as a side effect, so nothing can be surfaced as preview.
        var sut = new ExecutorBackedCompiler(_executor);

        // Act
        var execution = await sut.Compile(SomePlan());

        // Assert
        execution.Preview.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_AppliesPlanWithoutPlanOnly()
    {
        // Arrange
        var plan = SomePlan();
        var sut = new ExecutorBackedCompiler(_executor);

        // Act
        var execution = await sut.Compile(plan);
        await execution.Execute();

        // Assert
        await _executor.Received(1).Apply(plan, false, Arg.Any<CancellationToken>());
    }
}
