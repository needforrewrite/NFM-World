using System.Collections;
using Maxine.Extensions;
using NFMWorld.Mad;
using NFMWorld.Util;

public class SavedTimeTrial
{
    public UnlimitedArray<Nibble<byte>> TickInputs;
    public UnlimitedArray<long> Splits;

    private string _carName;
    private string _stageName;

    private string _pathName;
    private string _dirName;

    public SavedTimeTrial(string carName, string stageName)
    {
        _carName = carName;
        _stageName = stageName;

        _dirName = "data/tts/" + stageName;
        _pathName = "data/tts/" + stageName + "/" + carName + ".timetrial";

        TickInputs = [];
        Splits = [];
    }

    public bool Load()
    {
        if (System.IO.File.Exists(_pathName))
        {
            using (B
                // read splits
                int splitsCount = reader.ReadInt32();
                for(int i = 0; i < splitsCount; i++)
                {
                    Splits[i] = reader.ReadInt32();
                }

                // read demo
                int entry;
                while(reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    entry = reader.ReadInt32();
                    TickInputs[TickInputs.Count] = (byte)entry;
                }
            }

            return true;
        }
        return false;
    }

    public void Save()
    {
        if (!Directory.Exists(_dirName))
        {
            Directory.CreateDirectory(_dirName);
        }

        using (BinaryWriter outputFile = new BinaryWriter(System.IO.File.OpenWrite(_pathName)))
        {
            // write splits
            outputFile.Write(Splits.Count);
            foreach(int split in Splits)
            {
                outputFile.Write(split);
            }

            // write demo
            foreach (var enc in TickInputs)
            {
                outputFile.Write((int)enc);
            }
        }
    }

    public void RecordTick(Control control)
    {
        var enc = control.Encode();
        TickInputs[TickInputs.Count] = enc;
    }

    public Nibble<byte>? GetTick(int tick)
    {
        if(tick >= TickInputs.Count) return null;
        return TickInputs[tick];
    }

    public void RecordSplit(long elapsed)
    {
        Splits[Splits.Count] = elapsed;
    }

    public long GetSplitDiff(SavedTimeTrial other, int sample)
    {
        return Splits[sample] - other.Splits[sample];
    }
}