---@class ControlInstance
---@field arrace boolean
---@field chatup number
---@field down boolean
---@field enter boolean
---@field exit boolean
---@field handb boolean
---@field multion number
---@field mutem boolean
---@field mutes boolean
---@field radar boolean
---@field right boolean
---@field up boolean
---@field left boolean
---@field lookback number
---@field wall number
---@field zyinv boolean
ControlInstance = {}

---@class (exact) Control

---Creates a new Control
---@return ControlInstance
function Control.new() end

---@param self ControlInstance
---@param i number
function ControlInstance:falseo(i) end

---@param self ControlInstance
function ControlInstance:reset() end

---@param self ControlInstance
---@return Nibble_byteInstance
function ControlInstance:encode() end

---@param self ControlInstance
---@param enc Nibble_byteInstance
function ControlInstance:decode(enc) end

---@param self ControlInstance
---@param enc ValueTupleInstance
function ControlInstance:decode_tuple5(enc) end
