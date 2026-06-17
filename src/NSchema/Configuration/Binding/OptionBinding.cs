using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using NSchema.Configuration.Dsl;

namespace NSchema.Configuration.Binding;

/// <summary>
/// Entry point for building <see cref="OptionBinding{T}"/> instances fluently.
/// </summary>
internal static class OptionBinding
{
    /// <summary>
    /// Starts building a binding whose resolved value is of type <typeparamref name="T"/>.
    /// </summary>
    public static OptionBinding<T> Create<T>() => new();
}

/// <summary>
/// Represents a binding to a single configuration value from any combination of project config, environment variable or CLI option.
/// </summary>
internal sealed class OptionBinding<T>
{
    private string? _optionName;
    private string? _description;
    private bool _allowMultipleArguments;
    private bool _recursive;

    private string? _envVar;
    private Func<string, T>? _envParser;
    private Func<DslProjectConfig, T?>? _projectSelector;

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
        _envParser = parser;
        return this;
    }

    /// <summary>
    /// Adds a binding from a project config field.
    /// </summary>
    public OptionBinding<T> FromProjectConfig(Func<DslProjectConfig, T?> selector)
    {
        _projectSelector = selector;
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
    /// Resolves this binding against the project config, environment, and parsed command line.
    /// </summary>
    public void Bind(DslProjectConfig project, ParseResult cli, Action<T> apply)
    {
        if (TryGetValue(project, cli, out var value))
        {
            apply(value);
        }
    }

    public T GetValueOrDefault(DslProjectConfig? project, ParseResult cli, T defaultValue) =>
        TryGetValue(project, cli, out var value) ? value : defaultValue;

    public bool TryGetValue(DslProjectConfig? project, ParseResult cli, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (project != null && _projectSelector is not null)
        {
            var projectValue = _projectSelector(project);
            if (projectValue is not null)
            {
                value = projectValue;
            }
        }

        if (_envVar is not null && Environment.GetEnvironmentVariable(_envVar) is { } raw)
        {
            var envValue = Parse(raw);
            if (envValue is not null)
            {
                value = envValue;
            }
        }

        if (_optionName is not null && cli.GetResult(Option) is { Implicit: false } argument)
        {
            var cliValue = argument.GetRequiredValue(Option);
            if (cliValue is not null)
            {
                value = cliValue;
            }
        }

        return value is not null;
    }

    private Option<T> BuildOption()
    {
        var name = _optionName ?? throw new InvalidOperationException("Option name not set; call FromOption first.");
        var option = new Option<T>(name) { Description = _description, AllowMultipleArgumentsPerToken = _allowMultipleArguments, Recursive = _recursive, };

        // Enum options parse case-insensitively already; override the completions so help renders the
        // accepted values (the <a|b|c> list) in lower case rather than the PascalCase member names.
        var underlying = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (underlying.IsEnum)
        {
            option.CompletionSources.Clear();
            foreach (var memberName in Enum.GetNames(underlying))
            {
                option.CompletionSources.Add(memberName.ToLowerInvariant());
            }
        }

        return option;
    }

    private T Parse(string raw)
    {
        if (_envParser is not null)
        {
            return _envParser(raw);
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

        throw new InvalidOperationException($"Environment variable '{_envVar}' has no value parser for type '{typeof(T).Name}'.");
    }
}
