using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that tuple types in overloads produce valid C# identifiers (no parens/commas).
/// </summary>
[LuaVisible]
public partial class TypeWithTupleOverloads
{
    // Tuple as parameter — suffix should be e.g. _Tuple2
    public string ProcessTuple((int, int) coords) => $"({coords.Item1},{coords.Item2})";

    // Tuple with 3 elements
    public string ProcessTuple((int, int, int) point) => $"({point.Item1},{point.Item2},{point.Item3})";

    // Multiple tuples mixed with primitives
    public string ProcessMixed(int id, (string, bool) data) => $"{id}:{data.Item1}={data.Item2}";

    // Multiple tuple overloads of same method name
    public string Combine((int, int) a, (int, int) b) => "2x2";
    public string Combine((int, int, int) a, (int, int, int) b) => "3x3";
    public string Combine((int, int) a, int scalar) => "2x1";
}
