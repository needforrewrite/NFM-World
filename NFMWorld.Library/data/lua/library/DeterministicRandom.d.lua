---@class DeterministicRandomInstance
---@field _random DeterministicRandomInstance
DeterministicRandomInstance = {}

---@class (exact) DeterministicRandom

DeterministicRandom = {}

---Creates a new DeterministicRandom
---@param random DeterministicRandomInstance
---@return DeterministicRandomInstance
function DeterministicRandom.new(random) end

---@param value fixed64
---@return LuaDeterministicRandomInstance
function DeterministicRandom.create(value) end

---@param self DeterministicRandomInstance
---@return number
function DeterministicRandomInstance:next() end

---@param self DeterministicRandomInstance
---@return fixed64
function DeterministicRandomInstance:nextf64() end
