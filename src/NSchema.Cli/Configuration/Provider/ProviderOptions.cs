using System.CommandLine;

namespace NSchema.Cli.Configuration.Provider;

internal static class ProviderOptions
{
    public static readonly Option<ProviderType> Type = new("--provider")
    {
        Description = "Database provider supplying the live schema (e.g. postgres).",
    };

    public static readonly Option<string> ConnectionString = new("--connection-string")
    {
        Description = "Connection string for the database provider.",
    };

    public static readonly EnvironmentOption<string> ConnectionStringOption = new(ConnectionString, EnvironmentVariables.ConnectionString);
}
