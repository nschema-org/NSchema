namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig
{
    /// <summary>
    /// The provider type (e.g. <c>postgres</c>). When null, only offline operations are available.
    /// </summary>
    public ProviderType? Type { get; set; }

    /// <summary>
    /// The connection string the selected provider connects with.
    /// </summary>
    public string? ConnectionString { get; set; }
}
