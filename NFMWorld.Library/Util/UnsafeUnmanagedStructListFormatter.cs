using System.Buffers;
using System.Runtime.InteropServices;
using MessagePack;
using MessagePack.Formatters;

namespace NFMWorldLibrary.Util;

internal sealed unsafe class UnsafeUnmanagedStructListFormatter<T>(sbyte typeCode) : IMessagePackFormatter<List<T>?>
    where T : unmanaged
{
    public readonly sbyte TypeCode = typeCode;

    public void Serialize(ref MessagePackWriter writer, List<T>? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var byteCount = sizeof(T) * value.Count;
        writer.WriteExtensionFormatHeader(new ExtensionHeader(TypeCode, byteCount));
        if (byteCount == 0)
        {
            return;
        }

        var destinationSpan = writer.GetSpan(byteCount);
        fixed (void* destination = &destinationSpan[0])
        fixed (void* source = &CollectionsMarshal.AsSpan(value)[0])
        {
            Buffer.MemoryCopy(source, destination, byteCount, byteCount);
        }

        writer.Advance(byteCount);
    }

    public List<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var header = reader.ReadExtensionFormatHeader();
        if (header.TypeCode != TypeCode)
        {
            throw new MessagePackSerializationException("Extension TypeCode is invalid. typeCode: " + header.TypeCode);
        }

        if (header.Length == 0)
        {
            return [];
        }

        var elementCount = header.Length / sizeof(T);
        if (elementCount * sizeof(T) != header.Length)
        {
            throw new MessagePackSerializationException("Extension Length is invalid. actual: " + header.Length + ", element size: " + sizeof(T));
        }
        if (elementCount > int.MaxValue)
        {
            throw new MessagePackSerializationException("Extension Length is too large. element count: " + elementCount);
        }

        var answer = new List<T>((int)elementCount);
        CollectionsMarshal.SetCount(answer, (int)elementCount);
        reader.ReadRaw(header.Length).CopyTo(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(answer)));
        return answer;
    }
}