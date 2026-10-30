using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct Rad3dAttachmentLine([property: MemoryPackOrder(0), PluginVisible] AttachmentLineDirection Direction, [property: MemoryPackOrder(1), PluginVisible] fix64 Offset);