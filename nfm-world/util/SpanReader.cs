using System.Buffers;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using NFMWorld.Mad;

namespace NFMWorld.Util;

public ref struct SpanReader(ReadOnlySpan<byte> span)
{
    public ReadOnlySpan<byte> _span = span;
    public int _index;
    
    public byte ReadByte()
    {
        return _span[_index++];
    }

    public sbyte ReadSByte()
    {
        return (sbyte)_span[_index++];
    }

    public ushort ReadUInt16()
    {
        var value = BitConverter.ToUInt16(_span[_index..(_index + sizeof(ushort))]);
        _index += sizeof(ushort);
        return value;
    }

    public uint ReadUInt32()
    {
        var value = BitConverter.ToUInt32(_span[_index..(_index + sizeof(uint))]);
        _index += sizeof(uint);
        return value;
    }

    public ulong ReadUInt64()
    {
        var value = BitConverter.ToUInt64(_span[_index..(_index + sizeof(ulong))]);
        _index += sizeof(ulong);
        return value;
    }

    public short ReadInt16()
    {
        var value = BitConverter.ToInt16(_span[_index..(_index + sizeof(short))]);
        _index += sizeof(short);
        return value;
    }

    public int ReadInt32()
    {
        var value = BitConverter.ToInt32(_span[_index..(_index + sizeof(int))]);
        _index += sizeof(int);
        return value;
    }

    public long ReadInt64()
    {
        var value = BitConverter.ToInt64(_span[_index..(_index + sizeof(long))]);
        _index += sizeof(long);
        return value;
    }

    public float ReadSingle()
    {
        var value = BitConverter.ToSingle(_span[_index..(_index + sizeof(float))]);
        _index += sizeof(float);
        return value;
    }

    public double ReadDouble()
    {
        var value = BitConverter.ToDouble(_span[_index..(_index + sizeof(double))]);
        _index += sizeof(double);
        return value;
    }

    public string ReadString()
    {
        int length = ReadInt32();
        string result = new string(MemoryMarshal.Cast<byte, char>(_span.Slice(_index, length * 2)));
        _index += length * 2;
        return result;
    }
    
    public ReadOnlySpan<char> ReadStringSpan()
    {
        int length = ReadInt32();
        var result = MemoryMarshal.Cast<byte, char>(_span.Slice(_index, length * 2));
        _index += length * 2;
        return result;
    }

    public void Advance(int count)
    {
        _index += count;
    }

    public unsafe T ReadMemory<T>() where T : unmanaged
    {
        var value = MemoryMarshal.Read<T>(_span);
        _index += sizeof(T);
        return value;
    }
}

public static class BufferWriterExtensions
{
    extension(IBufferWriter<byte> writer)
    {
        public void WriteString(string value)
        {
            var length = value.Length;
            writer.Write(length);
            var span = writer.GetSpan(length * 2);
            MemoryMarshal.Cast<char, byte>(value.AsSpan()).CopyTo(span);
            writer.Advance(length * 2);
        }
    }
}