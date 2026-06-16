using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Dsl;
using NSchema.Configuration.Provider;
using NSchema.Diff.Policies;
using NSchema.Operations.Import;

namespace NSchema.Tests.Configuration.Binding;

public sealed class OptionBindingTests : IDisposable
{
    private const string EnvVar = "NSCHEMA_TEST_OPTION_BINDING";
    private static readonly DslProjectConfig Empty = new();

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
        binding.Bind(Empty, result, value => captured = value);

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
        binding.Bind(Empty, result, value => captured = value);

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
        binding.Bind(Empty, result, value => captured = value);

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
        binding.Bind(Empty, result, _ => called = true);

        // Assert
        called.ShouldBeFalse();
    }

    // ── Project config layer (lowest precedence) ────────────────────────────

    private static DslProjectConfig ProjectWithConnectionString(string value) =>
        new() { Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = value } } };

    [Fact]
    public void Bind_AppliesProjectValue_WhenSet()
    {
        var binding = OptionBinding.Create<string>().FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

        string? captured = null;
        binding.Bind(ProjectWithConnectionString("from-project"), new Command("test").Parse([]), v => captured = v);

        captured.ShouldBe("from-project");
    }

    [Fact]
    public void Bind_EnvironmentOverridesProject()
    {
        Environment.SetEnvironmentVariable(EnvVar, "from-env");
        var binding = OptionBinding.Create<string>()
            .FromEnvironmentVariable(EnvVar)
            .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

        string? captured = null;
        binding.Bind(ProjectWithConnectionString("from-project"), new Command("test").Parse([]), v => captured = v);

        captured.ShouldBe("from-env");
    }

    [Fact]
    public void Bind_CliOverridesProject()
    {
        var binding = OptionBinding.Create<string>()
            .FromOption("--opt")
            .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);
        var result = Parse(binding, "--opt", "cli");

        string? captured = null;
        binding.Bind(ProjectWithConnectionString("from-project"), result, v => captured = v);

        captured.ShouldBe("cli");
    }

    [Fact]
    public void Bind_AppliesProjectValue_ForNullableValueType()
    {
        // A nullable value-type binding reads the project value through the single selector.
        var binding = OptionBinding.Create<DestructiveActionPolicy?>()
            .FromProjectConfig(c => c.DestructiveActionPolicy);
        var project = new DslProjectConfig { DestructiveActionPolicy = DestructiveActionPolicy.Warn };

        DestructiveActionPolicy? captured = null;
        binding.Bind(project, new Command("test").Parse([]), v => captured = v);

        captured.ShouldBe(DestructiveActionPolicy.Warn);
    }

    [Fact]
    public void Bind_DoesNotApplyProjectValue_WhenNullableValueTypeAbsent()
    {
        // Empty project: the nullable selector yields null, so the action is never invoked.
        var binding = OptionBinding.Create<DestructiveActionPolicy?>()
            .FromProjectConfig(c => c.DestructiveActionPolicy);

        var called = false;
        binding.Bind(Empty, new Command("test").Parse([]), _ => called = true);

        called.ShouldBeFalse();
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_WhenNeitherSet()
    {
        // Arrange
        var binding = OptionBinding.Create<string>().FromOption("--opt").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        var found = binding.TryGetValue(Empty, result, out var value);

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
        var value = binding.GetValueOrDefault(Empty, result, "fallback");

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
        var value = binding.GetValueOrDefault(Empty, result, "fallback");

        // Assert
        value.ShouldBe("cli");
    }

    [Fact]
    public void Bind_ParsesEnumFromEnvironment_CaseInsensitively()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvVar, "schema");
        var binding = OptionBinding.Create<ImportPartitionMode>().FromOption("--partition").FromEnvironmentVariable(EnvVar);
        var result = Parse(binding);

        // Act
        ImportPartitionMode? captured = null;
        binding.Bind(Empty, result, value => captured = value);

        // Assert
        captured.ShouldBe(ImportPartitionMode.Schema);
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
        binding.Bind(Empty, result, value => captured = value);

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
        Should.Throw<InvalidOperationException>(() => binding.Bind(Empty, result, _ => { }));
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
        binding.Bind(Empty, result, value => captured = value);

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
        binding.Bind(Empty, result, _ => called = true);

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
