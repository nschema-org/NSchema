using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Completion;

internal static class CompletionOptions
{
    public static readonly OptionBinding<bool> Install = OptionBinding.Create<bool>()
        .FromOption("--install-autocomplete")
        .WithDescription("Install the completion script into the shell's startup file instead of printing it.");

    public static readonly OptionBinding<bool> Uninstall = OptionBinding.Create<bool>()
        .FromOption("--uninstall-autocomplete")
        .WithDescription("Remove the completion script from the shell's startup file.");

    public static IEnumerable<Option> All => [Install.Option, Uninstall.Option];
}
