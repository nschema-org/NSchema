using System.CommandLine;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Schema;

namespace NSchema.Cli.Commands.Init;

internal static class InitOptions
{
    public static readonly OptionBinding<SchemaFormat> Format = OptionBinding.Create<SchemaFormat>()
        .FromOption("--format")
        .WithDescription("Format for the generated config and sample schema: yaml (default) or json.");

    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force")
        .WithDescription("Overwrite an existing nschema.json.");

    public static IEnumerable<Option> All => [Format.Option, Force.Option];
}
