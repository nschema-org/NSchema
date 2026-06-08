using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Destroy;

internal static class DestroyOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString);

    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve")
        .WithDescription("Skip the interactive confirmation prompt and tear down the schema immediately.");

    public static IEnumerable<Option> All =>
    [
        AutoApprove.Option,
    ];
}
