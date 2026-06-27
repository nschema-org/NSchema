using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Lock.Release;

internal static class LockReleaseOptions
{
    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve", "-y")
        .WithDescription("Skip the interactive confirmation prompt and release the lock immediately.");

    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force")
        .WithDescription("Release whatever lock is held without naming its id. Use when you can't read the id first; otherwise pass the lock id so a lock taken since isn't released by mistake.");

    public static IEnumerable<Option> All =>
    [
        AutoApprove.Option,
        Force.Option,
    ];
}
