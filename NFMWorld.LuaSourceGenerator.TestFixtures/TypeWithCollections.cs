using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Exposes a <see cref="Dictionary{TKey,TValue}"/> via a [MemberLuaVisible] property so the
/// dictionary type itself becomes implicitly Lua-visible (string indexer + pairs/ipairs + #).
/// </summary>
[LuaVisible(Name = nameof(TypeWithDictionary))]
public partial class TypeWithDictionary
{
    [MemberLuaVisible]
    public Dictionary<string, int> Items { get; set; } = new();

    public TypeWithDictionary()
    {
    }

    public void Set(string key, int value)
    {
        Items[key] = value;
    }

    public int Get(string key)
    {
        return Items.TryGetValue(key, out var value) ? value : 0;
    }
}

/// <summary>
/// Exposes a <see cref="List{T}"/> via a [MemberLuaVisible] property so the list type itself
/// becomes implicitly Lua-visible (int indexer + pairs/ipairs + #).
/// </summary>
[LuaVisible(Name = nameof(TypeWithList))]
public partial class TypeWithList
{
    [MemberLuaVisible]
    public List<int> Items { get; set; } = new();

    public TypeWithList()
    {
    }

    public void Add(int value)
    {
        Items.Add(value);
    }

    public int Get(int index)
    {
        return Items[index];
    }
}

/// <summary>
/// A [LuaVisible] type implementing IEnumerable&lt;int&gt; with an int indexer —
/// exercises the IEnumerable binding path directly.
/// </summary>
[LuaVisible(Name = nameof(TypeWithEnumerable))]
public partial class TypeWithEnumerable : IEnumerable<int>
{
    private readonly List<int> _items = new();

    public TypeWithEnumerable()
    {
    }

    public int Count => _items.Count;

    public int this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(int value)
    {
        _items.Add(value);
    }

    public IEnumerator<int> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
