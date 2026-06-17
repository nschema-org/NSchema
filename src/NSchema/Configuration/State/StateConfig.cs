using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig : IBindable
{
    private static readonly OptionBinding<StateConfig> StateBinding = OptionBinding.Create<StateConfig>()
        .FromProjectConfig(c => c.State);

    /// <summary>
    /// Local-file state store settings.
    /// </summary>
    public FileStateConfig? File { get; set; }

    /// <summary>
    /// Amazon S3 state store settings.
    /// </summary>
    public S3StateConfig? S3 { get; set; }

    /// <summary>
    /// The number of state store sections populated. Zero means online-only (no state store).
    /// </summary>
    public int ConfiguredSectionCount => (File is not null ? 1 : 0) + (S3 is not null ? 1 : 0);

    /// <summary>
    /// Resolves the state store from the project config (it has no environment or command-line override today).
    /// </summary>
    public void Bind(DdlProjectConfig project, ParseResult cli) => StateBinding.Bind(project, cli, CopyFrom);

    /// <summary>
    /// Copies the populated sections from <paramref name="other"/> onto this instance.
    /// </summary>
    private void CopyFrom(StateConfig other)
    {
        File = other.File;
        S3 = other.S3;
    }
}
