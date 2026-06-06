using System.CommandLine;
using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Configuration.Provider;

internal static class ProviderOptions
{
    public static readonly OptionBinding<ProviderType> Type = new(
        "--provider", EnvironmentVariables.Provider, Enum.Parse<ProviderType>)
    {
        Description = "Database provider supplying the live schema (e.g. postgres).",
    };

    public static readonly OptionBinding<string> ConnectionString = new(
        "--connection-string", EnvironmentVariables.ConnectionString)
    {
        Description = "Connection string for the database provider.",
    };

    public static IEnumerable<Option> All => [Type.Option, ConnectionString.Option];
}
