using Microsoft.Extensions.Options;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// An <see cref="ISchemaStateStore"/> that persists the schema snapshot to a JSON file on the local filesystem.
/// </summary>
/// <param name="options">The absolute or relative path of the state file.</param>
/// <param name="serializer">The serializer used to convert schema snapshots to and from their stored representation.</param>
/// <remarks>
/// Useful for local development and for backends that surface state as a mounted file.
/// </remarks>
internal sealed class FileSchemaStateStore(IOptions<FileSchemaStateStoreOptions> options, ISchemaStateSerializer serializer) : ISchemaStateStore
{
    /// <inheritdoc />
    public async Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.Value.Path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(options.Value.Path, cancellationToken);
        return serializer.Deserialize(json);
    }

    /// <inheritdoc />
    public async Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.Value.Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = serializer.Serialize(schema);
        await File.WriteAllTextAsync(options.Value.Path, json, cancellationToken);
    }
}
