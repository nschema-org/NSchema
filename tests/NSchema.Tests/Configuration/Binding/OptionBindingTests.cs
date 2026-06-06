using System.CommandLine;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Schema;

namespace NSchema.Cli.Tests.Configuration.Binding;

public sealed class OptionBindingTests : IDisposable
{
    private const string EnvVar = "NSCHEMA_TEST_OPTION_BINDING";

    public OptionBindingTests() => Environment.SetEnvironmentVariable(EnvVar, null);

    public void Dispose() => Environment.SetEnvironmentVariable(EnvVar, null);

    private static ParseResult Parse<T>(OptionBinding<T> binding, params string[] args) where T : notnull
    {
        // Arrange a throwaway command so the option participates in a real parse.
        var command = new Command("test");
        command.Options.Add(binding.Option);
        return command.Parse(args);
    }

    [Fact]
    public void Bind_AppliesCliValue()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt");
        var result = Parse(binding, "--opt", "cli");

        // Act
        string? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe("cli");
    }

    [Fact]
    public void Bind_AppliesEnvironmentValue_WhenCliAbsent()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "from-env");
        var binding = OptionBinding.Create<string>().FromOption("--opt").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        string? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe("from-env");
    }

    [Fact]
    public void Bind_PrefersCliOverEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "from-env");
        var binding = OptionBinding.Create<string>().FromOption("--opt").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding, "--opt", "cli");

        // Act
        string? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe("cli");
    }

    [Fact]
    public void Bind_DoesNotInvokeAction_WhenNeitherSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        var called = false;
        binding.Bind(result, _ => called = true);

        // Assert
        called.ShouldBeFalse();
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_WhenNeitherSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        var found = binding.TryGetValue(result, out var value);

        // Assert
        found.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefault_WhenNeitherSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        var value = binding.GetValueOrDefault(result, "fallback");

        // Assert
        value.ShouldBe("fallback");
    }

    [Fact]
    public void GetValueOrDefault_ReturnsValue_WhenSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt");
        var result = Parse(binding, "--opt", "cli");

        // Act
        var value = binding.GetValueOrDefault(result, "fallback");

        // Assert
        value.ShouldBe("cli");
    }

    [Fact]
    public void Bind_ParsesEnumFromEnvironment_CaseInsensitively()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "json");
        var binding = OptionBinding.Create<SchemaFormat>().FromOption("--format").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        SchemaFormat? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe(SchemaFormat.Json);
    }

    [Fact]
    public void Bind_UsesCustomParser_ForNonStringEnvironmentValues()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "42");
        var binding = OptionBinding.Create<int>().FromOption("--count").FromEnvironmentVariable(EnvVar, int.Parse);
        var result = Parse(binding);

        // Act
        int? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe(42);
    }

    [Fact]
    public void Bind_Throws_WhenEnvironmentValueHasNoParserAndIsNotStringOrEnum()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "42");
        var binding = OptionBinding.Create<int>().FromOption("--count").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => binding.Bind(result, _ => { }));
    }

    [Fact]
    public void Option_ReflectsBuilderSettings()
    {
        // Arrange
        var binding = OptionBinding.Create<string[]>()
            .FromOption("--scope")
            .AllowMultipleArguments()
            .Recursive()
            .WithDescription("Limit to namespaces.");

        // Act
        var option = binding.Option;

        // Assert
        option.Name.ShouldBe("--scope");
        option.Description.ShouldBe("Limit to namespaces.");
        option.AllowMultipleArgumentsPerToken.ShouldBeTrue();
        option.Recursive.ShouldBeTrue();
    }

    [Fact]
    public void Option_IsBuiltOnceAndCached()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt");

        // Act / Assert
        binding.Option.ShouldBeSameAs(binding.Option);
    }

    [Fact]
    public void Option_Throws_WhenNameNotSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>();

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => binding.Option);
    }
}
