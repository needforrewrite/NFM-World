using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Files.Demo;

[MemoryPackable(GenerateType.VersionTolerant), PluginVisible]
public partial class Demo
{
    [MemoryPackOrder(0), PluginVisible] public List<CarFrame> Ticks = [];

    [PluginVisible]
    public void AddEntry(CarFrame entry)
    {
        Ticks.Add(entry);
    }

    [PluginVisible]
    public CarFrame GetEntry(int tick)
    {
        return Ticks[tick];
    }
}
