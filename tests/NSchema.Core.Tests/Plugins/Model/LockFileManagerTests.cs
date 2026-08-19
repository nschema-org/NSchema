using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// <see cref="LockFileManager"/> reads and writes <c>nschema.lock</c>: Core owns the format both ways, so a
/// written lockfile round-trips; a missing file is empty (not an error), and a newer lockfile's unknown
/// attributes are ignored rather than rejected.
/// </summary>
public sealed class LockFileManagerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nschema-lock-{Guid.NewGuid():N}.lock");

    private static readonly LockFile Sample = new(
    [
        new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse("5.0.0-alpha.2") },
        new LockedPlugin { Source = new PackageId("NSchema.Aws"), Version = SemanticVersion.Parse("5.0.0-alpha.2") },
    ]);

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private Task<Result<LockFile>> Read() => LockFileManager.Read(_path, TestContext.Current.CancellationToken);
    private Task WriteText(string text) => File.WriteAllTextAsync(_path, text, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Write_ThenRead_RoundTrips()
    {
        // Arrange / Act
        (await LockFileManager.Write(_path, Sample, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await Read();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldBe(Sample.Plugins);
    }

    [Fact]
    public async Task Write_ProducesOneStatementPerPin_WithAManagedHeader()
    {
        // Arrange
        await LockFileManager.Write(_path, Sample, TestContext.Current.CancellationToken);

        // Act
        var text = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        // Assert
        text.ShouldContain("-- nschema.lock");
        text.ShouldContain("LOCK (\n  source = 'NSchema.Postgres',\n  version = '5.0.0-alpha.2'\n);");
        text.ShouldContain("LOCK (\n  source = 'NSchema.Aws',\n  version = '5.0.0-alpha.2'\n);");
    }

    [Fact]
    public async Task Read_MissingFile_IsEmpty()
    {
        // Act
        var result = await Read();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_IgnoresUnknownAttributes()
    {
        // Arrange
        // A newer NSchema may add attributes (e.g. a content hash); an older reader keeps loading.
        await WriteText("LOCK ( source = 'NSchema.Postgres', version = '5.0.0', hash = 'sha512-abc' );");

        // Act
        var result = await Read();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().ShouldBe(new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse("5.0.0") });
    }

    [Fact]
    public async Task Read_MissingSource_IsAnError()
    {
        // Act
        await WriteText("LOCK ( version = '5.0.0' );");

        // Assert
        (await Read()).Errors.ShouldContain(e => e.Message.Contains("Source"));
    }

    [Fact]
    public async Task Read_MissingVersion_IsAnError()
    {
        // Act
        await WriteText("LOCK ( source = 'NSchema.Postgres' );");

        // Assert
        (await Read()).Errors.ShouldContain(e => e.Message.Contains("Version"));
    }

    [Fact]
    public async Task Read_InvalidPackageId_IsAnError()
    {
        // Act
        await WriteText("LOCK ( source = 'not a package', version = '5.0.0' );");

        // Assert
        (await Read()).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Read_InvalidVersion_IsAnError()
    {
        // Act
        await WriteText("LOCK ( source = 'NSchema.Postgres', version = 'banana' );");

        // Assert
        (await Read()).IsFailure.ShouldBeTrue();
    }
}
