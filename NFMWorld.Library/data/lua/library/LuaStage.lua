---@class LuaStage
---@field name string
---@field nlaps integer
---@field checkpointCount integer
---@field checkpointPosition fun(self: LuaStage, index: integer): fixed64vector3

LuaStage = {}


---Creates a new LuaStage
---@return LuaStage
function LuaStage.new() end
