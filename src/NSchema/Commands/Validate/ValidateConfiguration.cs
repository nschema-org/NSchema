using System.CommandLine;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Validate;

/// <summary>
/// Configuration for the validate command. The desired schema is always the <c>*.sql</c> files under the working
/// directory, so there is nothing to configure — the type exists only so the loader can resolve <c>--directory</c>.
/// </summary>
internal sealed class ValidateConfiguration : IBindable
{
    public void Bind(ParseResult result)
    {
    }
}
