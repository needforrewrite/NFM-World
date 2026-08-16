using MemoryPack;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util;

[LuaShimType("{ [integer]: T }")]
[MemoryPackable(GenerateType.Collection)]
public partial class LuaList<T> : LuaArray<T>
{
    public LuaList() : base(new List<T>())
    {
    }
    public LuaList(IList<T> innerList) : base(innerList)
    {
    }

    /// <inheritdoc cref="ICollection{T}.Add"/>
    public void Add(T item) => Value.Add(item);
    /// <inheritdoc cref="ICollection{T}.Clear"/>
    public void Clear() => Value.Clear();
    /// <inheritdoc cref="ICollection{T}.Contains"/>
    public bool Contains(T item) => Value.Contains(item);
    /// <inheritdoc cref="ICollection{T}.CopyTo"/>
    public void CopyTo(T[] array, int arrayIndex) => Value.CopyTo(array, arrayIndex);
    /// <inheritdoc cref="ICollection{T}.Remove"/>
    public bool Remove(T item) => Value.Remove(item);
    /// <inheritdoc cref="IList{T}.Insert"/>
    public void Insert(int index, T item) => Value.Insert(index, item);
    /// <inheritdoc cref="IList{T}.RemoveAt"/>
    public void RemoveAt(int index) => Value.RemoveAt(index);
}