// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Maxine.Extensions.Mathematics;
using NFMWorld.LuaSourceGenerataor;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Util;
using File = System.IO.File;

var fields = new List<Field>();

foreach (var field in typeof(Mad).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
{
    var name = field.Name;
    if (name.StartsWith('_'))
        name = name[1..];
    name = char.ToLowerInvariant(name[0]) + name[1..];
    
    if (field.FieldType == typeof(int))
    {
        var getter =
            $"""
             lua_pushinteger(L, obj.{field.Name});
             return 1;          
             """;
        var setter =
            $"""
             var newValue = (int)lua_tointeger(L, 3);
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(float))
    {
        var getter =
            $"""
             lua_pushnumber(L, obj.{field.Name});
             return 1;          
             """;
        var setter =
            $"""
             var newValue = (float)lua_tonumber(L, 3);
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(bool))
    {
        var getter =
            $"""
             lua_pushboolean(L, obj.{field.Name} ? 1 : 0);
             return 1;          
             """;
        var setter =
            $"""
             var newValue = lua_toboolean(L, 3) != 0;
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(string))
    {
        var getter =
            $"""
             lua_pushstring(L, obj.{field.Name});
             return 1;          
             """;
        var setter =
            $"""
             var newValue = lua_tostring(L, 3);
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(Int3))
    {
        var getter =
            $"""
             // Int3 is a struct, so we need to use a lambda to access the field
             PushInt3(L, obj, static obj => obj.{field.Name});
             return 1;          
             """;
        var setter =
            $"""
             var newValue = ReadInt3(L, 3);
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(int[,]))
    {
        var getter =
            $"""
             PushInt2DArray(L, obj.{field.Name});
             return 1;          
             """;
        var setter =
            $$"""
             var newValue = ReadInt2DArray(L, 3);
             for (int i = 0; i < newValue.GetLength(0); i++)
             {
                 for (int j = 0; j < newValue.GetLength(1); j++)
                 {
                     obj.{{field.Name}}[i, j] = newValue[i, j];
                 }
             }
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(UnlimitedArray<bool>))
    {
        var getter =
            $"""
             PushBoolUnlimitedArray(L, obj.{field.Name});
             return 1;          
             """;
        var setter =
            $$"""
             var newValue = ReadBoolUnlimitedArray(L, 3);
             for (int i = 0; i < newValue.Count; i++)
             {
                 obj.{{field.Name}}[i] = newValue[i];
             }
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(InlineArray4<fix64>))
    {
        var getter =
            $"""
             // InlineArray4 is a struct, so we need to use a lambda to access the field
             PushFix64InlineArray4(L, obj, static obj => obj.{field.Name});
             return 1;
             """;
        var setter =
            $"""
             var newValue = ReadFix64InlineArray4(L, 3);
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
    else if (field.FieldType == typeof(f64Vector3))
    {
        var getter =
            $"""
             // f64Vector3 is a struct, so we need to use a lambda to access the field
             PushF64Vector3(L, obj, static obj => obj.{field.Name});
             return 1;          
             """;
        var setter =
            $"""
             var newValue = ReadF64Vector3(L, 3);
             obj.{field.Name} = newValue;
             return 0;
             """;
        fields.Add(new Field(name, getter, setter));
    }
}

var sb = new StringBuilder();
sb.AppendLine(
    """
    // ReSharper disable InconsistentNaming

    using System.Runtime.InteropServices;
    
    namespace nfm_world.mad;

    public partial class Lua
    {
        public int GetMadField(lua_State L, Mad obj, string field)
        {
            switch (field)
            {
    """);
foreach (var field in fields)
{
    sb.AppendLine(
        $"""
                case "{field.Name}":
    {IndentLines(field.Getter, 4)}
    """);
}

sb.AppendLine(
    """
                default:
                    lua_pushnil(L);
                    return 1;
            }
        }

        public int SetMadField(lua_State L, Mad obj, string field)
        {
            switch (field)
            {
    """);
foreach (var field in fields)
{
    sb.AppendLine(
        $$"""
                case "{{field.Name}}":
                {
    {{IndentLines(field.Setter, 4)}}
                }
    """);
}
sb.AppendLine(
    """
                default:
                    return 0;
            }
        }
    }
    """);

File.WriteAllText(@"C:\Users\maxinelocal\Git\NFM-World\nfm-world\mad\Lua.generated.cs", sb.ToString());

return;

static string IndentLines(string lines, int indentLevel)
{
    var indent = new string(' ', indentLevel * 4);
    var indentedLines = lines.Replace("\n", "\n" + indent);
    return indent + indentedLines;
}

namespace NFMWorld.LuaSourceGenerataor
{
    public readonly record struct Field(string Name, string Getter, string Setter);
}