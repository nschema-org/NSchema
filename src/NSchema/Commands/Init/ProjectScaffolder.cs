namespace NSchema.Commands.Init;

/// <summary>
/// Scaffolds a starter NSchema project.
/// </summary>
internal sealed class ProjectScaffolder
{
    private const string ConfigFileName = "config.sql";
    private const string EnvironmentOverlayFileName = "config.env.prod.sql";
    private const string SchemaDirectoryName = "schemas";

    /// <summary>
    /// Writes the starter files into <paramref name="directory"/>, returning the created paths (relative to it).
    /// </summary>
    /// <param name="directory">The directory to scaffold into.</param>
    /// <param name="force">Force the initialization even if the directory is not empty.</param>
    /// <param name="provider">The database provider to scaffold configuration and a sample schema for.</param>
    /// <param name="backend">The state backend to scaffold configuration for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">The directory is not empty, and <paramref name="force"/> is false.</exception>
    public static async Task<IReadOnlyList<string>> Scaffold(
        string directory,
        bool force,
        ProviderKind provider = ProviderKind.Postgres,
        BackendKind backend = BackendKind.File,
        CancellationToken cancellationToken = default)
    {
        if (Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length != 0 && !force)
        {
            throw new InvalidOperationException($"{directory} is not empty. Use --force to override.");
        }

        var configPath = Path.Combine(directory, ConfigFileName);
        await File.WriteAllTextAsync(configPath, ReadTemplate($"config.{Slug(provider)}.{Slug(backend)}.sql"), cancellationToken);

        var overlayPath = Path.Combine(directory, EnvironmentOverlayFileName);
        await File.WriteAllTextAsync(overlayPath, ReadTemplate($"overlay.{Slug(backend)}.sql"), cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, "example.sql");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
        await File.WriteAllTextAsync(samplePath, ReadTemplate($"sample.{Slug(provider)}.sql"), cancellationToken);

        return [ConfigFileName, EnvironmentOverlayFileName, sampleRelativePath];
    }

    private static string Slug(ProviderKind provider) => provider switch
    {
        ProviderKind.Postgres => "postgres",
        ProviderKind.Sqlite => "sqlite",
        ProviderKind.SqlServer => "sqlserver",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
    };

    private static string Slug(BackendKind backend) => backend switch
    {
        BackendKind.File => "file",
        BackendKind.S3 => "s3",
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown backend."),
    };

    /// <summary>
    /// Reads an embedded scaffold template by file name. The resource is matched by its <c>.Scaffold.</c> suffix so the
    /// lookup is independent of the assembly's root namespace.
    /// </summary>
    private static string ReadTemplate(string fileName)
    {
        var assembly = typeof(ProjectScaffolder).Assembly;
        var resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith($".Scaffold.{fileName}", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded scaffold template '{fileName}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded scaffold template '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
