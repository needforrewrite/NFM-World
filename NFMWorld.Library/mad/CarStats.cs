using System.Text.Json.Serialization;
using MessagePack;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

[MessagePackObject]
public record struct CarStats
{
    [JsonPropertyName("swits"), Key(0)] public Int3 Swits { get; init; }
    [JsonPropertyName("acelf"), Key(1)] public f64Vector3 Acelf { get; init; }
    [JsonPropertyName("handb"), Key(2)] public int Handb { get; init; }
    [JsonPropertyName("airs"), Key(3)] public fix64 Airs { get; init; }
    [JsonPropertyName("airc"), Key(4)] public int Airc { get; init; }
    [JsonPropertyName("turn"), Key(5)] public int Turn { get; init; }
    [JsonPropertyName("grip"), Key(6)] public fix64 Grip { get; init; }
    [JsonPropertyName("bounce"), Key(7)] public fix64 Bounce { get; init; }
    [JsonPropertyName("simag"), Key(8)] public fix64 Simag { get; init; }
    [JsonPropertyName("moment"), Key(9)] public fix64 Moment { get; init; }
    [JsonPropertyName("comprad"), Key(10)] public fix64 Comprad { get; init; }
    [JsonPropertyName("push"), Key(11)] public fix64 Push { get; init; }
    [JsonPropertyName("revpush"), Key(12)] public fix64 Revpush { get; init; }
    [JsonPropertyName("lift"), Key(13)] public int Lift { get; init; }
    [JsonPropertyName("revlift"), Key(14)] public int Revlift { get; init; }
    [JsonPropertyName("powerloss"), Key(15)] public int Powerloss { get; init; }
    [JsonPropertyName("flipy"), Key(16)] public int Flipy { get; init; }
    [JsonPropertyName("msquash"), Key(17)] public int Msquash { get; init; }
    [JsonPropertyName("clrad"), Key(18)] public int Clrad { get; init; } 
    [JsonPropertyName("dammult"), Key(19)] public fix64 Dammult { get; init; }
    [JsonPropertyName("maxmag"), Key(20)] public int Maxmag { get; init; }
    [JsonPropertyName("dishandle"), Key(21)] public fix64 Dishandle { get; init; }
    [JsonPropertyName("outdam"), Key(22)] public fix64 Outdam { get; init; }
    [JsonPropertyName("name"), Key(23)] public string Name { get; init; }
    [JsonPropertyName("enginsignature"), Key(24)] public sbyte Enginsignature { get; init; }

    /// <summary>
    /// Tornado Shark stats, used as a fallback if a car has incomplete or invalid stats in the rad file.
    /// </summary>
    public static CarStats Default = new CarStats(
        new Int3(50, 185, 282),
        new f64Vector3((fix64)11.0f, (fix64)5.0f, (fix64)3.0f),
        7,
        (fix64)1.0f,
        70,
        6,
        (fix64)20.0f,
        (fix64)1.2f,
        (fix64)0.9f,
        (fix64)1.3f,
        (fix64)0.5f,
        2,
        2,
        0,
        0,
        2500000,
        -50,
        7,
        3300,
        (fix64)0.75f,
        7600,
        (fix64)0.65f,
        (fix64)0.68f,
        "Tornado Shark",
        0
    );

    public CarStats() : this(null)
    {
    }
    
