using System.CommandLine;
using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Configuration.Schema;

internal static class SchemaOptions
{
    public static readonly OptionBinding<SchemaFormat> Format = new("--format")
    {
        Description = "The format the desired schema is expressed in: yaml (default) or json.",
    };

    public static readonly OptionBinding<string> Directory = new("--schema-dir")
    {
        Description = "Directory containing the desired-schema files. Required for plan and apply unless set in config.",
    };

    public static readonly OptionBinding<string> Pattern = new("--schema-pattern")
    {
        Description = "Glob matched within the schema directory. Defaults to a per-format pattern (e.g. **/*.yaml).",
    };

    public static IEnumerable<Option> All => [Format.Option, Directory.Option, Pattern.Option];
}
