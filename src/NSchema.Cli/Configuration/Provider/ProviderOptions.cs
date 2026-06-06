using System.CommandLine;
using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Configuration.Provider;

internal static class ProviderOptions
{
    public static readonly OptionBinding<ProviderType> Type = OptionBinding.Create<ProviderType>()
        .FromOption("--provider")
        .FromEnvironmentVariable(EnvironmentVariables.Provider)
        .WithDescription("Database provider supplying the live schema (e.g. postgres).");

    public static readonly OptionBinding<string> ConnectionString = OptionBinding.Create<string>()
        .FromOption("--connection-string")
        .FromEnvironmentVariable(EnvironmentVariables.ConnectionString)
        .WithDescription("Connection string for the database provider.");

    public static IEnumerable<Option> All => [Type.Option, ConnectionString.Option];
}
