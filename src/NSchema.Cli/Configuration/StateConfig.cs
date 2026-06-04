namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig
{
    /// <summary>
    /// Local-file state store settings.
    /// </summary>
    public FileStateConfig? File { get; set; }

    /// <summary>
    /// Amazon S3 state store settings.
    /// </summary>
    public S3StateConfig? S3 { get; set; }

    /// <summary>
    /// The store selected by the populated section, or null when none is configured.
    /// </summary>
    public StateType? SelectedType => File is not null ? StateType.File
        : S3 is not null ? StateType.S3
        : null;

    // The store sections that have been populated. Used to enforce the "exactly one" rule.
    private IEnumerable<object> ConfiguredSections => new object?[] { File, S3 }.OfType<object>();

    /// <summary>
    /// Validates the configuration, yielding one message per problem found. An unconfigured store
    /// (online-only) is valid and yields nothing.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (ConfiguredSections.Count() > 1)
        {
            yield return "More than one state store is configured; specify exactly one.";
        }

        if (File is { } file)
        {
            foreach (var error in file.Validate())
            {
                yield return error;
            }
        }

        if (S3 is { } s3)
        {
            foreach (var error in s3.Validate())
            {
                yield return error;
            }
        }
    }
}
