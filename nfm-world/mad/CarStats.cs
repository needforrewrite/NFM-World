using System.Text.Json.Serialization;
using NFMWorld.Mad;
using SoftFloat;

public readonly record struct CarStats
{
    [JsonPropertyName("swits")] public Int3 Swits { get; init; }
    [JsonPropertyName("acelf")] public Vector3 Acelf { get; init; }
    [JsonPropertyName("handb")] public int Handb { get; init; }
    [JsonPropertyName("airs")] public fix64 Airs { get; init; }
    [JsonPropertyName("airc")] public int Airc { get; init; }
    [JsonPropertyName("turn")] public int Turn { get; init; }
    [JsonPropertyName("grip")] public fix64 Grip { get; init; }
    [JsonPropertyName("bounce")] public fix64 Bounce { get; init; }
    [JsonPropertyName("simag")] public fix64 Simag { get; init; }
    [JsonPropertyName("moment")] public fix64 Moment { get; init; }
    [JsonPropertyName("comprad")] public fix64 Comprad { get; init; }
    [JsonPropertyName("push")] public fix64 Push { get; init; }
    [JsonPropertyName("revpush")] public fix64 Revpush { get; init; }
    [JsonPropertyName("lift")] public int Lift { get; init; }
    [JsonPropertyName("revlift")] public int Revlift { get; init; }
    [JsonPropertyName("powerloss")] public int Powerloss { get; init; }
    [JsonPropertyName("flipy")] public int Flipy { get; init; }
    [JsonPropertyName("msquash")] public int Msquash { get; init; }
    [JsonPropertyName("clrad")] public int Clrad { get; init; }
    [JsonPropertyName("dammult")] public fix64 Dammult { get; init; }
    [JsonPropertyName("maxmag")] public int Maxmag { get; init; }
    [JsonPropertyName("dishandle")] public fix64 Dishandle { get; init; }
    [JsonPropertyName("outdam")] public fix64 Outdam { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; }
    [JsonPropertyName("enginsignature")] public sbyte Enginsignature { get; init; }

    /// <summary>
    /// Tornado Shark stats, used as a fallback if a car has incomplete or invalid stats in the rad file.
    /// </summary>
    public static CarStats Default = new CarStats(
        new Int3(50, 185, 282),
        new Vector3(11.0f, 5.0f, 3.0f),
        7,
        1.0f,
        70,
        6,
        20.0f,
        1.2f,
        0.9f,
        1.3f,
        0.5f,
        2,
        2,
        0,
        0,
        2500000,
        -50,
        7,
        3300,
        0.75f,
        7600,
        0.65f,
        0.68f,
        "Tornado Shark",
        0
    );

    public CarStats() : this(null)
    {
    }
    
    public CarStats(
        Int3? Swits = null,
        Vector3? Acelf = null,
        int Handb = int.MinValue,
        float Airs = float.NegativeInfinity,
        int Airc = int.MinValue,
        int Turn = int.MinValue,
        float Grip = float.NegativeInfinity,
        float Bounce = float.NegativeInfinity,
        float Simag = float.NegativeInfinity,
        float Moment = float.NegativeInfinity,
        float Comprad = float.NegativeInfinity,
        float Push = int.MinValue,
        float Revpush = float.NegativeInfinity,
        int Lift = int.MinValue,
        int Revlift = int.MinValue,
        int Powerloss = int.MinValue,
        int Flipy = int.MinValue,
        int Msquash = int.MinValue,
        int Clrad = int.MinValue,
        float Dammult = float.NegativeInfinity,
        int Maxmag = int.MinValue,
        float Dishandle = float.NegativeInfinity,
        float Outdam = float.NegativeInfinity,
        string Name = "",
        sbyte Enginsignature = sbyte.MinValue)
    {
        this.Swits = Swits ?? new Int3(int.MinValue, int.MinValue, int.MinValue);
        this.Acelf = Acelf ?? new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        this.Handb = Handb;
        this.Airs = (fix64)Airs;
        this.Airc = Airc;
        this.Turn = Turn;
        this.Grip = (fix64)Grip;
        this.Bounce = (fix64)Bounce;
        this.Simag = (fix64)Simag;
        this.Moment = (fix64)Moment;
        this.Comprad = (fix64)Comprad;
        this.Push = (fix64)Push;
        this.Revpush = (fix64)Revpush;
        this.Lift = Lift;
        this.Revlift = Revlift;
        this.Powerloss = Powerloss;
        this.Flipy = Flipy;
        this.Msquash = Msquash;
        this.Clrad = Clrad;
        this.Dammult = (fix64)Dammult;
        this.Maxmag = Maxmag;
        this.Dishandle = (fix64)Dishandle;
        this.Outdam = (fix64)Outdam;
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
        else if(Acelf.AsSpan()[0] == float.NegativeInfinity) return ValidateFail(nameof(Acelf));
        else if(Handb == int.MinValue) return ValidateFail(nameof(Handb));
        else if(Airs == fix64.MinValue) return ValidateFail(nameof(Airs));
        else if(Airc == int.MinValue) return ValidateFail(nameof(Airc));
        else if(Turn == int.MinValue) return ValidateFail(nameof(Turn));
        else if(Grip == fix64.MinValue) return ValidateFail(nameof(Grip));
        else if(Bounce == fix64.MinValue) return ValidateFail(nameof(Bounce));
        else if(Simag == fix64.MinValue) return ValidateFail(nameof(Simag));
        else if(Moment == fix64.MinValue) return ValidateFail(nameof(Moment));
        else if(Comprad == fix64.MinValue) return ValidateFail(nameof(Comprad));
        else if(Push == fix64.MinValue) return ValidateFail(nameof(Push));
        else if(Revpush == fix64.MinValue) return ValidateFail(nameof(Revpush));
        else if(Lift == int.MinValue) return ValidateFail(nameof(Lift));
        else if(Revlift == int.MinValue) return ValidateFail(nameof(Revlift));
        else if(Powerloss == int.MinValue) return ValidateFail(nameof(Powerloss));
        else if(Flipy == int.MinValue) return ValidateFail(nameof(Flipy));
        else if(Msquash == int.MinValue) return ValidateFail(nameof(Msquash));
        else if(Clrad == int.MinValue) return ValidateFail(nameof(Clrad));
        else if(Dammult == fix64.MinValue) return ValidateFail(nameof(Dammult));
        else if(Maxmag == int.MinValue) return ValidateFail(nameof(Maxmag));
        else if(Dishandle == fix64.MinValue) return ValidateFail(nameof(Dishandle));
        else if(Outdam == fix64.MinValue) return ValidateFail(nameof(Outdam));
        else if(Enginsignature == sbyte.MinValue) return ValidateFail(nameof(Enginsignature));
        else if(Name == "") return ValidateFailName(nameof(Name), fileName);

        return null;
    }

    private string ValidateFailName(string property, string fileName)
    {
        GameSparker.Writer.WriteLine($"Car stat {property} for car '{fileName}' was invalid or undefined. Falling back to Tornado Shark stats for all stats.", "error");
        return property;
    }

    private string ValidateFail(string property)
    {
        GameSparker.Writer.WriteLine($"Car stat {property} for car '{Name}' was invalid or undefined. Falling back to Tornado Shark stats for all stats.", "error");
        return property;
    }
}