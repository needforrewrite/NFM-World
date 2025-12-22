using NFMWorld.Util;

public class TimeTrialSplitsFile
{
    public UnlimitedArray<long> Splits;

    private string _carName;
    private string _stageName;

    private string _pathName;
    private string _dirName;

    public TimeTrialSplitsFile(string carName, string stageName)
    {
        _carName = carName;
        _stageName = stageName;

        _dirName = "data/tts/" + stageName;
        _pathName = "data/tts/" + stageName + "/" + carName + ".splits";

        Splits = [];
    }

    public bool Load()
    {
        if (System.IO.File.Exists(_pathName))
        {
            using (StreamReader reader = new(_pathName))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    long l = long.Parse(line ?? "");

                    Splits[Splits.Count] = l;
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

        using (StreamWriter outputFile = new StreamWriter(_pathName))
        {
            foreach (long time in Splits)
            {
                outputFile.WriteLine(time.ToString());
            }
        }
    }

    public void Record(long elapsed)
    {
        Splits[Splits.Count] = elapsed;
    }

    public long GetDiff(TimeTrialSplitsFile other, int sample)
    {
        return Splits[sample] - other.Splits[sample];
    }
}