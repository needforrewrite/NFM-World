using MessagePack;

namespace NFMWorldLibrary.Mad.Files;

[MessagePackObject]
public class Splits
{
    [Key(0)] public List<long> SplitTimes = [];
}