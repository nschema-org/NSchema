namespace NSchema.Model;

/// <summary>
/// The members owned by a <see cref="SchemaObject"/>.
/// </summary>
public sealed class ObjectMemberCollection<T>()
    : ParentedCollection<SchemaObject, T>((parent, child) => child.Parent = parent, child => child.Parent = null)
    where T : ObjectMember
{
    internal ObjectMemberCollection(SchemaObject owner) : this() => Attach(owner);
}
