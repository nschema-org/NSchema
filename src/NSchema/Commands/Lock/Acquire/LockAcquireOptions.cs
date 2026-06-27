using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Lock.Acquire;

internal static class LockAcquireOptions
{
    public static readonly OptionBinding<string> Reason = OptionBinding.Create<string>()
        .FromOption("--reason")
        .WithDescription("A note recorded on the lock describing why it's held, shown by 'lock status' (default: manual).");

    public static readonly OptionBinding<string> Ttl = OptionBinding.Create<string>()
        .FromOption("--ttl")
        .WithDescription("Optional lifetime after which the lock is reported as expired (e.g. 30m, 2h, 90s, 1d). The lock is never auto-released.");

    public static IEnumerable<Option> All =>
    [
        Reason.Option,
        Ttl.Option,
    ];
}
