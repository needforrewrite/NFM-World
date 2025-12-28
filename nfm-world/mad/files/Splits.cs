using MessagePack;

[MessagePackObject]
public class Splits
{
    [Key(0)] public List<long> SplitTimes = [];
}