using System.CommandLine;

namespace NSchema.Cli.Configuration.Binding;

/// <summary>
/// Entry point for building <see cref="OptionBinding{T}"/> instances fluently.
/// </summary>
internal static class OptionBinding
{
    /// <summary>
    /// Starts building a binding whose resolved value is of type <typeparamref name="T"/>.
    /// </summary>
    public static OptionBinding<T> Create<T>() where T : notnull => new();
}

/// <summary>
/// A single configuration binding between a CLI option and an optional environment-variable fallback, applied with
/// CLI &gt; environment precedence. Built fluently via <see cref="OptionBinding.Create{T}"/>.
/// </summary>
internal sealed class OptionBinding<T> where T : notnull
{
    private string? _optionName;
    private string? _description;
    private bool _allowMultipleArguments;
    private string? _envVar;
    private Func<string, T>? _parser;
    private Option<T>? _option;

    internal OptionBinding()
    {
    }

    /// <summary>
    /// The underlying System.CommandLine option, built on first access and exposed so commands can register it.
    /// </summary>
    public Option<T> Option => _option ??= BuildOption();

    /// <summary>
    /// Names the CLI option (e.g. <c>--scope</c>).
    /// </summary>
    public OptionBinding<T> FromOption(string name)
    {
        _optionName = name;
        return this;
    }

    /// <summary>
    /// Adds an environment variable read when the option is not passed explicitly on the command line.
    /// </summary>
    public OptionBinding<T> FromEnvironmentVariable(string envVar)
    {
        _envVar = envVar;
        return this;
    }

    /// <summary>
    /// Sets the description shown in <c>--help</c>.
    /// </summary>
    public OptionBinding<T> WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Allows the option to accept multiple arguments per token (e.g. <c>--scope a b c</c>).
    /// </summary>
    public OptionBinding<T> AllowMultipleArguments()
    {
        _allowMultipleArguments = true;
        return this;
    }

    /// <summary>
    /// Overrides how the environment-variable string is parsed. Only needed when the defaults (identity for strings,
    /// <see cref="Enum.Parse(Type, string, bool)"/> for enums) are not appropriate.
    /// </summary>
    public OptionBinding<T> WithParser(Func<string, T> parser)
    {
        _parser = parser;
        return this;
    }

    /// <summary>
    /// Resolves this binding against the parsed command line and, if a value is present (CLI &gt; environment), passes
    /// it to <paramref name="apply"/>. When neither source is set, <paramref name="apply"/> is not called, leaving the
    /// caller's file/base value intact.
    /// </summary>
    public void Bind(ParseResult result, Action<T> apply)
    {
        if (result.GetResult(Option) is { Implicit: false } argument)
        {
            apply(argument.GetRequiredValue(Option));
            return;
        }

        if (_envVar is not null && Environment.GetEnvironmentVariable(_envVar) is { } raw)
        {
            apply(Parse(raw));
        }
    }

    private Option<T> BuildOption()
    {
        var name = _optionName ?? throw new InvalidOperationException("Option name not set; call FromOption first.");
        return new Option<T>(name)
        {
            Description = _description,
            AllowMultipleArgumentsPerToken = _allowMultipleArguments,
        };
    }

    private T Parse(string raw)
    {
        if (_parser is not null)
        {
            return _parser(raw);
        }

        if (typeof(T).IsEnum)
        {
            return (T)Enum.Parse(typeof(T), raw, ignoreCase: true);
        }

        // A string binding needs no parser: the raw value is already the target type.
        if (raw is T value)
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Environment variable '{_envVar}' overrides '{Option.Name}' but no value parser was supplied.");
    }
}
