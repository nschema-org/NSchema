using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Show;

internal static class ShowOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the output to specific database schemas (namespaces). May be specified multiple times.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
    ];
}
