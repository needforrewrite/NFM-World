using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Files;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public partial class Splits
{
    [TightPackOrder(0), PluginVisible] public List<long> SplitTimes = [];
}
