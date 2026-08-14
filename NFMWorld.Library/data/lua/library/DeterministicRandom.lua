---@class DeterministicRandom
---@field next fun(self: DeterministicRandom): integer
---@field nextf64 fun(self: DeterministicRandom): fixed64

DeterministicRandom = {}


---Creates a new DeterministicRandom
---@param value fixed64
---@return DeterministicRandom
function DeterministicRandom.new(value) end
