using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Validate;

/// <summary>
/// Configuration for the validate command.
/// </summary>
internal sealed class ValidateConfiguration : IBindable
{
    public void Bind(ProjectConfig project, ParseResult cli)
    {
    }
}
