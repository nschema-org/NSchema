using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Init;

internal static class InitOptions
{
    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force", "-f")
        .WithDescription("Init even in a non-empty directory, overwriting any files.");

    public static IEnumerable<Option> All => [Force.Option];
}
