using System.Runtime.CompilerServices;

namespace NFMWorldLibrary.Util;

public static class LuaProxies
{
    private static ConditionalWeakTable<object, object> _table = new();

    public static TProxied? Get<T, TProxied>(T value)
    {
        if (typeof(T).IsValueType || typeof(TProxied).IsValueType)
        {
            return default;
        }

        _table.TryGetValue(value, out var proxied);
        if (proxied is TProxied result)
            return result;
        return default;
    }

    public static TProxied GetOrAdd<T, TProxied>(T value, Func<T, TProxied> factory)
    {
        if (typeof(T).IsValueType || typeof(TProxied).IsValueType)
        {
            return factory(value);
        }
        
        var proxied = _table.GetOrAdd(value!, static (value, factory) => factory((T)value)!, factory);
        if (proxied is TProxied result)
            return result;
        throw new InvalidOperationException("Value was already proxied to a different type: " + proxied.GetType());
    }

    public static TProxied GetOrAdd<T, TProxied, TArg>(T value, Func<T, TArg, TProxied> factory, TArg factoryArgument)
    {
        if (typeof(T).IsValueType || typeof(TProxied).IsValueType)
        {
            return factory(value, factoryArgument);
        }

        var proxied = _table.GetOrAdd(value!, static (value, args) =>
        {
            var (factory, factoryArgument) = args;

            return factory((T)value, factoryArgument)!;
        }, (factory, factoryArgument));
        if (proxied is TProxied result)
            return result;
        throw new InvalidOperationException("Value was already proxied to a different type: " + proxied.GetType());
    }
}