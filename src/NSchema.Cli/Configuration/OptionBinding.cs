using System.CommandLine;

namespace NSchema.Cli.Configuration;

/// <summary>
/// A single configuration binding: between a CLI option, and an environment-variable fallback.
/// </summary>
internal sealed class OptionBinding<T> where T : notnull
{
    private readonly string? _envVar;
    private readonly Func<string, T>? _parser;

    /// <summary>
    /// The underlying System.CommandLine option, exposed so commands can register it.
    /// </summary>
    public Option<T> Option { get; }

    /// <summary>
    /// Wraps an existing option (use when the option needs extra configuration, e.g. multiple arguments per token).
    /// </summary>
    /// <param name="option">The command line option to bind.</param>
    /// <param name="envVar">An optional environment variable to bind.</param>
    /// <param name="parser">An optional parser to convert an environment variable string into its correct type.</param>
    public OptionBinding(Option<T> option, string? envVar = null, Func<string, T>? parser = null)
    {
        Option = option;
        _envVar = envVar;
        _parser = parser;
    }

    /// <summary>
    /// Creates an option named <paramref name="name"/> bound to <paramref name="envVar"/>.
    /// </summary>
    public OptionBinding(string name, string? envVar = null, Func<string, T>? parser = null)
        : this(new Option<T>(name), envVar, parser) { }

    /// <summary>
    /// The description shown in <c>--help</c>; a passthrough to the wrapped option.
    /// </summary>
    public string? Description
    {
        get => Option.Description;
        init => Option.Description = value;
    }

    public void Bind(ParseResult result, Action<T> action)
    {
        if (result.GetResult(Option) is { Implicit: false } argument)
        {
            var value = argument.GetRequiredValue(Option);
            action(value);
            return;
        }

        if (_envVar is not null && Environment.GetEnvironmentVariable(_envVar) is { } raw)
        {
            var value = Parse(raw);
            action(value);
            return;
        }
    }

    private T Parse(string raw)
    {
        if (_parser is not null)
        {
            return _parser(raw);
        }

        // A string binding needs no parser: the raw value is already the target type.
        if (raw is T value)
        {
            return value;
        }

        throw new InvalidOperationException($"Environment variable '{_envVar}' overrides '{Option.Name}' but no value parser was supplied.");
    }
}
