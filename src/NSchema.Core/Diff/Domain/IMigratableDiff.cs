using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain;

/// <summary>
/// A member diff that can carry a matched change-event script.
/// </summary>
public interface IMigratableDiff : INamedObjectDiff
{
    /// <summary>
    /// The change-event script matched to this change.
    /// </summary>
    ChangeScript? MigrationScript { get; }
}
