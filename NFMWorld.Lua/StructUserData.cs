using Lua;
using Lua.Runtime;

namespace nfm_world_library.Lua;

/// <summary>
/// A class-based userdata wrapper that implements <see cref="ILuaUserData"/> for any type <typeparamref name="T"/>.
/// The metatable is code-generated per T by the Roslyn source generator and passed via the constructor,
/// making it immutable and avoiding runtime reflection.
/// A parameterless constructor provides a minimal fallback metatable for ad-hoc usage.
/// </summary>
/// <typeparam name="T">The wrapped type (class, struct, interface, array, delegate, or BCL type).</typeparam>
public class StructUserData<T> : ILuaUserData
{
    private readonly LuaTable? _metatable;

    /// <summary>
    /// Creates a new StructUserData with the code-generated shared metatable for <typeparamref name="T"/>.
    /// </summary>
    public StructUserData(LuaTable? metatable)
    {
        _metatable = metatable;
    }

    /// <summary>
    /// Creates a new StructUserData with a minimal fallback metatable (only __tostring).
    /// Use the parameterized constructor for proper code-generated metatables.
    /// </summary>
    public StructUserData()
    {
        _metatable = FallbackMetatable;
    }

    /// <summary>
    /// The wrapped value. Mutable so Lua writes via __newindex are visible.
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    /// Optional callback invoked after the wrapped value is mutated.
    /// Used for InlineArray write propagation back to the parent object.
    /// </summary>
    internal Action<T>? OnMutated { get; set; }

    /// <inheritdoc/>
    public LuaTable? Metatable
    {
        get => _metatable;
        set => throw new InvalidOperationException(
            "The metatable of StructUserData<T> is set at construction and cannot be changed.");
    }

    public static implicit operator LuaValue(StructUserData<T> value)
        => LuaValue.FromUserData(value);

    // Minimal fallback metatable — used when no code-generated metatable is provided.
    // Provides basic __index (array/list), __len (array/list), and __tostring.
    private static LuaTable FallbackMetatable
    {
        get
        {
            if (field != null) return field;
            var mt = new LuaTable();
            mt[Metamethods.Index] = new LuaFunction("__index", (context, ct) =>
            {
                var wrapper = context.GetArgument<StructUserData<T>>(0);
                var key = context.GetArgument(1);
                if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)
                {
                    var i = (int)n - 1;
                    if (wrapper.Value is Array arr && (uint)i < (uint)arr.Length)
                        return new(context.Return(LuaValue.FromObject(arr.GetValue(i)!)));
                    if (wrapper.Value is System.Collections.IList list && (uint)i < (uint)list.Count)
                        return new(context.Return(LuaValue.FromObject(list[i]!)));
                }
                return new(context.Return(LuaValue.Nil));
            });
            mt[Metamethods.NewIndex] = new LuaFunction("__newindex", (context, ct) =>
            {
                var wrapper = context.GetArgument<StructUserData<T>>(0);
                var key = context.GetArgument(1);
                var val = context.GetArgument(2);
                if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)
                {
                    var i = (int)n - 1;
                    if (wrapper.Value is Array arr && (uint)i < (uint)arr.Length)
                        arr.SetValue(val.Read<object>(), i);
                    else if (wrapper.Value is System.Collections.IList list && (uint)i < (uint)list.Count)
                        list[i] = val.Read<object>();
                }
                return new(context.Return());
            });
            mt[Metamethods.Len] = new LuaFunction("__len", (context, ct) =>
            {
                var wrapper = context.GetArgument<StructUserData<T>>(0);
                var len = wrapper.Value switch
                {
                    Array a => a.Length,
                    System.Collections.ICollection c => c.Count,
                    _ => 0
                };
                return new(context.Return((double)len));
            });
            mt[Metamethods.ToString] = new LuaFunction("__tostring", (context, ct) =>
            {
                var wrapper = context.GetArgument<StructUserData<T>>(0);
                return new(context.Return(wrapper.Value?.ToString() ?? "<nil>"));
            });
            Interlocked.CompareExchange(ref field, mt, null);
            return field;
        }
    }
}

/// <summary>
/// Thread-safe registry mapping each <typeparamref name="T"/> to its code-generated StructUserData metatable.
/// Populated at assembly load time by the source-generated <see cref="StructUserDataMetatableInitializer"/>.
/// </summary>
public static class StructUserDataMetatableRegistry<T>
{
    // ReSharper disable once StaticMemberInGenericType
    public static LuaTable? Metatable { get; private set; }

    public static void Register(LuaTable metatable) => Metatable = metatable;
}

/// <summary>
/// Helper for wrapping a value into <see cref="StructUserData{T}"/> using the registered metatable.
/// </summary>
public static class StructUserDataHelper
{
    /// <summary>
    /// Wraps <paramref name="value"/> into a <see cref="StructUserData{T}"/> if a metatable is registered for <typeparamref name="T"/>.
    /// Returns false with a fallback (parameterless) StructUserData if no metatable is registered.
    /// </summary>
    public static StructUserData<T> Wrap<T>(T value)
    {
        if (StructUserDataMetatableRegistry<T>.Metatable is { } mt)
            return new StructUserData<T>(mt) { Value = value };
        return new StructUserData<T> { Value = value };
    }

    /// <summary>
    /// Wraps <paramref name="value"/> into a <see cref="StructUserData{T}"/> if a metatable is registered for <typeparamref name="T"/>.
    /// Returns false when no metatable is registered (result is default).
    /// </summary>
    public static bool TryWrap<T>(T value, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out StructUserData<T> result)
    {
        if (StructUserDataMetatableRegistry<T>.Metatable is { } mt)
        {
            result = new StructUserData<T>(mt) { Value = value };
            return true;
        }
        result = default;
        return false;
    }
}
