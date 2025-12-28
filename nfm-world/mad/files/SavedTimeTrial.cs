using System.Collections;
using Maxine.Extensions;
using MessagePack;
using NFMWorld.Mad;
using NFMWorld.Mad.packets;
using NFMWorld.Util;

[MessagePackObject]
public class SavedTimeTrial
{
    [Key(0)] public string CarName;
    [Key(1)] public string StageName;
    [Key(2)] public Demo DemoData;
    [Key(3)] public Splits Splits;

    public static string GetDirName(string carName, string stageName)
    {
        return new FileInfo(GetPathName(carName, stageName)).Directory?.FullName ?? "";
    }

    public static string GetPathName(string carName, string stageName)
    {
        return "data/tts/" + stageName + "/" + carName + ".timetrial";
    }

    public SavedTimeTrial(string carName, string stageName)
    {
        CarName = carName;
        StageName = stageName;

        DemoData = new Demo()
        {
            Ticks = new List<DemoEntry>()
        };
        Splits = new Splits()
        {
            SplitTimes = new List<long>()
        };
    }

    public static SavedTimeTrial? Load(string carName, string stageName)
    {
        if (System.IO.File.Exists(GetPathName(carName, stageName)))
        {
            using var compressedStream = new System.IO.Compression.DeflateStream(
                System.IO.File.OpenRead(GetPathName(carName, stageName)),
                System.IO.Compression.CompressionMode.Decompress
            );
            using var memoryStream = new MemoryStream();
            compressedStream.CopyTo(memoryStream);
            byte[] data = memoryStream.ToArray();
            return MessagePackSerializer.Deserialize<SavedTimeTrial>(data, MsgPackHelpers.Options);
        }
        return null;
    }

    public void Save()
    {
        if (!Directory.Exists(GetDirName(CarName, StageName)))
        {
            Directory.CreateDirectory(GetDirName(CarName, StageName));
        }

        // compress file using DeflateStream

        byte[] data = MessagePackSerializer.Serialize(this, MsgPackHelpers.Options);
        using var compressedStream = new System.IO.Compression.DeflateStream(
            System.IO.File.OpenWrite(GetPathName(CarName, StageName)),
            System.IO.Compression.CompressionMode.Compress
        );
        compressedStream.Write(data, 0, data.Length);
    }

    public void RecordTick(InGameCar car, int checkpointInLap, int lap)
    {
        DemoEntry entry = DemoEntry.Create(car, checkpointInLap, lap);
        DemoData.AddEntry(entry);
    }
    public Nibble<byte>? GetTick(int tick)
    {
        if(tick >= DemoData.Ticks.Count) return null;
        return DemoData.Ticks[tick].SerializedControl;
    }

    public void RecordSplit(long elapsed)
    {
        Splits.SplitTimes.Add(elapsed);
    }

    public long GetSplitDiff(SavedTimeTrial other, int sample)
    {
        return Splits.SplitTimes[sample] - other.Splits.SplitTimes[sample];
    }
}