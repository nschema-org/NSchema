using NSchema.Diff.Domain.Schemas;
using NSchema.Model;

namespace NSchema.Diff.Domain;

/// <summary>
/// The members every schema-level object diff shares. Implemented by the per-kind diff records so kind-agnostic
/// consumers (change summaries, destructive-change detection) can walk one heterogeneous sequence — see
/// <see cref="SchemaDiff.EnumerateObjects"/> — instead of repeating a loop per kind.
/// </summary>
public interface ISchemaObjectDiff : INamedObjectDiff
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
