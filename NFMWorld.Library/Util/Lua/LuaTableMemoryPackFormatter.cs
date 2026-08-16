using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Lua;
using MemoryPack;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Util;

public class LuaValueMemoryPackFormatterAttribute : MemoryPackCustomFormatterAttribute<LuaValueMemoryPackFormatterAttribute.Formatter, LuaValue>
{
    public class Formatter : IMemoryPackFormatter<LuaValue>
    {
        private static readonly IWrappedLuaValue.IWrappedLuaValueFormatter TheFormatter = new();

        public void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref LuaValue value) where TBufferWriter : IBufferWriter<byte>
        {
            var iWrappedLuaValue = ToIWrappedLuaValue(value);
            TheFormatter.Serialize(ref writer, ref iWrappedLuaValue);
        }

        public void Deserialize(ref MemoryPackReader reader, scoped ref LuaValue value)
        {
            IWrappedLuaValue? result = null;
            TheFormatter.Deserialize(ref reader, ref result);
            value = result != null ? FromIWrappedLuaValue(result) : LuaValue.Nil;
        }

        private static LuaValue FromIWrappedLuaValue(IWrappedLuaValue value)
        {
            switch (value)
            {
                case WrappedLuaBooleanValue wrappedLuaBooleanValue:
                    return new(wrappedLuaBooleanValue.Value);
                case WrappedLuaFixed64AngleValue wrappedLuaFixed64AngleValue:
                    return new(wrappedLuaFixed64AngleValue.Value);
                case WrappedLuaFixed64EulerValue wrappedLuaFixed64EulerValue:
                    return new(wrappedLuaFixed64EulerValue.Value);
                case WrappedLuaFixed64Value wrappedLuaFixed64Value:
                    return new(wrappedLuaFixed64Value.Value);
                case WrappedLuaFixed64Vector3Value wrappedLuaFixed64Vector3Value:
                    return new(wrappedLuaFixed64Vector3Value.Value);
                case WrappedLuaNilValue:
                    return LuaValue.Nil;
                case WrappedLuaNumberValue wrappedLuaNumberValue:
                    return new(wrappedLuaNumberValue.Value);
                case WrappedLuaStringValue wrappedLuaStringValue:
                    return new(wrappedLuaStringValue.Value);
                case WrappedLuaTableValue wrappedLuaTableValue:
                    var luaTable = new LuaTable();
                    foreach (var (k, v) in wrappedLuaTableValue)
                    {
                        luaTable[FromIWrappedLuaValue(k)] = FromIWrappedLuaValue(v);
                    }

                    return luaTable;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static IWrappedLuaValue ToIWrappedLuaValue(LuaValue value)
        {
            switch (value.Type)
            {
                case LuaValueType.Nil:
                    return new WrappedLuaNilValue();
                case LuaValueType.Boolean:
                    return new WrappedLuaBooleanValue { Value = value.ToBoolean() };
                case LuaValueType.String:
                    return new WrappedLuaStringValue { Value = value.ToString() };
                case LuaValueType.Number:
                    return new WrappedLuaNumberValue { Value = value.Read<double>() };
                case LuaValueType.Function:
                    throw new InvalidOperationException("Type function not serializable!");
                case LuaValueType.Thread:
                    throw new InvalidOperationException("Type thread not serializable!");
                case LuaValueType.LightUserData:
                    throw new InvalidOperationException("Type lightuserdata not serializable!");
                case LuaValueType.UserData:
                    throw new InvalidOperationException("Type userdata not serializable!");
                case LuaValueType.Table:
                    var dict = new Dictionary<IWrappedLuaValue, IWrappedLuaValue>();
                    foreach (var (k, v) in value.Read<LuaTable>())
                    {
                        dict[ToIWrappedLuaValue(k)] = ToIWrappedLuaValue(v);
                    }
                    return new WrappedLuaTableValue(dict);
                case LuaValueType.Fixed64:
                    return new WrappedLuaFixed64Value { Value = value.Read<fix64>() };
                case LuaValueType.Fixed64Vector3:
                    return new WrappedLuaFixed64Vector3Value { Value = value.Read<f64Vector3>() };
                case LuaValueType.Fixed64Angle:
                    return new WrappedLuaFixed64AngleValue { Value = value.Read<f64AngleSingle>() };
                case LuaValueType.Fixed64Euler:
                    return new WrappedLuaFixed64EulerValue { Value = value.Read<f64Euler>() };
                case LuaValueType.UserData2:
                    throw new InvalidOperationException("Type userdata not serializable!");
                default:
                    throw new InvalidOperationException("Type not serializable!");
            }
        }
    }

    public override Formatter GetFormatter() => new();
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(WrappedLuaNilValue))]
[MemoryPackUnion(1, typeof(WrappedLuaBooleanValue))]
[MemoryPackUnion(2, typeof(WrappedLuaStringValue))]
[MemoryPackUnion(3, typeof(WrappedLuaNumberValue))]
[MemoryPackUnion(4, typeof(WrappedLuaFixed64Value))]
[MemoryPackUnion(5, typeof(WrappedLuaFixed64Vector3Value))]
[MemoryPackUnion(6, typeof(WrappedLuaFixed64AngleValue))]
[MemoryPackUnion(7, typeof(WrappedLuaFixed64EulerValue))]
[MemoryPackUnion(8, typeof(WrappedLuaTableValue))]
public partial interface IWrappedLuaValue;

