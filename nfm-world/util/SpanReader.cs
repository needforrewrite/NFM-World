using System.Buffers;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using NFMWorld.Mad;

namespace NFMWorld.Util;

public ref struct SpanReader(ReadOnlySpan<byte> span)
{
    public readonly ReadOnlySpan<byte> Span = span;
    public int Index { get; private set; } = 0;
    
    public byte ReadByte()
    {
        return Span[Index++];
    }

    public sbyte ReadSByte()
    {
        return (sbyte)Span[Index++];
    }

    public ushort ReadUInt16()
    {
        var value = BitConverter.ToUInt16(Span[Index..(Index + sizeof(ushort))]);
        Index += sizeof(ushort);
        return value;
    }

    public uint ReadUInt32()
    {
        var value = BitConverter.ToUInt32(Span[Index..(Index + sizeof(uint))]);
        Index += sizeof(uint);
        return value;
    }

    public ulong ReadUInt64()
    {
        var value = BitConverter.ToUInt64(Span[Index..(Index + sizeof(ulong))]);
        Index += sizeof(ulong);
        return value;
    }

    public short ReadInt16()
    {
        var value = BitConverter.ToInt16(Span[Index..(Index + sizeof(short))]);
        Index += sizeof(short);
        return value;
    }

    public int ReadInt32()
    {
        var value = BitConverter.ToInt32(Span[Index..(Index + sizeof(int))]);
        Index += sizeof(int);
        return value;
    }

    public long ReadInt64()
    {
        var value = BitConverter.ToInt64(Span[Index..(Index + sizeof(long))]);
        Index += sizeof(long);
        return value;
    }

    public float ReadSingle()
    {
        var value = BitConverter.ToSingle(Span[Index..(Index + sizeof(float))]);
        Index += sizeof(float);
        return value;
    }

    public double ReadDouble()
    {
        var value = BitConverter.ToDouble(Span[Index..(Index + sizeof(double))]);
        Index += sizeof(double);
        return value;
    }

    public string ReadString()
    {
        int length = ReadInt32();
        string result = new string(MemoryMarshal.Cast<byte, char>(Span.Slice(Index, length * 2)));
        Index += length * 2;
        return result;
    }
    
    public ReadOnlySpan<char> ReadStringSpan()
    {
        int length = ReadInt32();
        var result = MemoryMarshal.Cast<byte, char>(Span.Slice(Index, length * 2));
        Index += length * 2;
        return result;
    }

    public void Advance(int count)
    {
        Index += count;
    }

    public unsafe T ReadMemory<T>() where T : unmanaged
    {
        var value = MemoryMarshal.Read<T>(Span[Index..(Index + sizeof(T))]);
        Index += sizeof(T);
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