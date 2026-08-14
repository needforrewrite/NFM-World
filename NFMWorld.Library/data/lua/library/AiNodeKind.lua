---@class AiNodeKind : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

AiNodeKind = {}

---@type AiNodeKind
AiNodeKind.checkPoint = nil
---@type AiNodeKind
AiNodeKind.road = nil
---@type AiNodeKind
AiNodeKind.turn = nil
---@type AiNodeKind
AiNodeKind.auto = nil
---@type AiNodeKind
AiNodeKind.ramp = nil
---@type AiNodeKind
AiNodeKind.halfpipe = nil
---@type AiNodeKind
AiNodeKind.sequenceStart = nil
---@type AiNodeKind
AiNodeKind.sequenceEnd = nil
---@type AiNodeKind
AiNodeKind.fixRoadStart = nil
---@type AiNodeKind
AiNodeKind.fixRamp = nil
---@type AiNodeKind
AiNodeKind.fixHoop = nil
---@type AiNodeKind
AiNodeKind.fixRoadEnd = nil
---@type AiNodeKind
AiNodeKind.avoid = nil
---@type AiNodeKind
AiNodeKind.reset = nil

---Creates a new AiNodeKind
---@return AiNodeKind
function AiNodeKind.new() end
