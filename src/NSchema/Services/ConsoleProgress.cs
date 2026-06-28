using NSchema.Operations.Progress;

namespace NSchema.Services;

/// <summary>
/// The CLI sink for an operation's transient progress narration.
/// </summary>
internal sealed class ConsoleProgress(IConsoleMessenger messenger) : IProgress<OperationProgress>
{
    public void Report(OperationProgress value) =>
        messenger.Report(
            value.Level == ProgressLevel.Detail ? MessageKind.Verbose : MessageKind.Progress,
            value.Message);
}
