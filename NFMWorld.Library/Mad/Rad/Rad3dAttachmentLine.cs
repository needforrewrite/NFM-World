using MemoryPack;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Rad;

[MemoryPackable(GenerateType.VersionTolerant), LuaVisible]
public readonly partial record struct Rad3dAttachmentLine([property: MemoryPackOrder(0), LuaName] AttachmentLineDirection Direction, [property: MemoryPackOrder(1), LuaName] fix64 Offset);