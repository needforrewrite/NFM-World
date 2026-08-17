---@type GamemodeContext
GM = nil

---@param type string
---@param payload table
function OnServerEvent(type, payload) end

---@class RaceResults
---@field duration number
---@field gamemodeId string
---@field standings RaceStandings

---@param results RaceResults
function OnServerRaceFinished(results) end

---@param key integer
function OnKeyPressed(key) end

---@param key integer
function OnKeyReleased(key) end

---@param key string
function OnKeyTyped(key) end

---@type ServerGamemodeContext
SGM = nil