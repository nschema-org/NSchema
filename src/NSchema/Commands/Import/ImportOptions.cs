using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Import;

internal static class ImportOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope", "-s")
        .AllowMultipleArguments()
        .WithDescription("Limit the import to specific database schema namespaces. May be specified multiple times.");

    public static readonly OptionBinding<string> OutputDirectory = OptionBinding.Create<string>()
        .FromOption("--out-dir", "-o")
        .WithDescription("Directory to write the imported SQL files into. Defaults to the current directory.");

    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force", "-f")
        .WithDescription("Overwrite existing .sql files in the output directory.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        OutputDirectory.Option,
        Force.Option,
    ];
}
