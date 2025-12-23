using System.Collections;
using Maxine.Extensions;
using NFMWorld.Mad;
using NFMWorld.Util;

public class TimeTrialDemoFile
{
    public UnlimitedArray<Nibble<byte>> TickInputs;

    private string _carName;
    private string _stageName;

    private string _pathName;
    private string _dirName;

    public TimeTrialDemoFile(string carName, string stageName)
    {
        _carName = carName;
        _stageName = stageName;

        _dirName = "data/tts/" + stageName;
        _pathName = "data/tts/" + stageName + "/" + carName + ".demo";

        TickInputs = [];
    }

    public bool Load()
    {
        if (System.IO.File.Exists(_pathName))
        {
            using (BinaryReader reader = new(System.IO.File.OpenRead(_pathName)))
            {
                while(reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var entry = reader.ReadInt32();

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
            foreach (var enc in TickInputs)
            {
                outputFile.Write((int)enc);
            }
        }
    }

    public void Record(Control control)
    {
        var enc = control.Encode();
        TickInputs[TickInputs.Count] = enc;
    }

    public Nibble<byte>? GetTick(int tick)
    {
        if(tick >= TickInputs.Count) return null;
        return TickInputs[tick];
    }
}