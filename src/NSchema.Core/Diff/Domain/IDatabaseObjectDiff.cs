using NSchema.Model;

namespace NSchema.Diff.Domain;

/// <summary>
/// A change to something the database owns directly.
/// </summary>
public interface IDatabaseObjectDiff : IDatabaseElementDiff
{
    /// <summary>
    /// The address of the thing the diff describes.
    /// </summary>
    DatabaseAddress Address { get; }
}
