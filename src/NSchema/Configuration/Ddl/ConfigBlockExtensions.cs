namespace NSchema.Configuration.Ddl;

/// <summary>
/// Helpers shared by the section models' <c>FromBlock</c> factories when mapping a <see cref="ConfigBlock"/> onto a
/// typed config slice.
/// </summary>
internal static class ConfigBlockExtensions
{
    extension(ConfigBlock block)
    {
        /// <summary>
        /// Builds the exception thrown when a block carries an attribute the section doesn't recognise.
        /// </summary>
        public InvalidOperationException UnknownAttribute(string attribute) =>
            new($"Unknown attribute '{attribute}' in a {Describe(block)} block.");
    }

    private static string Describe(ConfigBlock block) =>
        block.Label is { Length: > 0 } label
            ? $"{block.Type.ToUpperInvariant()} {label}"
            : block.Type.ToUpperInvariant();
}
