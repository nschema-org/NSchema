using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly OptionBinding<bool> AutoApprove = new("--auto-approve")
    {
        Description = "Skip the interactive confirmation prompt and apply the plan immediately.",
    };
}
