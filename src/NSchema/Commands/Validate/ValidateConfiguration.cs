using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Commands.Validate;

/// <summary>
/// Configuration for the validate command.
/// </summary>
internal sealed class ValidateConfiguration : IBindable
{
    /// <summary>
    /// The environment to validate against, if any.
    /// </summary>
    public string? Environment { get; set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        CommonOptions.Environment.Bind(project, cli, e => Environment = e);
    }
}
