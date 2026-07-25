using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;

namespace NSchema.Tests.Operations;

public sealed class ValidateOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ValidateOperation _sut;

    public ValidateOperationTests()
    {
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _sut = new ValidateOperation(_workflow, _progress);
    }

    [Fact]
    public async Task Execute_WhenSchemaClean_SucceedsWithNoFindings()
    {
        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_WhenSchemaInvalid_FailsWithTheFindings()
    {
        // Arrange — an error finding fails the operation the same way a blocked plan does; the findings ride the result.
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(Result.From(Diagnostic.Error("P1", "bad schema")));

        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
    }

    [Fact]
    public async Task Execute_CarriesAdvisoryFindings_WithoutFailing()
    {
        // Arrange
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(Result.From(new Diagnostic("P1", "lint", DiagnosticSeverity.Warning)));

        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public async Task Execute_ReportsTheValidatingStep()
    {
        // Act
        await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        _progress.Received().Report(OperationProgress.Step("Validating project. No database or state store will be contacted."));
    }
}
