---@class DeterministicRandomInstance
---@field _random FixedMathSharp_Utility_DeterministicRandomInstance
DeterministicRandomInstance = {}

---@class (exact) DeterministicRandom

DeterministicRandom = {}

---Creates a new DeterministicRandom
---@param value fixed64
---@return DeterministicRandomInstance
function DeterministicRandom.new(value) end

---@param self DeterministicRandomInstance
---@return number
function DeterministicRandomInstance:next() end

---@param self DeterministicRandomInstance
---@return fixed64
function DeterministicRandomInstance:nextf64() end
