using FixedMathSharp.Utility;
using Lua;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util.Lua;

[LuaVisible]
[LuaName("DeterministicRandom")]
public partial class LuaDeterministicRandom(DeterministicRandom random)
{
    private DeterministicRandom _random = random;

    [LuaName("create")]
    public static LuaDeterministicRandom Create(fix64 value)
    {
        return new LuaDeterministicRandom(new DeterministicRandom((ulong)value.rawValue));
    }

    [LuaName("next")]
    public int Next()
    {
        return _random.Next();
    }

    [LuaName("nextf64")]
    public fix64 NextF64()
    {
        return _random.NextFixed6401();
    }
}