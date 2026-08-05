---@class AiNodeKindInstance : System_EnumInstance, System_IComparableInstance, System_IConvertibleInstance, System_ISpanFormattableInstance, System_IFormattableInstance
AiNodeKindInstance = {}

---@class (exact) AiNodeKind : System_Enum
---@field checkPoint AiNodeKindInstance
---@field road AiNodeKindInstance
---@field turn AiNodeKindInstance
---@field auto AiNodeKindInstance
---@field ramp AiNodeKindInstance
---@field halfpipe AiNodeKindInstance
---@field sequenceStart AiNodeKindInstance
---@field sequenceEnd AiNodeKindInstance
---@field fixRoadStart AiNodeKindInstance
---@field fixRamp AiNodeKindInstance
---@field fixHoop AiNodeKindInstance
---@field fixRoadEnd AiNodeKindInstance
---@field avoid AiNodeKindInstance
---@field reset AiNodeKindInstance

AiNodeKind = {}

---Creates a new AiNodeKind
---@return AiNodeKindInstance
function AiNodeKind.new() end
