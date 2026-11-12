using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Files.Demo;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public partial class Demo
{
    [TightPackOrder(0), PluginVisible] public List<CarFrame> Ticks = [];

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