[MemoryPackable] public partial class WrappedLuaNilValue : IWrappedLuaValue;
[MemoryPackable] public partial class WrappedLuaBooleanValue : IWrappedLuaValue { public bool Value { get; set; } }
[MemoryPackable] public partial class WrappedLuaStringValue : IWrappedLuaValue { public required string Value { get; set; } }
[MemoryPackable] public partial class WrappedLuaNumberValue : IWrappedLuaValue { public double Value { get; set; } }

// Added NFMW types
[MemoryPackable] public partial class WrappedLuaFixed64Value : IWrappedLuaValue { public fix64 Value { get; set; } }
[MemoryPackable] public partial class WrappedLuaFixed64Vector3Value : IWrappedLuaValue { public f64Vector3 Value { get; set; } }
[MemoryPackable] public partial class WrappedLuaFixed64AngleValue : IWrappedLuaValue { public f64AngleSingle Value { get; set; } }
[MemoryPackable] public partial class WrappedLuaFixed64EulerValue : IWrappedLuaValue { public f64Euler Value { get; set; } }

[MemoryPackable(GenerateType.Collection)]
public partial class WrappedLuaTableValue(IDictionary<IWrappedLuaValue, IWrappedLuaValue> dictionaryImplementation)
    : IWrappedLuaValue, IDictionary<IWrappedLuaValue, IWrappedLuaValue>
{
    public WrappedLuaTableValue() : this(new Dictionary<IWrappedLuaValue, IWrappedLuaValue>())
    {
    }

    public IEnumerator<KeyValuePair<IWrappedLuaValue, IWrappedLuaValue>> GetEnumerator()
    {
        return dictionaryImplementation.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)dictionaryImplementation).GetEnumerator();
    }

    public void Add(KeyValuePair<IWrappedLuaValue, IWrappedLuaValue> item)
    {
        dictionaryImplementation.Add(item);
    }

    public void Clear()
    {
        dictionaryImplementation.Clear();
    }

    public bool Contains(KeyValuePair<IWrappedLuaValue, IWrappedLuaValue> item)
    {
        return dictionaryImplementation.Contains(item);
    }

    public void CopyTo(KeyValuePair<IWrappedLuaValue, IWrappedLuaValue>[] array, int arrayIndex)
    {
        dictionaryImplementation.CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<IWrappedLuaValue, IWrappedLuaValue> item)
    {
        return dictionaryImplementation.Remove(item);
    }

    public int Count => dictionaryImplementation.Count;

    public bool IsReadOnly => dictionaryImplementation.IsReadOnly;

    public void Add(IWrappedLuaValue key, IWrappedLuaValue value)
    {
        dictionaryImplementation.Add(key, value);
    }

    public bool ContainsKey(IWrappedLuaValue key)
    {
        return dictionaryImplementation.ContainsKey(key);
    }

    public bool Remove(IWrappedLuaValue key)
    {
        return dictionaryImplementation.Remove(key);
    }

    public bool TryGetValue(IWrappedLuaValue key, [MaybeNullWhen(false)] out IWrappedLuaValue value)
    {
        return dictionaryImplementation.TryGetValue(key, out value);
    }

    public IWrappedLuaValue this[IWrappedLuaValue key]
    {
        get => dictionaryImplementation[key];
        set => dictionaryImplementation[key] = value;
    }

    public ICollection<IWrappedLuaValue> Keys => dictionaryImplementation.Keys;

    public ICollection<IWrappedLuaValue> Values => dictionaryImplementation.Values;
}