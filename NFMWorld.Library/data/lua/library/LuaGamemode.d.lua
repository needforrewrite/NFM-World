---@class LuaGamemodeInstance : NFMWorldLibrary_Backend_Gamemodes_BaseGamemodeInstance, NFMWorldLibrary_Backend_Gamemodes_IGamemodeInstance
---@field isClient boolean
---@field _path string
LuaGamemodeInstance = {}

---@class (exact) LuaGamemode : NFMWorldLibrary_Backend_Gamemodes_BaseGamemode

LuaGamemode = {}

---Creates a new LuaGamemode
---@return LuaGamemodeInstance
function LuaGamemode.new() end

---@param self LuaGamemodeInstance
---@return NFMWorldLibrary_Gamemodes_RaceResultsInstance|nil
function LuaGamemodeInstance:getResults() end

---@param self LuaGamemodeInstance
---@param playerStandings byteArrayInstance
function LuaGamemodeInstance:finishRace(playerStandings) end

---@param self LuaGamemodeInstance
---@param name string
---@param idx number
---@param x fixed64
---@param y fixed64
---@return NFMWorldLibrary_Backend_BackendCarInstance
function LuaGamemodeInstance:createBackendCar(name, idx, x, y) end

---@param self LuaGamemodeInstance
function LuaGamemodeInstance:reset() end

---@param self LuaGamemodeInstance
---@param callback fun()
function LuaGamemodeInstance:add_onEnter(callback) end

---@param self LuaGamemodeInstance
function LuaGamemodeInstance:remove_onEnter() end

---@param self LuaGamemodeInstance
---@param callback fun()
function LuaGamemodeInstance:add_onExit(callback) end

---@param self LuaGamemodeInstance
function LuaGamemodeInstance:remove_onExit() end

---@param self LuaGamemodeInstance
---@param callback fun()
function LuaGamemodeInstance:add_onGameTick(callback) end

---@param self LuaGamemodeInstance
function LuaGamemodeInstance:remove_onGameTick() end

---@param self LuaGamemodeInstance
---@param callback fun()
function LuaGamemodeInstance:add_onReset(callback) end

---@param self LuaGamemodeInstance
function LuaGamemodeInstance:remove_onReset() end
