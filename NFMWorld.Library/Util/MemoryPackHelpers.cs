using MemoryPack;

namespace NFMWorldLibrary.Util;

public static class MemoryPackHelpers
{
    public static MemoryPackSerializerOptions Options = new()
    {
        StringEncoding = StringEncoding.Utf8
    };
}