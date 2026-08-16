using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Lua;
using MemoryPack;
using nfm_world_library.Lua;
using NFMWorld.Sentry;

namespace NFMWorldLibrary;

[MemoryPackable(GenerateType.VersionTolerant)]
[LuaVisible]
public partial record struct CarStats
{
    [JsonPropertyName("swits"), LuaName("swits"), MemoryPackOrder(0)] public Int3 Swits { get; init; }
    [JsonPropertyName("acelf"), LuaName("acelf"), MemoryPackOrder(1)] public f64Vector3 Acelf { get; init; }
    [JsonPropertyName("handb"), LuaName("handb"), MemoryPackOrder(2)] public int Handb { get; init; }
    [JsonPropertyName("airs"), LuaName("airs"), MemoryPackOrder(3)] public fix64 Airs { get; init; }
    [JsonPropertyName("airc"), LuaName("airc"), MemoryPackOrder(4)] public int Airc { get; init; }
    // ReSharper disable once InconsistentNaming
    [JsonIgnore, MemoryPackOrder(5)] public int _deprecated_Turn { get; init; }
    [JsonPropertyName("grip"), LuaName("grip"), MemoryPackOrder(6)] public fix64 Grip { get; init; }
    [JsonPropertyName("bounce"), LuaName("bounce"), MemoryPackOrder(7)] public fix64 Bounce { get; init; }
    [JsonPropertyName("simag"), LuaName("simag"), MemoryPackOrder(8)] public fix64 Simag { get; init; }
    [JsonPropertyName("moment"), LuaName("moment"), MemoryPackOrder(9)] public fix64 Moment { get; init; }
    [JsonPropertyName("comprad"), LuaName("comprad"), MemoryPackOrder(10)] public fix64 Comprad { get; init; }
    [JsonPropertyName("push"), LuaName("push"), MemoryPackOrder(11)] public fix64 Push { get; init; }
    [JsonPropertyName("revpush"), LuaName("revpush"), MemoryPackOrder(12)] public fix64 Revpush { get; init; }
    [JsonPropertyName("lift"), LuaName("lift"), MemoryPackOrder(13)] public int Lift { get; init; }
    [JsonPropertyName("revlift"), LuaName("revlift"), MemoryPackOrder(14)] public int Revlift { get; init; }
    [JsonPropertyName("powerloss"), LuaName("powerloss"), MemoryPackOrder(15)] public int Powerloss { get; init; }
    [JsonPropertyName("flipy"), LuaName("flipy"), MemoryPackOrder(16)] public int Flipy { get; init; }
    [JsonPropertyName("msquash"), LuaName("msquash"), MemoryPackOrder(17)] public int Msquash { get; init; }
    [JsonPropertyName("clrad"), LuaName("clrad"), MemoryPackOrder(18)] public int Clrad { get; init; } 
    [JsonPropertyName("dammult"), LuaName("dammult"), MemoryPackOrder(19)] public fix64 Dammult { get; init; }
    [JsonPropertyName("maxmag"), LuaName("maxmag"), MemoryPackOrder(20)] public int Maxmag { get; init; }
    [JsonPropertyName("dishandle"), LuaName("dishandle"), MemoryPackOrder(21)] public fix64 Dishandle { get; init; }
    [JsonPropertyName("outdam"), LuaName("outdam"), MemoryPackOrder(22)] public fix64 Outdam { get; init; }
    [JsonPropertyName("name"), LuaName("name"), MemoryPackOrder(23)] public string Name { get; init; }
    [JsonPropertyName("enginsignature"), LuaName("enginsignature"), MemoryPackOrder(24)] public sbyte Enginsignature { get; init; }
    [JsonPropertyName("turnradius"), LuaName("turnradius"), MemoryPackOrder(25)] public int TurnRadius { get; set; }
    [JsonPropertyName("roadgrip"), LuaName("roadgrip"), MemoryPackOrder(26)] public fix64? RoadGrip { get; set; }
    [JsonPropertyName("offroadgrip"), LuaName("offroadgrip"), MemoryPackOrder(27)] public fix64? OffRoadGrip { get; set; }
    [JsonPropertyName("offtrackgrip"), LuaName("offtrackgrip"), MemoryPackOrder(28)] public fix64? OffTrackGrip { get; set; }
    [JsonPropertyName("turn"), LuaName("turn"), MemoryPackOrder(29)] public fix64 Turn { get; init; }

    /// <summary>
    /// Tornado Shark stats, used as a fallback if a car has incomplete or invalid stats in the rad file.
    /// </summary>
    public static CarStats Default = new(
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
        "Tornado Shark"
    );

    public CarStats() : this(null)
    {
    }
    
