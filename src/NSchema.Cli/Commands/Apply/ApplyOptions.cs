using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve")
        .WithDescription("Skip the interactive confirmation prompt and apply the plan immediately.");
}
