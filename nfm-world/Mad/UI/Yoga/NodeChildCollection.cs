using System.Collections;

namespace NFMWorld.UI.Yoga;

public class NodeChildCollection(Node parent) : IList<Node>
{
    private List<Node> _internalList = new();

    public IEnumerator<Node> GetEnumerator()
    {
        return _internalList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(Node item)
    {
        parent.NodeInternal.InsertChild(item.NodeInternal, parent.NodeInternal.GetChildCount());
        _internalList.Add(item);
    }

    public void Clear()
    {
        parent.NodeInternal.RemoveAllChildren();
        _internalList.Clear();
    }

    public bool Contains(Node item)
    {
        return _internalList.Contains(item);
    }

    public void CopyTo(Node[] array, int arrayIndex)
    {
        _internalList.CopyTo(array, arrayIndex);
    }

    public bool Remove(Node item)
    {
        if (_internalList.Remove(item))
        {
            parent.NodeInternal.RemoveChild(item.NodeInternal);
            return true;
        }

        return false;
    }

    public int Count => _internalList.Count;
    public bool IsReadOnly => false;
    public int IndexOf(Node item)
    {
        return _internalList.IndexOf(item);
    }

    public void Insert(int index, Node item)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        parent.NodeInternal.InsertChild(item.NodeInternal, (uint)index);
        _internalList.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
        var item = _internalList[index];
        parent.NodeInternal.RemoveChild(item.NodeInternal);
        _internalList.RemoveAt(index);
    }

    public Node this[int index]
    {
        get => _internalList[index];
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);
            _internalList[index] = value;
            parent.NodeInternal.SwapChild(value.NodeInternal, (uint)index);
        }
    }
}