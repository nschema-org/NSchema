using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

/// <summary>
/// A configuration source that projects explicitly-specified command-line options onto configuration keys.
/// </summary>
/// <remarks>
/// Only scalar options are projected. List options (e.g. <c>--schema</c>) are applied separately.
/// </remarks>
internal sealed class ParseResultConfigurationSource(ParseResult parseResult) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new ParseResultConfigurationProvider(parseResult);
}

/// <summary>
/// The provider backing <see cref="ParseResultConfigurationSource"/>.
/// </summary>
internal sealed class ParseResultConfigurationProvider(ParseResult parseResult) : ConfigurationProvider
{
    /// <summary>
    /// Maps each scalar command-line option to the configuration key it overrides.
    /// </summary>
    private static readonly Binding[] _scalarBindings =
    [
        Binding.For(CliOptions.ConnectionString, nameof(NSchemaConfiguration.ConnectionString)),
        Binding.For(CliOptions.Provider, nameof(NSchemaConfiguration.Provider)),
        Binding.For(CliOptions.Destructive, nameof(NSchemaConfiguration.DestructiveActionPolicy)),
        Binding.For(CliOptions.StateFile, $"{nameof(NSchemaConfiguration.State)}:{nameof(StateConfig.File)}"),
        Binding.For(CliOptions.AutoApprove, nameof(NSchemaConfiguration.AutoApprove)),
    ];

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in _scalarBindings)
        {
            // A value is only contributed when the option was actually present on the command line. An option
            // left at its default (Implicit) must not override the config file or environment variables.
            if (parseResult.GetResult(binding.Option) is { Implicit: false })
            {
                data[binding.Key] = binding.ReadValue(parseResult);
            }
        }

        Data = data;
    }

    /// <summary>Pairs a strongly-typed option with the configuration key it populates.</summary>
    private sealed record Binding(Option Option, Func<ParseResult, string?> ReadValue, string Key)
    {
        public static Binding For<T>(Option<T> option, string key) =>
            new(option, parseResult => Convert.ToString(parseResult.GetValue(option), CultureInfo.InvariantCulture), key);
    }
}
