using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

[PluginVisible(Visible.IncludingAllMembersOfType)]
public interface IAiNode : ITransform
{
    AiNodeKind Kind { get; }
    bool IsSpecial { get; }
}

[PluginVisible]
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