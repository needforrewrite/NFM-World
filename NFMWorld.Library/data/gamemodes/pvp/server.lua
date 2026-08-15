-- PvP racing gamemode (server). Validates checkpoint events with ordering
-- checks and tracks authoritative laps/standings.

local state = "waitingToStart" -- waitingToStart, countdown, inProgress, finished
local countdown = 3
local inner = 0
local perPlayer = {}          -- playerId -> { lap, checkpoint, finished, position }
local finishCounter = 0
local done = false

function on_begin()
    perPlayer = {}
    finishCounter = 0
    done = false
    state = "waitingToStart"

    for i = 0, server.playerCount - 1 do
        local id = server:playerId(i)
        perPlayer[id] = { lap = 0, checkpoint = -1, finished = false, position = -1 }
    end
end

function on_start_race()
    state = "countdown"
    countdown = 3
    inner = 0
end

function on_game_tick()
    if state == "countdown" then
        inner = inner - 1
        if inner <= 0 then
            inner = countdown_interval()
            countdown = countdown - 1
            if countdown <= 0 then
                state = "inProgress"
                broadcast_event("countdown_go", {})
            end
        end
    end
end

local function broadcast_standings()
    local standings = {}
    for i = 0, server.playerCount - 1 do
        local id = server:playerId(i)
        local s = perPlayer[id]
        standings[tostring(i)] = {
            position = s.finished and s.position or 0,
            finished = s.finished,
            lap = s.lap
        }
    end
    broadcast_event("standings", standings)
end

local function complete_race()
    local results = {}
    for i = 0, server.playerCount - 1 do
        local id = server:playerId(i)
        local s = perPlayer[id]
        table.insert(results, {
            playerId = id,
            position = s.finished and s.position or (finishCounter + 1),
            finished = s.finished
        })
    end

    -- C# global: builds the authoritative GameStateSnapshot.
    finish_race(results)
    done = true
end

function on_client_event(playerId, type, payload)
    if state ~= "inProgress" or type ~= "checkpoint" or payload == nil then
        return
    end

    local ps = perPlayer[playerId]
    if ps == nil then
        return
    end

    local cp = payload.index
    local lap = payload.lap
    local checkpointCount = stage.checkpointCount

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
        if ps.lap >= stage.nlaps and not ps.finished then
            ps.finished = true
            finishCounter = finishCounter + 1
            ps.position = finishCounter

            if finishCounter == 1 then
                state = "finished"
                complete_race()
            end
        end
    end

    if state == "inProgress" then
        broadcast_standings()
    end
end
