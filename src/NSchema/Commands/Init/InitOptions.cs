using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Schema;

namespace NSchema.Commands.Init;

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
