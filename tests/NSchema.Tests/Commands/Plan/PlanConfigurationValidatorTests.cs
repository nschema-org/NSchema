using NSchema.Commands.Plan;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Plan;

public sealed class PlanConfigurationValidatorTests
{
    private readonly PlanConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderOnly()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithStateOnly_ForOfflinePlanning()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenNeitherProviderNorStateConfigured()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig(),
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("current schema source"));
    }

    [Fact]
    public void Invalid_WhenDesiredSchemaMissing_ForForwardPlan()
    {
        // Arrange — a forward plan needs a desired schema to diff against.
        var config = new PlanConfiguration
        {
            Schema = new SchemaConfig(),
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("schema directory"));
    }

    [Fact]
    public void Valid_ForDestroy_WithProviderAndStateOnly()
    {
        // Arrange — --destroy tears down the recorded state, so no desired schema is required.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Schema = new SchemaConfig(),
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForDestroy_WithProviderAndDesiredSchemaFallback()
    {
        // Arrange — with no state store, --destroy falls back to the desired schema as the teardown source.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Schema = new SchemaConfig { Directory = "./schemas" },
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_ForDestroy_WhenProviderMissing()
    {
        // Arrange — the teardown SQL is rendered against the provider, so one is required.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Schema = new SchemaConfig(),
            Provider = new ProviderConfig(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_ForDestroy_WhenNoManagedSchemaSource()
    {
        // Arrange — neither a state store nor a desired schema to tear down.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Schema = new SchemaConfig(),
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig { ConnectionString = "Host=localhost" } },
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("managed schema source"));
    }
}
