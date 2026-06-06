using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Configuration.Schema;

internal static class SchemaOptions
{
    public static readonly OptionBinding<SchemaFormat> Format = OptionBinding.Create<SchemaFormat>()
        .FromOption("--format")
        .WithDescription("The format the desired schema is expressed in: yaml (default) or json.");

    public static readonly OptionBinding<string> Directory = OptionBinding.Create<string>()
        .FromOption("--schema-dir")
        .WithDescription("Directory containing the desired-schema files. Required for plan and apply unless set in config.");

    public static readonly OptionBinding<string> Pattern = OptionBinding.Create<string>()
        .FromOption("--schema-pattern")
        .WithDescription("Glob matched within the schema directory. Defaults to a per-format pattern (e.g. **/*.yaml).");

    public static IEnumerable<Option> All => [Format.Option, Directory.Option, Pattern.Option];
}
