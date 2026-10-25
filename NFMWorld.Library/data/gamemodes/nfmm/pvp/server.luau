-- PvP racing gamemode (server). Validates checkpoint events with ordering
-- checks and tracks authoritative laps/standings.

local state = "waitingToStart" -- waitingToStart, countdown, inProgress, finished
local countdown = 3
local inner = SGM.countdownInterval
local perPlayer: { [string] : { lap: number, checkpoint: number, finished: boolean, position: number } } = {} -- playerId -> { lap, checkpoint, finished, position }
local finishCounter = 0

local function OnBegin()
    perPlayer = {}
    finishCounter = 0
    done = false
    state = "waitingToStart"

    for i = 1, #SGM.players do
        local id = SGM.players[i].id
        perPlayer[id] = { lap = 0, checkpoint = -1, finished = false, position = -1 }
    end
end

local function OnStartRace()
    state = "countdown"
    countdown = 3
    inner = SGM.countdownInterval
end

local function OnGameTick()
    if state == "countdown" then
        inner = inner - 1
        print(tostring(inner))
        if inner <= 0 then
            inner = SGM.countdownInterval
            countdown = countdown - 1
            if countdown <= 0 then
                state = "inProgress"
                SGM:broadcastEvent("countdown_go", {})
            end
        end
    end
end

local function completeRace()
    local results = {}
    for i = 1, #SGM.players do
        local id = SGM.players[i].id
        local s = perPlayer[id]
        table.insert(results, {
            playerId = id,
            position = s.finished and s.position or (finishCounter + 1),
            finished = s.finished
        })
    end

    SGM:finishRace(results)
    done = true
end

---@param playerId string
---@param type string
---@param payload table
local function OnClientEvent(playerId, type, payload)
    if state ~= "inProgress" or type ~= "checkpoint" or payload == nil then
        return
    end

    local ps = perPlayer[playerId]
    if ps == nil then
        return
    end

    local cp = payload.index
    local lap = payload.lap
    local checkpointCount = #SGM.currentStage.checkpoints

    -- Lap must be current or next.
    if lap ~= ps.lap and lap ~= ps.lap + 1 then
        return
    end

    -- Checkpoint must be the next expected one.
    local expected = (ps.checkpoint + 1) % checkpointCount
    if cp ~= expected then
        return
    end

    ps.checkpoint = cp

    if cp == 0 and lap == ps.lap then
        ps.lap = ps.lap + 1
        if ps.lap >= SGM.currentStage.nlaps and not ps.finished then
            ps.finished = true
            finishCounter = finishCounter + 1
            ps.position = finishCounter

            if finishCounter == 1 then
                state = "finished"
                completeRace()
            end
        end
    end
end

return {
    OnBegin = OnBegin,
    OnStartRace = OnStartRace,
    OnGameTick = OnGameTick,
    OnClientEvent = OnClientEvent,
}