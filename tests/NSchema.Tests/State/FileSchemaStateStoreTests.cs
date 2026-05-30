using Microsoft.Extensions.Options;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class FileSchemaStateStoreTests : IDisposable
{
    private static readonly ISchemaStateSerializer _serializer = new DefaultSchemaStateSerializer();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nschema-state-{Guid.NewGuid():N}");
    private readonly string _path;
    private readonly IOptions<FileSchemaStateStoreOptions> _options;

    public FileSchemaStateStoreTests()
    {
        _path = Path.Combine(_directory, "nested", "state.json");
        _options = Options.Create(new FileSchemaStateStoreOptions { Path = _path });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static DatabaseSchema SampleSchema() => DatabaseSchema.Create(
        [SchemaDefinition.Create("app", tables: [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])])]);

    [Fact]
    public async Task Read_MissingFile_ReturnsNull()
    {
        // Arrange
        var sut = new FileSchemaStateStore(_options, _serializer);

        // Act
        var result = await sut.Read();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Write_CreatesFileAndMissingDirectories()
    {
        // Arrange
        var sut = new FileSchemaStateStore(_options, _serializer);

        // Act
        await sut.Write(SampleSchema());

        // Assert
        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsTheSchema()
    {
        // Arrange
        var sut = new FileSchemaStateStore(_options, _serializer);
        var original = SampleSchema();

        // Act
        await sut.Write(original);
        var result = await sut.Read();

        // Assert: compare via the serializer, since the domain records don't all define structural equality.
        result.ShouldNotBeNull();
        _serializer.Serialize(result).ShouldBe(_serializer.Serialize(original));
    }
}
