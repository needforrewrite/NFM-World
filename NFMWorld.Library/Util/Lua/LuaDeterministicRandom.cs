using FixedMathSharp.Utility;
using Lua;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util.Lua;

[LuaVisible, LuaName("DeterministicRandom")]
public partial class LuaDeterministicRandom(fix64 value)
{
    private DeterministicRandom _random = new((ulong)value.rawValue);

    [LuaName("next")]
    public int Next()
    {
        return _random.Next();
    }

    [LuaName("nextBetween")]
    public int NextBetween(int min, int max)
    {
        return _random.Next(min, max);
    }

    [LuaName("nextf64")]
    public fix64 NextF64()
    {
        return _random.NextFixed6401();
    }
}