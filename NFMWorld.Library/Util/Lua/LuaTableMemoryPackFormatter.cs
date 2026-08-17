using System.Buffers;
using Lua;
using MemoryPack;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Util;

public class LuaValueMemoryPackFormatterAttribute : MemoryPackCustomFormatterAttribute<LuaValueMemoryPackFormatterAttribute.Formatter, LuaValue>
{
    public class Formatter : IMemoryPackFormatter<LuaValue>
    {
        private const ushort TagNil = 0;
        private const ushort TagFalse = 1;
        private const ushort TagTrue = 2;
        private const ushort TagStr = 3;
        private const ushort TagNum = 4;
        private const ushort TagTab = 5;
        private const ushort TagFix64 = 6;
        private const ushort TagFix64V = 7;
        private const ushort TagFix64A = 8;
        private const ushort TagFix64E = 9;
        
        public void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref LuaValue value) where TBufferWriter : IBufferWriter<byte>
        {
            WriteLuaValue(ref writer, ref value);
        }

        private static void WriteLuaValue<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref readonly LuaValue value) where TBufferWriter : IBufferWriter<byte>
        {
            switch (value.Type)
            {
                case LuaValueType.Nil:
                    writer.WriteUnionHeader(TagNil);
                    break;
                case LuaValueType.Boolean:
                    if (value.ToBoolean())
                    {
                        writer.WriteUnionHeader(TagTrue);
                    }
                    else
                    {
                        writer.WriteUnionHeader(TagFalse);
                    }
                    break;
                case LuaValueType.String:
                    var str = value.ToString();
                    writer.WriteUnionHeader(TagStr);
                    writer.WriteString(str);
                    break;
                case LuaValueType.Number:
                    var num = value.Read<double>();
                    writer.WriteUnionHeader(TagNum);
                    writer.WriteUnmanaged(num);
                    break;
                case LuaValueType.Function:
                    throw new InvalidOperationException("Type function not serializable!");
                case LuaValueType.Thread:
                    throw new InvalidOperationException("Type thread not serializable!");
                case LuaValueType.LightUserData:
                    throw new InvalidOperationException("Type lightuserdata not serializable!");
                case LuaValueType.UserData:
                    throw new InvalidOperationException("Type userdata not serializable!");
                case LuaValueType.Table:
                    var t = value.Read<LuaTable>();
                    var len = t.Count();
                    writer.WriteUnionHeader(TagTab);
                    writer.WriteCollectionHeader(len);
                    foreach (var (k, v) in t)
                    {
                        WriteLuaValue(ref writer, in k);
                        WriteLuaValue(ref writer, in v);
                    }
                    break;
                case LuaValueType.Fixed64:
                    var fixed64 = value.Read<fix64>();
                    writer.WriteUnionHeader(TagFix64);
                    writer.WriteUnmanaged(fixed64);
                    break;
                case LuaValueType.Fixed64Vector3:
                    var fixed64Vec3 = value.Read<f64Vector3>();
                    writer.WriteUnionHeader(TagFix64V);
                    writer.WriteUnmanaged(fixed64Vec3);
                    break;
                case LuaValueType.Fixed64Angle:
                    var fixed64Ang = value.Read<f64AngleSingle>();
                    writer.WriteUnionHeader(TagFix64A);
                    writer.WriteUnmanaged(fixed64Ang);
                    break;
                case LuaValueType.Fixed64Euler:
                    var fixed64Eul = value.Read<f64Euler>();
                    writer.WriteUnionHeader(TagFix64E);
                    writer.WriteUnmanaged(fixed64Eul);
                    break;
                case LuaValueType.UserData2:
                    throw new InvalidOperationException("Type userdata not serializable!");
                default:
                    throw new InvalidOperationException("Type not serializable!");
            }
        }

        public void Deserialize(ref MemoryPackReader reader, scoped ref LuaValue value)
        {
            ReadLuaValue(ref reader, ref value);
        }

        private static void ReadLuaValue(ref MemoryPackReader reader, scoped ref LuaValue value)
        {
            if (!reader.TryReadUnionHeader(out var tag))
                throw new InvalidOperationException("Type not deserializable!");

            switch (tag)
            {
                case TagNil:
                    value = LuaValue.Nil;
                    break;
                case TagFalse:
                    value = new(false);
                    break;
                case TagTrue:
                    value = new(true);
                    break;
                case TagStr:
                    value = reader.ReadString();
                    break;
                case TagNum:
                    value = reader.ReadUnmanaged<double>();
                    break;
                case TagTab:
                    if (!reader.TryReadCollectionHeader(out var len))
                    {
                        throw new InvalidOperationException("Type not deserializable!");
                    }

                    var t = new LuaTable();

                    for (var i = 0; i < len; i++)
                    {
                        LuaValue k = default;
                        LuaValue v = default;
                        ReadLuaValue(ref reader, ref k);
                        ReadLuaValue(ref reader, ref v);

                        t[k] = v;
                    }
                    
                    value = t;
                    break;
                case TagFix64:
                    value = reader.ReadUnmanaged<fix64>();
                    break;
                case TagFix64V:
                    value = reader.ReadUnmanaged<f64Vector3>();
                    break;
                case TagFix64A:
                    value = reader.ReadUnmanaged<f64AngleSingle>();
                    break;
                case TagFix64E:
                    value = reader.ReadUnmanaged<f64Euler>();
                    break;
                default:
                    throw new InvalidOperationException("Type not deserializable!");
            }
        }
    }

    public override Formatter GetFormatter() => new();
}
