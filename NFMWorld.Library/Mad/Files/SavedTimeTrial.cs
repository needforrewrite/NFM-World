using Maxine.Extensions;
using MemoryPack;
using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Files;

[MemoryPackable(GenerateType.CircularReference), PluginVisible]
public partial class SavedTimeTrial
{
    [PluginVisible] public const int CURRENT_VERSION = 5;

    [MemoryPackOrder(0), PluginVisible] public string CarName;
    [MemoryPackOrder(1), PluginVisible] public string StageName;
    [MemoryPackOrder(2)] public Demo.Demo DemoData;
    [MemoryPackOrder(3), PluginVisible] public Splits Splits;
    [MemoryPackOrder(4)] public int? Version; // New in version 1, defaults to 0
    [MemoryPackOrder(5)] public StageLoader? StageData; // New in version 2 
    [MemoryPackOrder(6)] public Rad3d? CarData; // New in version 2

    public static string GetDirName(string carName, string stageName)
    {
        return new FileInfo(GetPathName(carName, stageName)).Directory?.FullName ?? "";
    }

    public static string GetPathName(string carName, string stageName)
    {
        return "data/tts/" + stageName.Replace("/", "++") + "++" + carName.Replace("/", "++") + ".timetrial";
    }

    public static string GetOldPathName(string carName, string stageName)
    {
        return "data/tts/" + stageName + "/" + carName + ".timetrial";
    }

    public static IEnumerable<(string stageName, string carName, string fileName)> GetTimeTrials()
    {
        foreach (var file in VFS.EnumerateFiles("data/tts", "*.timetrial", SearchOption.AllDirectories))
        {
            var tt = Load(file);
            if (tt != null)
            {
                yield return (tt.StageName, tt.CarName, file);
            }
        }
    }

    [MemoryPackConstructor]
    private SavedTimeTrial()
    {
        DemoData = new Demo.Demo
        {
            Ticks = []
        };
        Splits = new Splits
        {
            SplitTimes = []
        };
        Version = CURRENT_VERSION;
    }

    private SavedTimeTrial(string carName, string stageName) : this()
    {
        CarName = carName;
        StageName = stageName;
    }

    [PluginVisible]
    public SavedTimeTrial(string carName, string stageName, StageLoader stageData, Rad3d carData) : this(carName, stageName)
    {
        StageData = stageData;
        CarData = carData;
    }

    [PluginVisible]
    public static SavedTimeTrial? Load(string carName, string stageName)
    {
        return Load(GetPathName(carName, stageName)) ?? Load(GetOldPathName(carName, stageName));
    }

    [PluginVisible]
    public static SavedTimeTrial? Load(string fileName)
    {
        try
        {
            if (VFS.FileExists(fileName))
            {
                using var stream = VFS.OpenRead(fileName);
                using var sequence = stream.AsPooledReadOnlySequence();
                var data = Deflate.Decompress(sequence.Sequence);
                return MemoryPackSerializer.Deserialize<SavedTimeTrial>(data, MemoryPackHelpers.Options);
            }
            else
            {
                Logging.Info($"No timetrial file for {fileName} found.");
            }
        }
        catch (Exception ex)
        {
            Logging.Info($"Failed to load SavedTimeTrial for {fileName}: {ex}");
        }

        return null;
    }

    public static SavedTimeTrial? Load(ReadOnlyMemory<byte> data)
    {
        var decompData = Deflate.Decompress(data.Span);
        return MemoryPackSerializer.Deserialize<SavedTimeTrial>(decompData, MemoryPackHelpers.Options);
    }

    [PluginVisible]
    public void Save()
    {
        // Through the VFS: the browser has no writable install directory, so the write root is the
        // in-memory filesystem there. The directory is implied by the path, so it needs no mount
        // of its own.
        using var fileStream = VFS.OpenWrite(GetPathName(CarName, StageName));
        var body = MemoryPackSerializer.Serialize(this, MemoryPackHelpers.Options);
        Deflate.Compress(body, fileStream.AsBufferWriter<byte>());
    }

    [PluginVisible]
    public void RecordTick(BackendCar car)
    {
        CarFrame entry = CarFrame.Create(car);
        DemoData.AddEntry(entry);
    }

    [PluginVisible]
    public (bool Up, bool Down, bool Left, bool Right, bool Handb)? GetTick(int tick)
    {
        if(tick >= DemoData.Ticks.Count) return null;
        var tickData = DemoData.GetEntry(tick);
        return (tickData.TheBitFlags.Up, tickData.TheBitFlags.Down, tickData.TheBitFlags.Left, tickData.TheBitFlags.Right, tickData.TheBitFlags.Handb);
    }

    [PluginVisible]
    public void RecordSplit(long elapsed)
    {
        Splits.SplitTimes.Add(elapsed);
    }

    [PluginVisible]
    public long GetSplitDiff(SavedTimeTrial other, int sample)
    {
        return Splits.SplitTimes[sample] - other.Splits.SplitTimes[sample];
    }

    [PluginVisible]
    public long GetLapTime(int checkpointsInLap, int lap)
    {
        if (checkpointsInLap <= 0) return 0;
        int startIndex = lap * checkpointsInLap;
        int endIndex = startIndex + checkpointsInLap;

        if (startIndex >= Splits.SplitTimes.Count) return 0;

        long startTime = startIndex == 0 ? 0 : Splits.SplitTimes[startIndex - 1];
        long endTime = endIndex - 1 < Splits.SplitTimes.Count ? Splits.SplitTimes[endIndex - 1] : Splits.SplitTimes[^1];

        return endTime - startTime;
    }
}