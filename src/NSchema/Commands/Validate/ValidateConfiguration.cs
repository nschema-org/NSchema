using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;

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
