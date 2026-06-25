using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Init;

internal static class InitOptions
{
    public static readonly OptionBinding<bool> Force = OptionBinding.Create<bool>()
        .FromOption("--force", "-f")
        .WithDescription("Init even in a non-empty directory, overwriting any files.");

    public static readonly OptionBinding<ProviderKind> Provider = OptionBinding.Create<ProviderKind>()
        .FromOption("--provider")
        .WithDescription("Database provider to scaffold configuration for: postgres (default), sqlite, or sqlserver.");

    public static readonly OptionBinding<BackendKind> Backend = OptionBinding.Create<BackendKind>()
        .FromOption("--backend")
        .WithDescription("State backend to scaffold configuration for: file (default) or s3.");

    public static IEnumerable<Option> All => [Force.Option, Provider.Option, Backend.Option];
}
