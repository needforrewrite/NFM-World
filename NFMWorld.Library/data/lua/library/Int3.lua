---@class Int3 : System.IEquatable_Int3
---@field x integer
---@field y integer
---@field z integer
---@field getHashCode fun(self: Int3): integer
---@field equals fun(self: Int3, other: Int3): boolean
---@field equals fun(self: Int3, value: System.object|nil): boolean

Int3 = {}

---@type Int3
Int3.zero = nil
---@type Int3
Int3.unitX = nil
---@type Int3
Int3.unitY = nil
---@type Int3
Int3.unitZ = nil
---@type Int3
Int3.one = nil

---Creates a new Int3
---@param value integer
---@return Int3
function Int3.new(value) end

---Creates a new Int3
---@param x integer
---@param y integer
---@param z integer
---@return Int3
function Int3.new(x, y, z) end

---Creates a new Int3
---@return Int3
function Int3.new() end
