using System.Runtime.CompilerServices;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Test fixture demonstrating inline array support.
/// Inline arrays are C# structs with InlineArrayAttribute that can be indexed but don't expose indexers via reflection.
/// </summary>
[LuaVisible(Name = "InlineBuffer")]
[InlineArray(10)]
public struct InlineBuffer
{
    private int _element0;
}

/// <summary>
/// A class that uses an inline array as a field.
/// </summary>
[LuaVisible(Name = "TypeWithInlineArray")]
public class TypeWithInlineArray
{
    public InlineBuffer buffer;

    public TypeWithInlineArray()
    {
        for (int i = 0; i < 10; i++)
        {
            buffer[i] = i * 10;
        }
    }

    public int GetBufferValue(int index)
    {
        return buffer[index];
    }

    public void SetBufferValue(int index, int value)
    {
        buffer[index] = value;
    }

    public int SumBuffer()
    {
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            sum += buffer[i];
        }
        return sum;
    }
}
