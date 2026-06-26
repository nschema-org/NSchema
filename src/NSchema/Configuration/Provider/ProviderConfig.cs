using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Plugins;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig : IBindable
{
    /// <summary>
    /// The first-party provider plugins, keyed by their <c>PROVIDER</c> block label.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> BuiltInPackages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["postgres"] = "NSchema.Postgres",
            ["sqlite"] = "NSchema.Sqlite",
            ["sqlserver"] = "NSchema.SqlServer",
        };

    /// <summary>
    /// The resolved provider plugin reference; <see langword="null"/> when offline (no <c>PROVIDER</c> block).
    /// </summary>
    public PluginReference? Plugin { get; set; }

    /// <summary>
    /// The number of providers configured. Zero means offline (no live schema source).
    /// </summary>
    public int ConfiguredSectionCount => Plugin is not null ? 1 : 0;

    /// <summary>
    /// Maps a <c>PROVIDER</c> block onto a resolved plugin reference.
    /// </summary>
    public static ProviderConfig FromBlock(ConfigBlock block) =>
        new() { Plugin = PluginReference.FromBlock(block, BuiltInPackages) };

    /// <summary>
    /// Adopts the provider resolved from the project config. The provider has no environment or command-line
    /// override — it is declared in the <c>PROVIDER</c> block (the connection string and other secrets are read by
    /// the plugin itself).
    /// </summary>
    public void Bind(DdlProjectConfig project, ParseResult cli) => Plugin = project.Provider?.Plugin;
}
