using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;
using NSchema.Schema.Ddl;

namespace NSchema.Configuration.Ddl;

/// <summary>
/// Reads project configuration from the <c>.sql</c> files under a directory.
/// </summary>
internal static class DdlProjectConfigReader
{
    private const string PreScriptSuffix = ".pre.sql";
    private const string PostScriptSuffix = ".post.sql";
    private static readonly EnumerationOptions _sqlEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    public static async ValueTask<DdlProjectConfig> Read(string root, CancellationToken cancellationToken = default)
    {
        var schemaFiles = SchemaFiles(root).Select(f => File.ReadAllTextAsync(f, cancellationToken));
        var schemaContents = await Task.WhenAll(schemaFiles);
        var blocks = schemaContents.Select(DdlReader.Instance.Read).SelectMany(d => d.Config).ToList();
        var config = Parse(blocks);
        return config;
    }

    private static IEnumerable<string> SchemaFiles(string root) => Directory
        .EnumerateFiles(root, "*.sql", _sqlEnumeration)
            .Where(path =>
                !path.EndsWith(PreScriptSuffix, StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(PostScriptSuffix, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(path => path, StringComparer.Ordinal);

    private static DdlProjectConfig Parse(IReadOnlyList<ConfigBlock> blocks)
    {
        ProviderConfig? provider = null;
        StateConfig? state = null;
        DestructiveActionPolicy? destructiveAction = null;
        var nschemaSeen = false;

        foreach (var block in blocks)
        {
            switch (block.Type)
            {
                case "provider":
                    if (provider is not null)
                    {
                        throw Conflict("PROVIDER");
                    }
                    provider = ParseProvider(block);
                    break;
                case "backend":
                    if (state is not null)
                    {
                        throw Conflict("BACKEND");
                    }
                    state = ParseBackend(block);
                    break;
                case "nschema":
                    if (nschemaSeen)
                    {
                        throw Conflict("NSCHEMA");
                    }
                    nschemaSeen = true;
                    destructiveAction = ParseNschema(block);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown configuration block '{block.Type.ToUpperInvariant()}'. Expected NSCHEMA, PROVIDER, or BACKEND.");
            }
        }

        return new DdlProjectConfig { Provider = provider, State = state, DestructiveActionPolicy = destructiveAction };
    }

    private static ProviderConfig ParseProvider(ConfigBlock block)
    {
        if (!string.Equals(block.Label, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unknown or missing provider '{block.Label}' in a PROVIDER block.");
        }

        var postgres = new PostgresProviderConfig();
        foreach (var (key, value) in block.Attributes)
        {
            switch (key.ToLowerInvariant())
            {
                case "connection_string":
                    postgres.ConnectionString = value.AsString();
                    break;
                case "command_timeout":
                    postgres.CommandTimeout = (int)value.AsInteger();
                    break;
                default:
                    throw UnknownAttribute(key, "PROVIDER postgres");
            }
        }

        return new ProviderConfig { Postgres = postgres };
    }

    private static StateConfig ParseBackend(ConfigBlock block)
    {
        switch (block.Label?.ToLowerInvariant())
        {
            case "file":
                var file = new FileStateConfig();
                foreach (var (key, value) in block.Attributes)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "path":
                            file.Path = value.AsString();
                            break;
                        default:
                            throw UnknownAttribute(key, "BACKEND file");
                    }
                }
                return new StateConfig { File = file };

            case "s3":
                var s3 = new S3StateConfig();
                foreach (var (key, value) in block.Attributes)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "bucket":
                            s3.Bucket = value.AsString();
                            break;
                        case "key":
                            s3.Key = value.AsString();
                            break;
                        default:
                            throw UnknownAttribute(key, "BACKEND s3");
                    }
                }
                return new StateConfig { S3 = s3 };

            default:
                throw new InvalidOperationException(
                    $"Unknown or missing backend '{block.Label}' in a BACKEND block. Expected 'file' or 's3'.");
        }
    }

    private static DestructiveActionPolicy? ParseNschema(ConfigBlock block)
    {
        DestructiveActionPolicy? destructiveAction = null;
        foreach (var (key, value) in block.Attributes)
        {
            switch (key.ToLowerInvariant())
            {
                case "destructive_action":
                    destructiveAction = ParseDestructiveAction(value.AsString());
                    break;
                // Reserved, accepted for forward-compatibility with the core grammar but not yet wired in the CLI:
                // the dialect is determined by the provider, and transaction mode is not yet a CLI setting.
                case "dialect":
                case "transaction_mode":
                    break;
                default:
                    throw UnknownAttribute(key, "NSCHEMA");
            }
        }

        return destructiveAction;
    }

    private static DestructiveActionPolicy ParseDestructiveAction(string value) =>
        Enum.TryParse<DestructiveActionPolicy>(value, ignoreCase: true, out var policy)
            ? policy
            : throw new InvalidOperationException(
                $"Invalid destructive_action '{value}'. Expected Error, Warn, or Allow.");

    private static InvalidOperationException Conflict(string blockType) =>
        new($"More than one {blockType} block is declared; specify exactly one across the project's .sql files.");

    private static InvalidOperationException UnknownAttribute(string attribute, string blockDescription) =>
        new($"Unknown attribute '{attribute}' in a {blockDescription} block.");
}
