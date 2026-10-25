--!strict
-- Time trial gamemode (client-only).
-- State machine: notStarted -> countdown -> inProgress -> finished.

local config: { simulation: boolean? } = GM.config

-- Headless replay-validation mode (set by the TT simulator). Skips the
-- countdown and disables recording/ghost/save -- inputs are applied externally.
local simulation = config ~= nil and config.simulation

local state = "notStarted"
local countdown = 3
local inner = GM.countdownInterval
local tick = 0
local written = false
local timer = Stopwatch.new()

local timeTrial: TimeTrial?
if not simulation then
    timeTrial = TimeTrial.new(GM.stage)
end

local lastCheckpoint = 0
local lastLap = 0

local function setupPlayer()
    local car = GM:createCar(1, fixed64(0), fixed64(0))
    car.currentCheckpoint = 0
    car.currentLap = 0
    GM.hudState.lap = 1
    GM.hudState.totalLaps = GM.stage.nlaps
    GM.client:resetCheckpointGlow()
    if timeTrial ~= nil then
        timeTrial:begin(car)
    end
end

-- Mirrors TimeTrialClientGamemode.RenderFinishedText.
local function timeParts(totalMs: number)
    local minutes = math.floor(totalMs / 60000)
    local seconds = math.floor((totalMs % 60000) / 1000)
    local millis = math.floor(totalMs % 1000)
    return minutes, seconds, millis
end

local function formatElapsed(totalMs: number)
    local minutes, seconds, millis = timeParts(totalMs)
    return string.format("%02d:%02d.%03d", minutes, seconds, millis)
end

local function formatBest(totalMs: number)
    local minutes, seconds, millis = timeParts(totalMs)
    return string.format("%02d:%02d:%02d", minutes, seconds, millis)
end

local function renderFinishedText()
    local text = "Finished! Time: " .. formatElapsed(math.floor(timer.elapsed * 1000))

    text = text .. "\nPress R to restart"
    GM.hudState.stateText = text
end

local function OnBegin()
    state = "notStarted"
    GM.hudState.lap = 1
    GM.hudState.totalLaps = GM.stage.nlaps
    GM.hudState.position = 1
    GM.hudState.totalRacers = 1
    GM.hudState.stateText = ''
end

local function OnReset()
    tick = 0
    written = false
    timer:reset()

    if simulation then
        state = "inProgress"
        timer:start()
    else
        state = "countdown"
        countdown = 3
        inner = GM.countdownInterval
    end

    lastCheckpoint = 0
    lastLap = 0

    GM:removeFakePlayers()
    setupPlayer()

    if timeTrial ~= nil and timeTrial.hasGhost then
        local ghost = GM:clonePlayer(GM.players[1])
        if ghost.car ~= nil then
            GM.client:getClientCarCallbacks(ghost.car).alphaOverride = 0.5
            ghost.car.currentLap = 0
        end
    end
end

local function OnGameTick()
    if state == "notStarted" then
        OnReset()
        return
    end

    if state == "countdown" then
        inner = inner - 1
        GM.hudState.countdownTimer = countdown

        if inner <= 0 then
            inner = GM.countdownInterval
            countdown = countdown - 1
            if countdown <= 0 then
                GM.hudState.countdownTimer = 0
                state = "inProgress"
                timer:start()
            end
        end
        return
    end

    if state == "inProgress" then
        local car = GM.players[1].car
        if car ~= nil then
            -- Snapshot before checkpoint handling (TimeTrialGamemode.OnBeforePhysics).
            lastCheckpoint = car.currentCheckpoint
            lastLap = car.currentLap

            GM:handleFixHoops(car)
            GM:handleCheckPoint(car)
            car:drive(GM.stage)

            GM:updateHudAndSounds(car)
            GM.hudState.lap = car.currentLap + 1
            GM.hudState.lapTime = math.floor(timer.elapsed * 1000)
            GM.client:updateCheckpointGlow(car.currentCheckpoint, car.currentCheckpoint == #GM.stage.checkpoints - 1 and car.currentLap == GM.stage.nlaps - 1)

            if timeTrial ~= nil then
                timeTrial:record(car)
            end

            if timeTrial ~= nil and timeTrial.hasGhost then
                local ghost = GM.players[2]
                if ghost ~= nil and ghost.car ~= nil then
                    timeTrial:applyGhost(ghost.car, tick)
                    ghost.car:drive(GM.stage)
                end
            end

            if car.currentLap >= GM.stage.nlaps then
                state = "finished"
                car.carPhysics.halted = true
                GM:sendEvent('finished', {})
                timer:stop()
            end
        end

        tick = tick + 1
        return
    end

    if state == "finished" then
        local car = GM.players[1].car
        if car ~= nil then
            car:drive(GM.stage)
        end

        if not written then
            written = true
            if timeTrial ~= nil then
                timeTrial:save()
            end
            renderFinishedText()
        end
    end
end

local function OnKeyPressed(key: Key)
    if key == Key.r then
        OnReset()
    end
end

return {
    OnBegin = OnBegin,
    OnReset = OnReset,
    OnGameTick = OnGameTick,
    OnKeyPressed = OnKeyPressed,
}