---@class LuaServerData
---@field playerCount integer
---@field playerId fun(self: LuaServerData, index: integer): string
---@field playerName fun(self: LuaServerData, index: integer): string
---@field playerVehicle fun(self: LuaServerData, index: integer): string
---@field playerPosition fun(self: LuaServerData, playerId: string): fixed64vector3|nil

LuaServerData = {}


---Creates a new LuaServerData
---@return LuaServerData
function LuaServerData.new() end
