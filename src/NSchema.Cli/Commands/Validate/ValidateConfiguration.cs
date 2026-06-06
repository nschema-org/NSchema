using System.CommandLine;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Schema;

namespace NSchema.Cli.Commands.Validate;

/// <summary>
/// Configuration for the validate command. It checks only the desired schema, so it composes nothing but the schema
/// slice — no provider, state, or scope.
/// </summary>
internal sealed class ValidateConfiguration : IBindable
{
    /// <summary>
    /// How the desired schema is located and read.
    /// </summary>
    public SchemaConfig Schema { get; init; } = new();

    public void Bind(ParseResult result)
    {
        Schema.Bind(result);
    }
}
