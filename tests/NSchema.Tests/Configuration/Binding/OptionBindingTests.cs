using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Tests.Configuration.Binding;

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

        // Act / Assert
        binding.GetValueOrDefault(result, "fallback").ShouldBe("fallback");
    }

    [Fact]
    public void GetValueOrDefault_ReturnsValue_WhenSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt");
        var result = Parse(binding, "--opt", "cli");

        // Act / Assert
        binding.GetValueOrDefault(result, "fallback").ShouldBe("cli");
    }

    [Fact]
    public void Bind_ParsesEnumFromEnvironment_CaseInsensitively()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "warn");
        var binding = OptionBinding.Create<PolicyEnforcement>().FromOption("--destructive-actions").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        PolicyEnforcement? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe(PolicyEnforcement.Warn);
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
    public void Bind_AppliesEnvironmentValue_ForEnvironmentOnlyBinding()
    {
        // Arrange — no FromOption: the binding is environment-only and registers no CLI option.
        Environment.SetEnvironmentVariable(EnvVar, "from-env");
        var binding = OptionBinding.Create<string>().FromEnvironmentVariable(EnvVar);
        var result = new Command("test").Parse([]);

        // Act
        string? captured = null;
        binding.Bind(result, value => captured = value);

        // Assert
        captured.ShouldBe("from-env");
    }

    [Fact]
    public void Bind_DoesNotInvokeAction_ForEnvironmentOnlyBinding_WhenUnset()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromEnvironmentVariable(EnvVar);
        var result = new Command("test").Parse([]);

        // Act
        var called = false;
        binding.Bind(result, _ => called = true);

        // Assert
        called.ShouldBeFalse();
    }

    [Fact]
    public void Option_Throws_ForEnvironmentOnlyBinding()
    {
        // Arrange — an environment-only binding has no CLI option to expose.
        var binding = OptionBinding.Create<string>().FromEnvironmentVariable(EnvVar);

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => binding.Option);
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
    public void Option_ExposesShortAliases()
    {
        // Arrange
        var binding = OptionBinding.Create<string[]>().FromOption("--scope", "-s");

        // Act
        var option = binding.Option;

        // Assert
        option.Name.ShouldBe("--scope");
        option.Aliases.ShouldContain("-s");
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