    public CarStats(
        Int3? Swits = null,
        f64Vector3? Acelf = null,
        int Handb = int.MinValue,
        fix64? Airs = null,
        int Airc = int.MinValue,
        int Turn = int.MinValue,
        fix64? Grip = null,
        fix64? Bounce = null,
        fix64? Simag = null,
        fix64? Moment = null,
        fix64? Comprad = null,
        fix64? Push = null,
        fix64? Revpush = null,
        int Lift = int.MinValue,
        int Revlift = int.MinValue,
        int Powerloss = int.MinValue,
        int Flipy = -100,
        int Msquash = int.MinValue,
        int Clrad = int.MinValue,
        fix64? Dammult = null,
        int Maxmag = 7,
        fix64? Dishandle = null,
        fix64? Outdam = null,
        string Name = "",
        sbyte Enginsignature = 0)
    {
        this.Swits = Swits ?? new Int3(int.MinValue, int.MinValue, int.MinValue);
        this.Acelf = Acelf ?? new f64Vector3(fix64.MinValue, fix64.MinValue, fix64.MinValue);
        this.Handb = Handb;
        this.Airs = Airs ?? fix64.MinValue;
        this.Airc = Airc;
        this.Turn = Turn;
        this.Grip = Grip ?? fix64.MinValue;
        this.Bounce = Bounce ?? fix64.MinValue;
        this.Simag = Simag ?? (fix64)1.3f;
        this.Moment = Moment ?? fix64.MinValue;
        this.Comprad = Comprad ?? fix64.MinValue;
        this.Push = Push ?? fix64.MinValue;
        this.Revpush = Revpush ?? fix64.MinValue;
        this.Lift = Lift;
        this.Revlift = Revlift;
        this.Powerloss = Powerloss;
        this.Flipy = Flipy;
        this.Msquash = Msquash;
        this.Clrad = Clrad;
        this.Dammult = Dammult ?? fix64.MinValue;
        this.Maxmag = Maxmag;
        this.Dishandle = Dishandle ?? fix64.MinValue;
        this.Outdam = Outdam ?? fix64.MinValue;
        this.Name = Name;
        this.Enginsignature = Enginsignature;
    }

    /// <summary>
    /// Validates the car stats by checking they are all defined. Sends error to console if not valid.
    /// </summary>
    /// <returns>the first invalid property name if any, or null if all are valid</returns>
    public string? Validate(string fileName)
    {
        if(Swits[0] == int.MinValue) return ValidateFail(nameof(Swits));
        else if(Acelf.AsSpan()[0] == fix64.MinValue) return ValidateFail(nameof(Acelf));
        else if(Handb == int.MinValue) return ValidateFail(nameof(Handb));
        else if(Airs == fix64.MinValue) return ValidateFail(nameof(Airs));
        else if(Airc == int.MinValue) return ValidateFail(nameof(Airc));
        else if(Turn == int.MinValue) return ValidateFail(nameof(Turn));
        else if(Grip == fix64.MinValue) return ValidateFail(nameof(Grip));
        else if(Bounce == fix64.MinValue) return ValidateFail(nameof(Bounce));
        //else if(Simag == fix64.MinValue) return ValidateFail(nameof(Simag));
        else if(Moment == fix64.MinValue) return ValidateFail(nameof(Moment));
        else if(Comprad == fix64.MinValue) return ValidateFail(nameof(Comprad));
        else if(Push == fix64.MinValue) return ValidateFail(nameof(Push));
        else if(Revpush == fix64.MinValue) return ValidateFail(nameof(Revpush));
        else if(Lift == int.MinValue) return ValidateFail(nameof(Lift));
        else if(Revlift == int.MinValue) return ValidateFail(nameof(Revlift));
        else if(Powerloss == int.MinValue) return ValidateFail(nameof(Powerloss));
        else if(Clrad == int.MinValue) return ValidateFail(nameof(Clrad));
        else if(Dammult == fix64.MinValue) return ValidateFail(nameof(Dammult));
        else if(Maxmag == int.MinValue) return ValidateFail(nameof(Maxmag));
        else if(Outdam == fix64.MinValue) return ValidateFail(nameof(Outdam));
        else if(Name == "") return ValidateFailName(nameof(Name), fileName);

        return null;
    }

    private string ValidateFailName(string property, string fileName)
    {
        SentrySdk.CaptureMessage($"Car stat {property} for car '{fileName}' was invalid or undefined. Falling back to Tornado Shark stats for all stats.");
        Logging.Error($"Car stat {property} for car '{fileName}' was invalid or undefined. Falling back to Tornado Shark stats for all stats.");
        return property;
    }

    private string ValidateFail(string property)
    {
        SentrySdk.CaptureMessage($"Car stat {property} for car '{Name}' was invalid or undefined. Falling back to Tornado Shark stats for all stats.");
        Logging.Error($"Car stat {property} for car '{Name}' was invalid or undefined. Falling back to Tornado Shark stats for all stats.");
        return property;
    }

    public static CarStats ValidateStats(CarStats stats, string fileName)
    {
        string? invalidStat = stats.Validate(fileName);
        if (invalidStat != null)
        {
            stats = Default;
            if(invalidStat == nameof(stats.Name) || string.IsNullOrEmpty(stats.Name))
            {
                stats = stats with { Name = fileName };
            }
        }

        return stats;
    }
}