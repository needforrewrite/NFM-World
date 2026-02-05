using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

[LuaVisible(Name = "TypeWithArrays")]
public class TypeWithArrays
{
    // Properties with arrays
    public int[]? Numbers { get; set; }
    public string[]? Names { get; set; }
    public float[]? Values { get; set; }

    // Constructors
    public TypeWithArrays()
    {
    }

    public TypeWithArrays(int[] numbers)
    {
        Numbers = numbers;
    }

    public TypeWithArrays(string[] names, float[] values)
    {
        Names = names;
        Values = values;
    }

    // Methods
    public int[]? GetNumbers() => Numbers;

    public string[]? GetNames() => Names;

    public void SetNumbers(int[] numbers)
    {
        Numbers = numbers;
    }

    public void SetNames(string[] names)
    {
        Names = names;
    }

    public int SumNumbers()
    {
        if (Numbers == null) return 0;
        return Numbers.Sum();
    }

    public string ConcatenateNames()
    {
        if (Names == null) return "";
        return string.Join(", ", Names);
    }

    public int GetLength()
    {
        return Numbers?.Length ?? 0;
    }

    public int GetAt(int index)
    {
        if (Numbers == null || index < 0 || index >= Numbers.Length)
            return -1;
        return Numbers[index];
    }

    // Static methods
    public static int[] CreateArray(int size)
    {
        return new int[size];
    }

    public static string[] CreateStringArray(int size)
    {
        return new string[size];
    }

    public static int[] CreateSequence(int length)
    {
        return Enumerable.Range(1, length).ToArray();
    }
}
