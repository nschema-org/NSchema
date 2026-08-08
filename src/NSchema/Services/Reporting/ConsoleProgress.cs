using NSchema.Operations.Progress;

namespace NSchema.Services.Reporting;

/// <summary>
/// The CLI sink for an operation's transient progress narration.
/// </summary>
internal sealed class ConsoleProgress(IConsoleReporter reporter) : IProgress<OperationProgress>
{
    public void Report(OperationProgress value) =>
        reporter.Report(
            value.Level == ProgressLevel.Detail ? MessageKind.Verbose : MessageKind.Progress,
            value.Message);
}
