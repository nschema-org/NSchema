using NSchema.Operations.Progress;
using NSchema.Operations.Workflow;

namespace NSchema.Operations;

/// <summary>
/// Loads the project and validates it against the configured project policies. Contacts no infrastructure.
/// </summary>
internal sealed class ValidateOperation(IMigrationWorkflow workflow, IProgress<OperationProgress> progress)
    : IOperation<ValidateArguments, Result<ValidateResult>>
{
    public async Task<Result<ValidateResult>> Execute(ValidateArguments args, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Validating project. No database or state store will be contacted."));

        var findings = await workflow.Validate(cancellationToken);
        return Result.From(new ValidateResult(), findings.Diagnostics);
    }
}
