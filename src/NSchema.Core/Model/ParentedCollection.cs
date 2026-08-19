using System.Collections.ObjectModel;

namespace NSchema.Model;

/// <summary>
/// Base class for collections that support a parent node that adopts its children.
/// </summary>
/// <param name="adopt"></param>
/// <param name="orphan"></param>
/// <typeparam name="TParent">The parent type.</typeparam>
/// <typeparam name="TChild">The child type</typeparam>
public abstract class ParentedCollection<TParent, TChild>(Action<TParent, TChild> adopt, Action<TChild> orphan) : Collection<TChild>
{
    private TParent? _parent;

    internal void Attach(TParent owner)
    {
        if (_parent is not null && !ReferenceEquals(_parent, owner))
        {
            throw new InvalidOperationException($"The collection already has a parent and cannot be claimed by a different one.");
        }

        _parent = owner;
        foreach (var item in this)
        {
            adopt(owner, item);
        }
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, TChild item)
    {
        Adopt(item);
        base.InsertItem(index, item);
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, TChild item)
    {
        if (!ReferenceEquals(this[index], item))
        {
            Adopt(item);
            Orphan(this[index]);
        }
        base.SetItem(index, item);
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        Orphan(this[index]);
        base.RemoveItem(index);
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            Orphan(item);
        }
        base.ClearItems();
    }

    private void Adopt(TChild item)
    {
        if (_parent is not null)
        {
            adopt(_parent, item);
        }
    }

    // Only unwire parentage this collection granted:
    // before Attach, an item's membership carries no ownership.
    private void Orphan(TChild item)
    {
        if (_parent is not null)
        {
            orphan(item);
        }
    }
}
