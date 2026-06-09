using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Init;

internal static class InitOptions
{
    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force")
        .WithDescription("Overwrite an existing nschema.json.");

    public static IEnumerable<Option> All => [Force.Option];
}
