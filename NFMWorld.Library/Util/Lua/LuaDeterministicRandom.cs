using FixedMathSharp.Utility;
using Lua;

namespace NFMWorldLibrary.Util.Lua;

[LuaObject("DeterministicRandom")]
public partial class LuaDeterministicRandom(DeterministicRandom random)
{
    private DeterministicRandom _random = random;

    [LuaMember("create")]
    public static LuaDeterministicRandom Create(fix64 value)
    {
        return new LuaDeterministicRandom(new DeterministicRandom((ulong)value.rawValue));
    }

    [LuaMember("next")]
    public int Next()
    {
        return _random.Next();
    }

    [LuaMember("nextf64")]
    public fix64 NextF64()
    {
        return _random.NextFixed6401();
    }
}