    [MemoryPackConstructor]
    public CarStats(
        Int3? Swits = null,
        f64Vector3? Acelf = null,
        int Handb = int.MinValue,
        fix64? Airs = null,
        int Airc = int.MinValue,
        fix64? Turn = null,
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
        string Name = "hogan rewish",
        sbyte Enginsignature = 0,
        int TurnRadius = 36,
        fix64? RoadGrip = null,
        fix64? OffRoadGrip = null,
        fix64? OffTrackGrip = null)
    {
        this.Swits = Swits ?? new Int3(int.MinValue, int.MinValue, int.MinValue);
        this.Acelf = Acelf ?? new f64Vector3(fix64.MinValue, fix64.MinValue, fix64.MinValue);
        this.Handb = Handb;
        this.Airs = Airs ?? fix64.MinValue;
        this.Airc = Airc;
        this.Turn = Turn ?? fix64.MinValue;
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
        this.TurnRadius = TurnRadius;
        this.RoadGrip = RoadGrip;
        this.OffRoadGrip = OffRoadGrip;
        this.OffTrackGrip = OffTrackGrip;
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
        else if(Turn == fix64.MinValue) return ValidateFail(nameof(Turn));
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
        else if(TurnRadius == int.MinValue) return ValidateFail(nameof(TurnRadius));
        else if(Name == "hogan rewish") return ValidateFailName(fileName);

        return null;
    }

    private string ValidateFailName(string fileName)
    {
        SentrySdk.CaptureMessage($"Car name for car '{fileName}' was invalid or undefined. Falling back to file name.");
        Logging.Error($"Car name for car '{fileName}' was invalid or undefined. Falling back to file name.");
        return nameof(Name);
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
            if(invalidStat == nameof(Name) || string.IsNullOrEmpty(stats.Name))
            {
                stats = stats with { Name = fileName };
            }
        }

        return stats;
    }
}


/// <summary>
/// Represents a three dimensional mathematical vector.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
[LuaVisible]
public partial struct Int3 : IEquatable<Int3>
{
    /// <summary>
    /// A <see cref="Int3"/> with all of its components set to zero.
    /// </summary>
    public static readonly Int3 Zero = new();

    /// <summary>
    /// The X unit <see cref="Int3"/> (1, 0, 0).
    /// </summary>
    public static readonly Int3 UnitX = new(1, 0, 0);

    /// <summary>
    /// The Y unit <see cref="Int3"/> (0, 1, 0).
    /// </summary>
    public static readonly Int3 UnitY = new(0, 1, 0);

    /// <summary>
    /// The Z unit <see cref="Int3"/> (0, 0, 1).
    /// </summary>
    public static readonly Int3 UnitZ = new(0, 0, 1);

    /// <summary>
    /// A <see cref="Int3"/> with all of its components set to one.
    /// </summary>
    public static readonly Int3 One = new(1, 1, 1);

    /// <summary>
    /// The X component of the vector.
    /// </summary>
    [JsonPropertyName("x")] [LuaName] public int X;

    /// <summary>
    /// The Y component of the vector.
    /// </summary>
    [JsonPropertyName("y")] [LuaName] public int Y;

    /// <summary>
    /// The Z component of the vector.
    /// </summary>
    [JsonPropertyName("z")] [LuaName] public int Z;

    /// <summary>
    /// Initializes a new instance of the <see cref="Int3"/> struct.
    /// </summary>
    /// <param name="value">The value that will be assigned to all components.</param>
    public Int3(int value)
    {
        X = value;
        Y = value;
        Z = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Int3"/> struct.
    /// </summary>
    /// <param name="x">Initial value for the X component of the vector.</param>
    /// <param name="y">Initial value for the Y component of the vector.</param>
    /// <param name="z">Initial value for the Z component of the vector.</param>
    public Int3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    
    /// <summary>
    /// Tests for equality between two objects.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><c>true</c> if <paramref name="left"/> has the same value as <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Int3 left, Int3 right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tests for inequality between two objects.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><c>true</c> if <paramref name="left"/> has a different value than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Int3 left, Int3 right)
    {
        return !left.Equals(right);
    }
    
    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    /// <summary>
    /// Determines whether the specified <see cref="Int3"/> is equal to this instance.
    /// </summary>
    /// <param name="other">The <see cref="Int3"/> to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the specified <see cref="Int3"/> is equal to this instance; otherwise, <c>false</c>.
    /// </returns>
    public readonly bool Equals(Int3 other)
    {
        return other.X == X && other.Y == Y && other.Z == Z;
    }

    /// <summary>
    /// Determines whether the specified <see cref="object"/> is equal to this instance.
    /// </summary>
    /// <param name="value">The <see cref="object"/> to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the specified <see cref="object"/> is equal to this instance; otherwise, <c>false</c>.
    /// </returns>
    public readonly override bool Equals(object? value)
    {
        return value is Int3 i && Equals(i);
    }

    public int this[int index]
    {
        get
        {
            return index switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                _ => ThrowArgumentOutOfRangeException()
            };
        }
        set
        {
            switch (index)
            {
                case 0:
                    X = value;
                    return;
                case 1:
                    Y = value;
                    return;
                case 2:
                    Z = value;
                    return;
                default:
                    ThrowArgumentOutOfRangeException();
                    return;
            }
        }
    }

    [DoesNotReturn]
    private static int ThrowArgumentOutOfRangeException()
    {
        throw new ArgumentOutOfRangeException();
    }
}