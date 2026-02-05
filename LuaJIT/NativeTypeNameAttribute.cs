#nullable disable
namespace LuaJIT;

public class NativeTypeNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}