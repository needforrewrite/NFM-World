using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[TightPackable(GenerateType.VersionTolerant), TightPackAllowSerialize, PluginVisible]
public readonly partial record struct Rad3dAttachmentLine(
    [property: TightPackOrder(0), PluginVisible]
    AttachmentLineDirection Direction,
    [property: TightPackOrder(1), PluginVisible]
    fix64 Offset
);
