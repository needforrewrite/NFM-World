---@class Int3Instance : System_IEquatable_NFMWorldLibrary_Int3_Instance
---@field x number
---@field y number
---@field z number
Int3Instance = {}

---@class (exact) Int3
---@field zero Int3Instance
---@field unitX Int3Instance
---@field unitY Int3Instance
---@field unitZ Int3Instance
---@field one Int3Instance

Int3 = {}

---Creates a new Int3
---@param value number
---@return Int3Instance
function Int3.new(value) end

---Creates a new Int3
---@param x number
---@param y number
---@param z number
---@return Int3Instance
function Int3.new_int_int_int(x, y, z) end

---@return number
function Int3.throwArgumentOutOfRangeException() end

---@param self Int3Instance
---@return number
function Int3Instance:getHashCode() end

---@param self Int3Instance
---@param other Int3Instance
---@return boolean
function Int3Instance:equals(other) end

---@param self Int3Instance
---@param value any
---@return boolean
function Int3Instance:equals_obj(value) end
