using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Schema;

namespace NSchema.Commands.Validate;

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
        ValidateOptions.SchemaDirectory.Bind(result, d => Schema.Directory = d);
    }
}
