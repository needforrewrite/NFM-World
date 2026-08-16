using nfm_world_library.Lua;

namespace NFMWorldLibrary;

public interface IAiNode : ITransform
{
    AiNodeKind Kind { get; }
    bool IsSpecial { get; }
}

[LuaVisible]
public enum AiNodeKind
{
    CheckPoint,
    Road,
    Turn,
    Auto,
    Ramp,
    Halfpipe,
    SequenceStart,
    SequenceEnd,
    FixRoadStart,
    FixRamp,
    FixHoop,
    FixRoadEnd,
    Avoid,
    Reset
}