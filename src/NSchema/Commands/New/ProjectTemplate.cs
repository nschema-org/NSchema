using NSchema.Project.Nsql;

namespace NSchema.Commands.New;

/// <summary>
/// What a new project is scaffolded from.
/// </summary>
internal sealed record ProjectTemplate
{
    /// <summary>
    /// The version range the scaffolded <c>ENGINE</c> statement asserts (e.g. <c>[5.0,6.0)</c>).
    /// </summary>
    public required string EngineRequirement { get; init; }

    /// <summary>
    /// The plugins to declare, in the order they should be written.
    /// </summary>
    public required IReadOnlyList<ResolvedPlugin> Plugins { get; init; }

    /// <summary>
    /// The database plugin's configuration.
    /// </summary>
    public required NsqlDocument Database { get; init; }

    /// <summary>
    /// The database plugin's configuration for the scaffolded environment overlay. Empty when it has nothing that
    /// differs per environment, which is the usual case: a connection string comes from the environment instead.
    /// </summary>
    public required NsqlDocument DatabaseOverlay { get; init; }

    /// <summary>
    /// The state store's configuration.
    /// </summary>
    public required NsqlDocument State { get; init; }

    /// <summary>
    /// The state store's configuration for the scaffolded environment overlay.
    /// </summary>
    public required NsqlDocument StateOverlay { get; init; }

    /// <summary>
    /// The provider's dialect-specific sample schema.
    /// </summary>
    public required NsqlDocument Schema { get; init; }
}
