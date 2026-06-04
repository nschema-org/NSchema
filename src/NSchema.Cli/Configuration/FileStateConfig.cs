namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures a local-file state store.
/// </summary>
internal sealed class FileStateConfig
{
    /// <summary>
    /// The path to the state file.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Validates the configuration, yielding one message per problem found.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            yield return "state.file.path is required.";
        }
    }
}
