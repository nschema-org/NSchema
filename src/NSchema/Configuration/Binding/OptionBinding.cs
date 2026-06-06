using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace NSchema.Configuration.Binding;

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
    private bool _recursive;

    private string? _envVar;
    private Func<string, T>? _parser;

    /// <summary>
    /// The underlying System.CommandLine option, built on first access and exposed so commands can register it.
    /// </summary>
    public Option<T> Option => field ??= BuildOption();

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
    public OptionBinding<T> FromEnvironmentVariable(string envVar, Func<string, T>? parser = null)
    {
        _envVar = envVar;
        _parser = parser;
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
    /// Causes the option to be registered on all subcommands, not just the root command.
    /// </summary>
    public OptionBinding<T> Recursive()
    {
        _recursive = true;
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

    public T GetValueOrDefault(ParseResult result, T defaultValue)
    {
        return TryGetValue(result, out var value) ? value : defaultValue;
    }

    public bool TryGetValue(ParseResult result, [NotNullWhen(true)] out T? value)
    {
        if (result.GetResult(Option) is { Implicit: false } argument)
        {
            value = argument.GetRequiredValue(Option);
            return true;
        }

        if (_envVar is not null && Environment.GetEnvironmentVariable(_envVar) is { } raw)
        {
            value = Parse(raw);
            return true;
        }

        value = default;
        return false;
    }

    private Option<T> BuildOption()
    {
        var name = _optionName ?? throw new InvalidOperationException("Option name not set; call FromOption first.");
        return new Option<T>(name)
        {
            Description = _description,
            AllowMultipleArgumentsPerToken = _allowMultipleArguments,
            Recursive = _recursive,
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

        throw new InvalidOperationException($"Environment variable '{_envVar}' overrides '{Option.Name}' but no value parser was supplied.");
    }
}
