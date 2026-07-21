using Microsoft.IO;

namespace NFMWorld.Audio;

public static class MemoryManager
{
    public static RecyclableMemoryStreamManager Manager { get; } = new();
}