using TightPack;

namespace TightPack.Tests;

/// <summary>A plain reference type with a parameterless constructor and settable members.</summary>
[TightPackable]
public partial class SimpleObject
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double Score { get; set; }
}

/// <summary>All-primitive struct: this is the blit candidate.</summary>
[TightPackable]
public partial struct TightStruct
{
    public int X;
    public float Y;
}

/// <summary>A struct whose field order forces padding, so it must be written member by member.</summary>
[TightPackable]
public partial struct PaddedStruct
{
    public byte A;
    public long B;
}

/// <summary>A struct with a nested blittable struct.</summary>
[TightPackable]
public partial struct NestedTightStruct
{
    public TightStruct Inner;
    public int Z;
}

/// <summary>Nullable<T> can never satisfy <c>where T : unmanaged</c>, so it is never blitted.</summary>
[TightPackable]
public partial struct NullableHolder
{
    public long? Value;
    public int Tag;
}

[TightPackable]
public partial class Child
{
    public int Value { get; set; }

    public override bool Equals(object? obj) => obj is Child other && other.Value == Value;

    public override int GetHashCode() => Value;

    public override string ToString() => $"Child({Value})";
}

[TightPackable]
public partial class Parent
{
    public Child? First { get; set; }
    public List<Child>? Children { get; set; }
    public int[]? Numbers { get; set; }
    public Dictionary<string, int>? Map { get; set; }
}

/// <summary>Added to the end over time; older payloads must still load.</summary>
[TightPackable(GenerateType.VersionTolerant)]
public partial class Evolvable
{
    public int First { get; set; }
    public string? Second { get; set; }
    public int Third { get; set; }
}

/// <summary>A cycle: each node points at the next, and the last points back at the first.</summary>
[TightPackable(GenerateType.CircularReference)]
public partial class Node
{
    public int Value { get; set; }
    public Node? Next { get; set; }
}

[TightPackable(GenerateType.Union)]
[TightPackUnion(1, typeof(Circle))]
[TightPackUnion(2, typeof(Square))]
public partial interface IShape
{
}

[TightPackable]
public partial class Circle : IShape
{
    public double Radius { get; set; }
}

[TightPackable]
public partial class Square : IShape
{
    public double Side { get; set; }
}

[TightPackable]
public partial class ShapeHolder
{
    public IShape? Shape { get; set; }
    public List<IShape>? Shapes { get; set; }
}

/// <summary>Exercises every BCL scalar the writer special-cases.</summary>
[TightPackable]
public partial class Scalars
{
    public bool Bool { get; set; }
    public byte Byte { get; set; }
    public sbyte SByte { get; set; }
    public char Char { get; set; }
    public short Int16 { get; set; }
    public ushort UInt16 { get; set; }
    public int Int32 { get; set; }
    public uint UInt32 { get; set; }
    public long Int64 { get; set; }
    public ulong UInt64 { get; set; }
    public float Single { get; set; }
    public double Double { get; set; }
    public Half Half { get; set; }
    public Int128 Int128 { get; set; }
    public UInt128 UInt128 { get; set; }
    public decimal Decimal { get; set; }
    public Guid Guid { get; set; }
    public DateTime DateTime { get; set; }
    public DateTimeOffset DateTimeOffset { get; set; }
    public TimeSpan TimeSpan { get; set; }
    public DateOnly DateOnly { get; set; }
    public TimeOnly TimeOnly { get; set; }
    public DayOfWeek Enum { get; set; }
}

/// <summary>Rebuilt through its constructor rather than setters.</summary>
[TightPackable]
public partial class Immutable
{
    [TightPackConstructor]
    public Immutable(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }
    public string Name { get; }
}

[TightPackable]
public partial class WithCallbacks
{
    public int Value { get; set; }

    public List<string> Log { get; } = [];

    [TightPackOnSerializing]
    private void OnSerializing() => Log.Add("serializing");

    [TightPackOnSerialized]
    private void OnSerialized() => Log.Add("serialized");

    [TightPackOnDeserializing]
    private void OnDeserializing() => Log.Add("deserializing");

    [TightPackOnDeserialized]
    private void OnDeserialized() => Log.Add("deserialized");
}

/// <summary>A real cycle through the same instance, requiring reference tracking.</summary>
[TightPackable(GenerateType.CircularReference)]
public partial class TreeNode
{
    public string? Name { get; set; }
    public TreeNode? Left { get; set; }
    public TreeNode? Right { get; set; }
}

[TightPackable]
public partial class StructHolder
{
    public TightStruct Tight { get; set; }
    public PaddedStruct Padded { get; set; }
    public NestedTightStruct Nested { get; set; }
    public NullableHolder Nullable { get; set; }
}

[TightPackable]
public partial class ListsOfPackables
{
    public List<TightStruct>? Structs { get; set; }
    public List<Child>? Children { get; set; }
    public List<int>? Primitives { get; set; }
    public List<string?>? Strings { get; set; }
}

/// <summary>Used by the array length guard: a blittable element type in a collection.</summary>
[TightPackable]
public partial class BigArray
{
    public TightStruct[]? Data { get; set; }
}
