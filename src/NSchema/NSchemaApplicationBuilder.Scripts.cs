using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;
using NSchema.Migration.ScriptProviders;
using NSchema.Schema;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Adds a provider to the application that will be used to retrieve deployment scripts to run during migration.
    /// </summary>
    /// <param name="provider">The provider to add.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScripts(IScriptProvider provider)
    {
        Services.AddSingleton(provider);
        return this;
    }

    /// <summary>
    /// Adds a provider to the application that will be used to retrieve deployment scripts to run during migration.
    /// </summary>
    /// <typeparam name="TProvider">The type of the provider to add.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScripts<TProvider>() where TProvider : class, IScriptProvider
    {
        Services.AddSingleton<IScriptProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Adds a SQL script to the application from a file that will be run after all other migration actions.
    /// </summary>
    /// <param name="type">The type of the script, indicating when it should be executed in relation to the main migration actions.</param>
    /// <param name="path">The path to the SQL script file.</param>
    /// <param name="name">An optional name for the script, used for logging and in migration plans. If not provided, the file name will be used.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScriptFromFile(ScriptType type, string path, string? name = null)
        => AddScripts(new FileScriptProvider(type, path, name));

    /// <summary>
    /// Adds SQL scripts to the application from files in a directory that will be run after all other migration actions. The scripts will be run in alphabetical order.
    /// </summary>
    /// <param name="type">The type of the script, indicating when it should be executed in relation to the main migration actions.</param>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    /// <param name="resourceName">The name of the embedded resource containing the SQL script.</param>
    /// <param name="name">An optional name for the script, used for logging and in migration plans. If not provided, the resource name will be used.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScriptFromEmbeddedResource(ScriptType type, Assembly assembly, string resourceName, string? name = null)
        => AddScripts(new EmbeddedResourceScriptProvider(type, assembly, resourceName, name));

    /// <summary>
    /// Adds SQL scripts to the application from embedded resources in an assembly that will be run after all other migration actions. The scripts will be run in alphabetical order.
    /// </summary>
    /// <param name="type">The type of the script, indicating when it should be executed in relation to the main migration actions.</param>
    /// <param name="assembly">The assembly containing the embedded resources.</param>
    /// <param name="resourcePrefix">The prefix of the embedded resources to include as scripts. For example, if the assembly contains embedded resources "Scripts.Post.Script1.sql" and "Scripts.Post.Script2.sql", a prefix of "Scripts.Post." would include both of these as scripts.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder AddScriptsFromEmbeddedResources(ScriptType type, Assembly assembly, string resourcePrefix)
        => AddScripts(new EmbeddedResourcePrefixScriptProvider(type, assembly, resourcePrefix));
}
