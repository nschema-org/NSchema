using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.Db.Show;

/// <summary>
/// Configuration for <c>db show</c>: reads the live schema directly from the database via the provider.
/// </summary>
internal sealed class DbShowConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema.
    /// </summary>
    public PluginReference? Provider { get; set; }

    /// <summary>
    /// Optional scope filter limiting the output to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        Provider = project.Provider;
        DbShowOptions.Scope.Bind(cli, s => Scope = s);
    }
}
