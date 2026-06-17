using System.CommandLine;
using NSchema.Configuration.Dsl;

namespace NSchema.Configuration.Binding;

/// <summary>
/// Marks a configuration DTO as being bindable.
/// </summary>
internal interface IBindable
{
    /// <summary>
    /// Binds the configuration to the DTO.
    /// </summary>
    /// <param name="project">The project config to bind.</param>
    /// <param name="cli">The args to bind.</param>
    void Bind(DslProjectConfig project, ParseResult cli);
}
