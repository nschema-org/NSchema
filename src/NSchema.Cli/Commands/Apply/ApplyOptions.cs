using System.CommandLine;

namespace NSchema.Cli.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly Option<bool> AutoApprove = new("--auto-approve")
    {
        Description = "Skip the interactive confirmation prompt and apply the plan immediately.",
    };
}
