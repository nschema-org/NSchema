using NSchema.Configuration.Provider;

namespace NSchema.Tests.Configuration.Provider;

public sealed class ProviderConfigTests
{
    private readonly ProviderConfig _sut = new();

    [Fact]
    public void SetProvider_CreatesPostgresSection()
    {
        // Act
        _sut.SetProvider(ProviderType.Postgres);

        // Assert
        _sut.Postgres.ShouldNotBeNull();
        _sut.ConfiguredSectionCount.ShouldBe(1);
    }

    [Fact]
    public void SetProvider_PreservesExistingSection()
    {
        // Arrange
        var existing = new PostgresProviderConfig { ConnectionString = "Host=localhost" };
        _sut.Postgres = existing;

        // Act
        _sut.SetProvider(ProviderType.Postgres);

        // Assert
        _sut.Postgres.ShouldBeSameAs(existing);
    }

    [Fact]
    public void SetConnectionString_SetsValue_WhenProviderConfigured()
    {
        // Arrange
        _sut.SetProvider(ProviderType.Postgres);

        // Act
        _sut.SetConnectionString("Host=localhost");

        // Assert
        _sut.Postgres!.ConnectionString.ShouldBe("Host=localhost");
    }

    [Fact]
    public void SetConnectionString_Throws_WhenNoProviderConfigured()
    {
        // Act / Assert
        Should.Throw<InvalidOperationException>(() => _sut.SetConnectionString("Host=localhost"));
    }
}
