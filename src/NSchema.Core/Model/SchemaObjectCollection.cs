using NSchema.Model.Schemas;

namespace NSchema.Model;

/// <summary>
/// The objects owned by a <see cref="Schemas.Schema"/>.
/// </summary>
public sealed class SchemaObjectCollection<T>()
    : ParentedCollection<Schema, T>((parent, child) => child.Schema = parent, child => child.Schema = null)
    where T : SchemaObject
{
    internal SchemaObjectCollection(Schema owner) : this() => Attach(owner);
}
