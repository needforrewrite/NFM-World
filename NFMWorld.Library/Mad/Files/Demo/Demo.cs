using MemoryPack;

namespace NFMWorldLibrary.Files.Demo;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class Demo
{
    [MemoryPackOrder(0)] public List<NFMWorldLibrary.CarFrame> Ticks = [];

    public void AddEntry(NFMWorldLibrary.CarFrame entry)
    {
        Ticks.Add(entry);
    }

    public NFMWorldLibrary.CarFrame GetEntry(int tick)
    {
        return Ticks[tick];
    }
}
