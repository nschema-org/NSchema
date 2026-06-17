using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Commands.Validate;

/// <summary>
/// Configuration for the validate command. Currently just a placeholder.
/// </summary>
internal sealed class ValidateConfiguration : IBindable
{
    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        // Nothing to bind.
    }
}
