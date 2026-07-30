using NSchema.Model;

namespace NSchema.Diff.Domain;

/// <summary>
/// What every diff node shares, at whatever level it sits.
/// </summary>
public interface IDatabaseElementDiff
{
    /// <summary>
    /// The name of the thing the diff describes.
    /// </summary>
    SqlIdentifier Name { get; }

    /// <summary>
    /// How the thing changed.
    /// </summary>
    ChangeKind Change { get; }

    /// <summary>
    /// The change to the thing's comment, if any.
    /// </summary>
    ValueChange<string>? Comment { get; }
}
