using NSchema.Configuration.Plugins;

namespace NSchema.Plugins;

/// <summary>
/// A plugin that supplies a database provider for one engine, configured by a <c>DATABASE</c> statement.
/// </summary>
public interface INSchemaDatabasePlugin : INSchemaPlugin
{
    /// <summary>
    /// Registers this provider onto the application being built, interpreting the project's <c>DATABASE</c> statement.
    /// </summary>
    /// <param name="builder">The application builder to register the provider's services on.</param>
    /// <param name="settings">The statement's bound settings.</param>
    Result Configure(NSchemaApplicationBuilder builder, PluginSettings settings);

    /// <summary>
    /// Builds the starter project DDL this provider contributes when a new project is scaffolded.
    /// </summary>
    string GetSampleSchema();
}
