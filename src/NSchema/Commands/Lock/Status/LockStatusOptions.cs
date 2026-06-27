using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Lock.Status;

internal static class LockStatusOptions
{
    public static readonly OptionBinding<bool> DetailedExitCode = OptionBinding.Create<bool>()
        .FromOption("--detailed-exitcode")
        .WithDescription("Return a detailed exit code: 0 = not locked, 2 = locked (errors remain 1). For CI gating.");

    public static IEnumerable<Option> All =>
    [
        DetailedExitCode.Option,
    ];
}
