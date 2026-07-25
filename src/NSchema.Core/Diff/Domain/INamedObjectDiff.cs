using NSchema.Model;

namespace NSchema.Diff.Domain;

/// <summary>
/// The members every named-object diff shares.
/// </summary>
public interface INamedObjectDiff
{
    /// <summary>
    /// The object name.
    /// </summary>
    SqlIdentifier Name { get; }

    /// <summary>
    /// The change to the object.
    /// </summary>
    ChangeKind Kind { get; }

    /// <summary>
    /// The change to the object's comment, if any.
    /// </summary>
    ValueChange<string>? Comment { get; }
}
