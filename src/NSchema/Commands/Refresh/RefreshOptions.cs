using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Refresh;

internal static class RefreshOptions
{
    public static readonly OptionBinding<bool> NoLock = OptionBinding.Create<bool>()
        .FromOption("--no-lock")
        .WithDescription("Refresh without acquiring the state lock. You take responsibility for preventing concurrent runs (e.g. when operating under a manually-held lock).");

    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force")
        .WithDescription("Replace an existing state payload that cannot be read, resetting the run-once script ledger. Without it, an unreadable payload fails the refresh.");

    public static IEnumerable<Option> All =>
    [
        NoLock.Option,
        Force.Option,
    ];
}
