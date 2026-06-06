using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Commands.Init;

/// <summary>
/// Represents the model for a boilerplate nschema.json project file.
/// </summary>
internal sealed class ScaffoldedProjectConfig
{
    /// <summary>
    /// The database/live schema provider settings.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The backend state store connection.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// The desired schema configuration.
    /// </summary>
    public SchemaConfig Schema { get; init; } = new();
}
