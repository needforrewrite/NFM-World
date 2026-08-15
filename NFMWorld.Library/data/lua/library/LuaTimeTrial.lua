---@class LuaTimeTrial
---@field hasGhost boolean
---@field begin fun(self: LuaTimeTrial, car: IInGameCar)
---@field applyGhost fun(self: LuaTimeTrial, ghostCar: IInGameCar, tick: integer)
---@field record fun(self: LuaTimeTrial, car: IInGameCar)
---@field save fun(self: LuaTimeTrial)

LuaTimeTrial = {}


---Creates a new LuaTimeTrial
---@return LuaTimeTrial
function LuaTimeTrial.new() end
