using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Files;

[MemoryPackable(GenerateType.VersionTolerant), PluginVisible]
public partial class Splits
{
    [MemoryPackOrder(0), PluginVisible] public List<long> SplitTimes = [];
}