using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diffing;
using NSchema.Domain.Migration;
using NSchema.Domain.Schema;
using NSchema.Extractors;
using NSchema.Migration;

namespace NSchema.Tests.Hosting;

public sealed class DefaultSchemaMigratorTests
{
    private readonly ISchemaExtractor _extractor = Substitute.For<ISchemaExtractor>();
    private readonly ISchemaDiffer _differ = Substitute.For<ISchemaDiffer>();
    private readonly IInstructionExecutor _executor = Substitute.For<IInstructionExecutor>();
    private readonly DatabaseModel _desired = new([]);

    private DefaultSchemaMigrator CreateMigrator() => new(NullLogger<DefaultSchemaMigrator>.Instance, _extractor, _differ, _executor, _desired);

    // ── MigrationPlan ─────────────────────────────────────────────────────────

    [Fact]
    public void MigrationPlan_IsEmpty_TrueWhenNoInstructions()
    {
        // Arrange
        var plan = new MigrationPlan([]);

        // Act & Assert
        plan.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void MigrationPlan_IsEmpty_FalseWhenInstructionsPresent()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("public")]);

        // Act & Assert
        plan.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void MigrationPlan_HasDestructiveInstructions_TrueWhenAnyInstructionIsDestructive()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("public"), new DropTable("public", "users")]);

        // Act & Assert
        plan.HasDestructiveInstructions.ShouldBeTrue();
    }

    [Fact]
    public void MigrationPlan_HasDestructiveInstructions_FalseWhenNoInstructionsAreDestructive()
    {
        // Arrange
        var plan = new MigrationPlan([new CreateSchema("public"), new CreateSchema("admin")]);

        // Act & Assert
        plan.HasDestructiveInstructions.ShouldBeFalse();
    }

    // ── SchemaMigrator.Plan ───────────────────────────────────────────────────

    [Fact]
    public async Task Plan_ExtractsCurrentSchemaAndDiffs()
    {
        // Arrange
        var current = new DatabaseModel([]);
        var instructions = new List<SchemaInstruction> { new CreateSchema("public") };
        _extractor.Extract(Arg.Any<CancellationToken>()).Returns(current);
        _differ.Diff(current, _desired).Returns(instructions);
        var migrator = CreateMigrator();

        // Act
        var plan = await migrator.Plan();

        // Assert
        plan.Instructions.ShouldBe(instructions);
    }

    [Fact]
    public async Task Plan_PassesExtractedCurrentAndDesiredModelToDiffer()
    {
        // Arrange
        var current = new DatabaseModel([new DatabaseSchema("public", [])]);
        _extractor.Extract(Arg.Any<CancellationToken>()).Returns(current);
        _differ.Diff(Arg.Any<DatabaseModel>(), Arg.Any<DatabaseModel>()).Returns([]);
        var migrator = CreateMigrator();

        // Act
        await migrator.Plan();

        // Assert
        _differ.Received(1).Diff(current, _desired);
    }

    // ── SchemaMigrator.Apply ──────────────────────────────────────────────────

    [Fact]
    public async Task Apply_WhenPlanIsEmpty_DoesNotCallExecutor()
    {
        // Arrange
        var plan = new MigrationPlan([]);
        _extractor.Extract(Arg.Any<CancellationToken>()).Returns(new DatabaseModel([]));
        _differ.Diff(Arg.Any<DatabaseModel>(), Arg.Any<DatabaseModel>()).Returns([]);

        var migrator = CreateMigrator();

        // Act
        await migrator.Apply(plan);

        // Assert
        await _executor.DidNotReceive().Execute(
            Arg.Any<IReadOnlyList<SchemaInstruction>>(),
            Arg.Any<ExecutionOptions?>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Apply_WhenPlanHasInstructions_ExecutesThem()
    {
        // Arrange
        var instructions = new List<SchemaInstruction> { new CreateSchema("public") };
        var plan = new MigrationPlan(instructions);

        _extractor.Extract(Arg.Any<CancellationToken>()).Returns(new DatabaseModel([]));

        var migrator = CreateMigrator();

        // Act
        await migrator.Apply(plan);

        // Assert
        await _executor.Received(1).Execute(instructions,
            Arg.Any<ExecutionOptions?>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Apply_PassesExecutionOptionsToExecutor()
    {
        // Arrange
        var instructions = new List<SchemaInstruction> { new CreateSchema("public") };
        var options = new ExecutionOptions(DestructiveActionPolicy.Allow);
        _extractor.Extract(Arg.Any<CancellationToken>()).Returns(new DatabaseModel([]));
        _differ.Diff(Arg.Any<DatabaseModel>(), Arg.Any<DatabaseModel>()).Returns(instructions);
        var migrator = CreateMigrator();

        var plan = new  MigrationPlan(instructions);

        // Act
        await migrator.Apply(plan, options);

        // Assert
        await _executor.Received(1).Execute(plan.Instructions, options,Arg.Any<CancellationToken>());
    }
}
