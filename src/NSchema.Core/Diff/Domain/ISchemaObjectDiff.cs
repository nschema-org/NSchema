using NSchema.Model;

namespace NSchema.Diff.Domain;

/// <summary>
/// The members every schema-level object diff shares.
/// </summary>
public interface ISchemaObjectDiff : IDatabaseElementDiff
{
    /// <summary>
    /// The name of the schema the object belongs to.
    /// </summary>
    SqlIdentifier Schema { get; }

    /// <summary>
    /// The object's address.
    /// </summary>
    ObjectAddress Address { get; }

    /// <summary>
    /// The previous object name when the object is being renamed; otherwise <see langword="null"/>.
    /// </summary>
    SqlIdentifier? RenamedFrom { get; }
}
