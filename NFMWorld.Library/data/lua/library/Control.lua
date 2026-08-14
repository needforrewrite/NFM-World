---@class Control
---@field arrace boolean
---@field chatup integer
---@field down boolean
---@field enter boolean
---@field exit boolean
---@field handb boolean
---@field multion integer
---@field mutem boolean
---@field mutes boolean
---@field radar boolean
---@field right boolean
---@field up boolean
---@field left boolean
---@field lookback integer
---@field wall integer
---@field zyinv boolean
---@field encode fun(self: Control): Maxine.Extensions.Nibble_byte
---@field decode fun(self: Control, enc: Maxine.Extensions.Nibble_byte)
---@field decode fun(self: Control, enc: System.ValueTuple_bool_Up_bool_Down_bool_Left_bool_Right_bool_Handb)

Control = {}


---Creates a new Control
---@return Control
function Control.new() end
