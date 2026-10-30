---@class DeterministicRandom
---@field next fun(self: DeterministicRandom): integer
---@field nextBetween fun(self: DeterministicRandom, min: integer, max: integer): integer
---@field nextf64 fun(self: DeterministicRandom): fixed64

DeterministicRandom = {}


---Creates a new DeterministicRandom
---@param value fixed64
---@return DeterministicRandom
function DeterministicRandom.new(value) end

---@class LuaRect : System.IEquatable_LuaRect
---@field x number
---@field y number
---@field width number
---@field height number

LuaRect = {}


---@class Vector2 : System.IEquatable_Vector2
---@field x number
---@field y number

Vector2 = {}


---@class Vector3 : System.IEquatable_Vector3
---@field x number
---@field y number
---@field z number

Vector3 = {}


