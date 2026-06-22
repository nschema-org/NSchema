using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Drift;

internal static class DriftOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope", "-s")
        .AllowMultipleArguments()
        .WithDescription("Limit the drift check to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<bool> DetailedExitCode = OptionBinding.Create<bool>()
        .FromOption("--detailed-exitcode")
        .WithDescription("Return a detailed exit code: 0 = no drift, 2 = drift detected (errors remain 1). For CI gating.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        DetailedExitCode.Option,
    ];
}
