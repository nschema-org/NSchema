using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Validate;

internal static class ValidateOptions
{
    public static readonly OptionBinding<string> SchemaDirectory = OptionBinding.Create<string>()
        .FromOption("--schema-dir")
        .WithDescription("Directory containing the desired-schema files to validate. Required unless set in config.");

    public static IEnumerable<Option> All => [SchemaDirectory.Option];
}
