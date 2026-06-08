using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Schema;

namespace NSchema.Commands.Validate;

internal static class ValidateOptions
{
    public static readonly OptionBinding<string> SchemaDirectory = OptionBinding.Create<string>()
        .FromOption("--schema-dir")
        .WithDescription("Directory containing the desired-schema files to validate. Required unless set in config.");

    public static readonly OptionBinding<SchemaFormat> SchemaFormat = OptionBinding.Create<SchemaFormat>()
        .FromOption("--format")
        .WithDescription("The format the desired schema is expressed in: yaml (default) or json.");

    public static readonly OptionBinding<string> SchemaPattern = OptionBinding.Create<string>()
        .FromOption("--schema-pattern")
        .WithDescription("Glob matched within the schema directory. Defaults to a per-format pattern (e.g. **/*.yaml).");

    public static IEnumerable<Option> All =>
    [
        SchemaDirectory.Option,
        SchemaFormat.Option,
        SchemaPattern.Option,
    ];
}
