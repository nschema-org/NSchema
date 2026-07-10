using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.State.Push;

internal static class StatePushOptions
{
    public static readonly OptionBinding<bool> NoLock = OptionBinding.Create<bool>()
        .FromOption("--no-lock")
        .WithDescription("Push without acquiring the state lock. You take responsibility for preventing concurrent runs (e.g. when operating under a manually-held lock).");

    public static IEnumerable<Option> All =>
    [
        NoLock.Option,
    ];
}
