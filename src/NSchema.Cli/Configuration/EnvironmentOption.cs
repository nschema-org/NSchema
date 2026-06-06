using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace NSchema.Cli.Configuration;

internal class EnvironmentOption<T> where T : notnull
{
    private readonly Option<T> _option;
    private readonly string? _envVar;
    private readonly Func<string, T>? _envParser;

    /// <summary>
    ///
    /// </summary>
    /// <param name="option"></param>
    /// <param name="envVar"></param>
    /// <param name="envParser"></param>
    public EnvironmentOption(Option<T> option, string? envVar = null, Func<string, T>? envParser = null)
    {
        _option = option;
        _envVar = envVar;
        _envParser = envParser;
    }

    public bool TryGetOverride(ParseResult result, [NotNullWhen(true)] out T? value)
    {
        if (result.GetResult(_option) is { Implicit: false } argument)
        {
            value = argument.GetRequiredValue(_option);
            return true;
        }

        if (_envVar != null && Environment.GetEnvironmentVariable(_envVar) is { } envValue)
        {
            if (_envParser == null)
            {
                throw new InvalidOperationException("Environment variable override specified without value parser.");
            }

            value = _envParser(envValue);
            return true;
        }

        value = default;
        return false;
    }
}
