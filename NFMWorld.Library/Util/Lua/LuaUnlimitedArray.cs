using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util;

[LuaShimType("{ [integer]: T }")]
public class LuaUnlimitedArray<T>() : LuaArray<T>(new UnlimitedArray<T>())
{
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
}