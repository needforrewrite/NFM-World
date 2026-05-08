using MessagePack;

namespace NFMWorldLibrary.Mad.Files.Demo;

[MessagePackObject]
public class Demo
{
    [Key(0)] public required List<DemoEntry> Ticks;

    public void AddEntry(DemoEntry entry)
    {
        Ticks.Add(entry);
    }

    public DemoEntry GetEntry(int tick)
    {
        return Ticks[tick];
    }
}