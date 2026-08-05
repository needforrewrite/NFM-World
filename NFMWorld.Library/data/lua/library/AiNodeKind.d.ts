declare class AiNodeKind
    extends System_Enum
    implements System_IComparable, System_IConvertible, System_ISpanFormattable, System_IFormattable
{
    static readonly checkPoint: AiNodeKind;
    static readonly road: AiNodeKind;
    static readonly turn: AiNodeKind;
    static readonly auto: AiNodeKind;
    static readonly ramp: AiNodeKind;
    static readonly halfpipe: AiNodeKind;
    static readonly sequenceStart: AiNodeKind;
    static readonly sequenceEnd: AiNodeKind;
    static readonly fixRoadStart: AiNodeKind;
    static readonly fixRamp: AiNodeKind;
    static readonly fixHoop: AiNodeKind;
    static readonly fixRoadEnd: AiNodeKind;
    static readonly avoid: AiNodeKind;
    static readonly reset: AiNodeKind;
}
