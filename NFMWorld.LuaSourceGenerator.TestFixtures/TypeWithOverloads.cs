using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

[LuaVisible]
public class TypeWithOverloads
{
    public int Value { get; set; }
    public string Text { get; set; } = "";

    // Constructors with same argument count but different types
    public TypeWithOverloads(int value)
    {
        Value = value;
        Text = "int";
    }

    public TypeWithOverloads(float value)
    {
        Value = (int)value;
        Text = "float";
    }

    public TypeWithOverloads(string text)
    {
        Value = text.Length;
        Text = "string:" + text;
    }

    // Methods with same argument count but different types
    public string ProcessNumber(int x) => $"int:{x}";
    public string ProcessNumber(double x) => $"double:{x}";
    public string ProcessNumber(long x) => $"long:{x}";
    public string ProcessNumber(float x) => $"float:{x}";

    public string ProcessData(string s) => $"string:{s}";
    public string ProcessData(int[] arr) => $"int[]:{arr.Length}";
    public string ProcessData(float[] arr) => $"float[]:{arr.Length}";
    public string ProcessData(bool flag) => $"bool:{flag}";

    // Two parameters with different type combinations
    public string Combine(int a, int b) => $"int,int:{a},{b}";
    public string Combine(float a, float b) => $"float,float:{a},{b}";
    public string Combine(string a, string b) => $"string,string:{a},{b}";
    public string Combine(int a, string b) => $"int,string:{a},{b}";
    public string Combine(string a, int b) => $"string,int:{a},{b}";

    // Static methods with overloads
    public static string StaticProcess(int x) => $"static:int:{x}";
    public static string StaticProcess(double x) => $"static:double:{x}";
    public static string StaticProcess(string s) => $"static:string:{s}";

    // Operator overloads with same argument count
    public static TypeWithOverloads operator -(TypeWithOverloads a) => new TypeWithOverloads(-a.Value);

    public static TypeWithOverloads operator +(TypeWithOverloads a, TypeWithOverloads b)
        => new TypeWithOverloads(a.Value + b.Value) { Text = "obj+obj" };

    public static TypeWithOverloads operator +(TypeWithOverloads a, int b)
        => new TypeWithOverloads(a.Value + b) { Text = "obj+int" };

    public static TypeWithOverloads operator +(int a, TypeWithOverloads b)
        => new TypeWithOverloads(a + b.Value) { Text = "int+obj" };

    public static TypeWithOverloads operator -(TypeWithOverloads a, TypeWithOverloads b)
        => new TypeWithOverloads(a.Value - b.Value) { Text = "obj-obj" };

    public static TypeWithOverloads operator -(TypeWithOverloads a, int b)
        => new TypeWithOverloads(a.Value - b) { Text = "obj-int" };
}
