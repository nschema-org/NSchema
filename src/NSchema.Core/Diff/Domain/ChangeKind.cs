namespace NSchema.Diff.Domain;

/// <summary>
/// The kind of change a diff element represents.
/// </summary>
public enum ChangeKind
{
    /// <summary>
    /// The element is being created.
    /// </summary>
    Add,

    /// <summary>
    /// The element is being modified in place.
    /// </summary>
    Modify,

    /// <summary>
    /// The element is being removed.
    /// </summary>
    Remove
}
