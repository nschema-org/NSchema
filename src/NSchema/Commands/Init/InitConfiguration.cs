using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Init;

/// <summary>
/// Configuration for the init command.
/// </summary>
internal sealed class InitConfiguration : IBindable
{
    /// <summary>
    /// Whether to overwrite an existing nschema.json.
    /// </summary>
    [JsonIgnore]
    public bool Force { get; set; }

    public void Bind(ParseResult result)
    {
        InitOptions.Force.Bind(result, f => Force = f);
    }
}
