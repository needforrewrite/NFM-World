using WorldXaml.ObservableCollections;

namespace NFMWorld.Reactor;

public class ComponentChildCollection(Component parent) : NonSynchronizedObservableCollection<Node>
{
    /// <summary>
    /// Recursively mark a node (and its descendants) as disposed/attached. Removal marks
    /// the subtree disposed so hover tracking can drop the now-stale references; re-insert
    /// clears it because the node is live again.
    /// </summary>
    private static void SetDisposed(Node node, bool disposed)
    {
        node.IsDisposed = disposed;
        foreach (var child in node.VisualChildren)
            SetDisposed(child, disposed);
    }

    protected override void InsertItem(int index, Node item)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);

        // Move semantics (match DOM `insertBefore`): if `item` is already a
        // child, relocate it instead of inserting a duplicate. The Lua React
        // reconciler can re-place a reused host node (e.g. when it detects a
        // move), and without this the host tree grows by one node on every
        // such re-render — the "components multiply on hover/click" bug.
        var existingIndex = Items.IndexOf(item);
        if (existingIndex >= 0)
        {
            if (existingIndex == index)
            {
                return; // already in the right place
            }

            if (existingIndex < index)
            {
                index--;
            }

            RemoveItem(existingIndex);
        }

        if (item is Component cmp)
        {
            // YogaNode only holds Components; TextNodes interleaved in the Items list
            // (e.g. an Sx dynamic-slot anchor or bare text in a view) must not shift the
            // Yoga insert index. Count the Components strictly before `index` — otherwise
            // List.Insert throws ArgumentOutOfRangeException (index > Yoga child count).
            var yogaIndex = 0;
            var limit = index < Items.Count ? index : Items.Count;
            for (var i = 0; i < limit; i++)
            {
                if (Items[i] is Component) yogaIndex++;
            }

            parent.NodeInternal.InsertChild(cmp.Contents, yogaIndex);
        }

        item.VisualParent = parent;
        SetDisposed(item, false);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, Node item)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        var oldItem = Items[index];
        oldItem.VisualParent = null;
        SetDisposed(oldItem, true);
        if (item is Component cmp)
            parent.NodeInternal.ReplaceChild(cmp.Contents, index);
        else if (oldItem is Component cmp1)
            parent.NodeInternal.RemoveChild(cmp1.Contents);
        item.VisualParent = parent;
        SetDisposed(item, false);
        base.SetItem(index, item);
    }

    protected override void ClearItems()
    {
        foreach (var item in Items)
        {
            item.VisualParent = null;
            SetDisposed(item, true);
        }
        parent.NodeInternal.ClearChildren();
        base.ClearItems();
    }

    protected override void RemoveItem(int index)
    {
        var item = Items[index];
        item.VisualParent = null;
        SetDisposed(item, true);
        if (item is Component cmp)
            parent.NodeInternal.RemoveChild(cmp.Contents);
        base.RemoveItem(index);
    }
}