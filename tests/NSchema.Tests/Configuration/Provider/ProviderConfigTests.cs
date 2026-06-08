using NSchema.Configuration.Provider;

namespace NSchema.Tests.Configuration.Provider;

public sealed class ProviderConfigTests
{
    private readonly ProviderConfig _sut = new();

    [Fact]
    public void EnsurePostgres_CreatesSectionOnFirstUse()
    {
        // Act
        var section = _sut.EnsurePostgres();

        // Assert
        section.ShouldNotBeNull();
        _sut.Postgres.ShouldBeSameAs(section);
        _sut.ConfiguredSectionCount.ShouldBe(1);
    }

    [Fact]
    public void EnsurePostgres_PreservesExistingSection()
    {
        // Arrange
        var existing = new PostgresProviderConfig { ConnectionString = "Host=localhost" };
        _sut.Postgres = existing;

        // Act
        var section = _sut.EnsurePostgres();

        // Assert
        section.ShouldBeSameAs(existing);
        section.ConnectionString.ShouldBe("Host=localhost");
    }
}
