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
        .WithDescription("Tear down without acquiring the state lock.");

    public static readonly OptionBinding<bool> NoRefresh = OptionBinding.Create<bool>()
        .FromOption("--no-refresh")
        .WithDescription("Plan the teardown against the recorded state as-is, without capturing the live schema first.");

    public static readonly OptionBinding<bool> Ephemeral = OptionBinding.Create<bool>()
        .FromOption("--ephemeral")
        .WithDescription("Run against an in-memory state store, instead of a configured STATE store.");

    public static IEnumerable<Option> All =>
    [
        AutoApprove.Option,
        NoLock.Option,
        NoRefresh.Option,
        Ephemeral.Option,
    ];
}
