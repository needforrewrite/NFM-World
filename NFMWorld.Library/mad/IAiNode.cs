namespace NFMWorldLibrary.Mad;

public interface IAiNode : ITransform
{
    AiNodeKind Kind { get; }
    bool IsSpecial { get; }
}

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