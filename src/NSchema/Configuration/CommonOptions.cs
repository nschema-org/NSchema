using System.CommandLine.Completions;
using NSchema.Configuration.Binding;

namespace NSchema.Configuration;

/// <summary>
/// Options used by most/all commands.
/// </summary>
internal static class CommonOptions
{
    public static readonly OptionBinding<string> Directory = OptionBinding.Create<string>()
        .FromOption("--directory", "-C")
        .Recursive()
        .WithDescription("Project directory to run in. Defaults to the current directory.");

    public static readonly OptionBinding<string?> Environment = OptionBinding.Create<string?>()
        .FromOption("--environment", "-e")
        .FromEnvironmentVariable(EnvironmentVariables.Environment)
        .Recursive()
        .WithCompletions(CompleteEnvironmentNames)
        .WithDescription("Target environment. Layers the matching *.env.<name>.sql overlay files over the base configuration.");

    public static readonly OptionBinding<bool> NoColor = OptionBinding.Create<bool>()
        .FromOption("--no-color")
        .FromEnvironmentVariable(EnvironmentVariables.NoColor)
        .Recursive()
        .WithDescription("Disable colored output.");

    public static readonly OptionBinding<bool> Json = OptionBinding.Create<bool>()
        .FromOption("--json")
        .Recursive()
        .WithDescription("Emit machine-readable NDJSON output instead of formatted text.");

    public static readonly OptionBinding<bool> Verbose = OptionBinding.Create<bool>()
        .FromOption("--verbose", "-v")
        .Recursive()
        .WithDescription("Show verbose diagnostic detail (files read, object counts, per-run internals).");

    public static readonly OptionBinding<bool> Quiet = OptionBinding.Create<bool>()
        .FromOption("--quiet", "-q")
        .Recursive()
        .WithDescription("Suppress progress narration; show only outcomes, warnings, and results.");

    private static IEnumerable<string> CompleteEnvironmentNames(CompletionContext context)
    {
        try
        {
            var root = Directory.GetValueOrDefault(context.ParseResult, System.IO.Directory.GetCurrentDirectory());
            return ProjectGlobs.EnvironmentNames(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
