using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.ForceUnlock;

internal static class ForceUnlockOptions
{
    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force", "-f")
        .WithDescription("Skip the interactive confirmation prompt and release the lock immediately.");

    public static IEnumerable<Option> All =>
    [
        Force.Option,
    ];
}
