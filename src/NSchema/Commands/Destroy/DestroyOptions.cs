using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Destroy;

internal static class DestroyOptions
{
    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve", "-y")
        .WithDescription("Skip the interactive confirmation prompt and tear down the schema immediately.");

    public static readonly OptionBinding<bool> NoLock = OptionBinding.Create<bool>()
        .FromOption("--no-lock")
        .WithDescription("Tear down without acquiring the state lock. You take responsibility for preventing concurrent runs (e.g. when operating under a manually-held lock).");

    public static readonly OptionBinding<bool> Ephemeral = OptionBinding.Create<bool>()
        .FromOption("--ephemeral")
        .WithDescription("Run against an in-memory state store discarded when the command exits, instead of a configured STATE store — for CI runs against disposable databases. Run-once script history does not persist.");

    public static IEnumerable<Option> All =>
    [
        AutoApprove.Option,
        NoLock.Option,
        Ephemeral.Option,
    ];
}
