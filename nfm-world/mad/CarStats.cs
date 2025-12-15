using System.Text.Json.Serialization;
using Stride.Core.Mathematics;

public readonly record struct CarStats
{
    [JsonPropertyName("swits")] public Int3 Swits { get; init; }
    [JsonPropertyName("acelf")] public Vector3 Acelf { get; init; }
    [JsonPropertyName("handb")] public int Handb { get; init; }
    [JsonPropertyName("airs")] public float Airs { get; init; }
    [JsonPropertyName("airc")] public int Airc { get; init; }
    [JsonPropertyName("turn")] public int Turn { get; init; }
    [JsonPropertyName("grip")] public float Grip { get; init; }
    [JsonPropertyName("bounce")] public float Bounce { get; init; }
    [JsonPropertyName("simag")] public float Simag { get; init; }
    [JsonPropertyName("moment")] public float Moment { get; init; }
    [JsonPropertyName("comprad")] public float Comprad { get; init; }
    [JsonPropertyName("push")] public int Push { get; init; }
    [JsonPropertyName("revpush")] public float Revpush { get; init; }
    [JsonPropertyName("lift")] public int Lift { get; init; }
    [JsonPropertyName("revlift")] public int Revlift { get; init; }
    [JsonPropertyName("powerloss")] public int Powerloss { get; init; }
    [JsonPropertyName("flipy")] public int Flipy { get; init; }
    [JsonPropertyName("msquash")] public int Msquash { get; init; }
    [JsonPropertyName("clrad")] public int Clrad { get; init; }
    [JsonPropertyName("dammult")] public float Dammult { get; init; }
    [JsonPropertyName("maxmag")] public int Maxmag { get; init; }
    [JsonPropertyName("dishandle")] public float Dishandle { get; init; }
    [JsonPropertyName("outdam")] public float Outdam { get; init; }
    [JsonPropertyName("cclass")] public sbyte Cclass { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; }
    [JsonPropertyName("enginsignature")] public sbyte Enginsignature { get; init; }

    public CarStats() : this(null)
    {
    }
    
    public CarStats(
        Int3? Swits = null,
        Vector3? Acelf = null,
        int Handb = -1,
        float Airs = -1,
        int Airc = -1,
        int Turn = -1,
        float Grip = -1,
        float Bounce = -1,
        float Simag = -1,
        float Moment = -1,
        float Comprad = -1,
        int Push = -1,
        float Revpush = -1,
        int Lift = -1,
        int Revlift = -1,
        int Powerloss = -1,
        int Flipy = -1,
        int Msquash = -1,
        int Clrad = -1,
        float Dammult = -1,
        int Maxmag = -1,
        float Dishandle = -1,
        float Outdam = -1,
        sbyte Cclass = -1,
        string Name = "Hogan Rewish",
        sbyte Enginsignature = -1)
    {
        this.Swits = Swits ?? new Int3(0, 0, 0);
        this.Acelf = Acelf ?? new Vector3(0f, 0f, 0f);
        this.Handb = Handb;
        this.Airs = Airs;
        this.Airc = Airc;
        this.Turn = Turn;
        this.Grip = Grip;
        this.Bounce = Bounce;
        this.Simag = Simag;
        this.Moment = Moment;
        this.Comprad = Comprad;
        this.Push = Push;
        this.Revpush = Revpush;
        this.Lift = Lift;
        this.Revlift = Revlift;
        this.Powerloss = Powerloss;
        this.Flipy = Flipy;
        this.Msquash = Msquash;
        this.Clrad = Clrad;
        this.Dammult = Dammult;
        this.Maxmag = Maxmag;
        this.Dishandle = Dishandle;
        this.Outdam = Outdam;
        this.Cclass = Cclass;
        this.Name = Name;
        this.Enginsignature = Enginsignature;
    }
}