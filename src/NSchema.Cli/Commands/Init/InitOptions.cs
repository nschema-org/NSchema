using System.CommandLine;
using NSchema.Cli.Configuration.Schema;

namespace NSchema.Cli.Commands.Init;

internal static class InitOptions
{
    public static readonly Option<SchemaFormat> Format = new("--format")
    {
        Description = "Format for the generated config and sample schema: yaml (default) or json.",
    };

    public static readonly Option<bool> Force = new("--force")
    {
        Description = "Overwrite an existing nschema.json.",
    };
}
