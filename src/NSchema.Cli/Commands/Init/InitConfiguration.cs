using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Schema;

namespace NSchema.Cli.Commands.Init;

/// <summary>
/// Configuration for the init command.
/// </summary>
internal sealed class InitConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema to import.
    /// </summary>
    [JsonIgnore]
    public SchemaFormat Format { get; set; } = SchemaFormat.Yaml;

    /// <summary>
    /// Where and how to write the imported schema files.
    /// </summary>
    [JsonIgnore]
    public bool Force { get; set; }

    public void Bind(ParseResult result)
    {
        InitOptions.Format.Bind(result, f => Format = f);
        InitOptions.Force.Bind(result, f => Force = f);
    }
}
