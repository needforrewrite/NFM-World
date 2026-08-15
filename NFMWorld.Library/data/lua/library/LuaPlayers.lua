---@class LuaPlayers
---@field count integer
---@field get fun(self: LuaPlayers, index: integer): ClientSidePlayer|nil

LuaPlayers = {}


---Creates a new LuaPlayers
---@return LuaPlayers
function LuaPlayers.new() end
