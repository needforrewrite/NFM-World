-- Time trial gamemode (client-only).
-- State machine: notStarted -> countdown -> inProgress -> finished.

---@type { simulation?: boolean }
local config = GM.config

-- Headless replay-validation mode (set by the TT simulator). Skips the
-- countdown and disables recording/ghost/save -- inputs are applied externally.
local simulation = config ~= nil and config.simulation

local state = "notStarted"
local countdown = 3
local inner = 0
local tick = 0
local written = false
local timer = Stopwatch.new()

---@type TimeTrial?
local timeTrial
if not simulation then
    timeTrial = TimeTrial.new(GM.stage)
end

-- Split/diff tracking (ported from TimeTrialClientGamemode).
-- Stored in seconds (the unit LuaTimeTrial helpers return); HudStateData's
-- *DiffMs fields are written in milliseconds.
local lastCheckpoint = 0
local lastLap = 0
local recordedSplitCount = 0
local lastCheckpointSplitDiff = 0   -- cumulative diff at the previous checkpoint
local lastLapSplitDiff = 0          -- diff of the last completed lap

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

-- Mirrors TimeTrialClientGamemode.RenderInfo: refreshes the checkpoint and
-- lap diff HUD fields each tick.
local function renderInfo()
    local car = GM.players[1].car

    -- Checkpoint diff (chkDiffMs / lastChkDiffMs).
    if car ~= nil and (car.currentCheckpoint ~= 0 or car.currentLap ~= 0)
        and timeTrial ~= nil and timeTrial.hasGhost and recordedSplitCount > 0 then
        local diff = timeTrial:getLastSplitDiff() -- seconds
        if diff ~= nil then
            diff = diff
            GM.hudState.chkDiffMs = math.floor(diff * 1000)
            GM.hudState.lastChkDiffMs = math.floor((diff - lastCheckpointSplitDiff) * 1000)
        end
    else
        GM.hudState.chkDiffMs = 0
        GM.hudState.lastChkDiffMs = 0
    end

    -- Lap diff (lapDiffMs / lastLapDiffMs).
    if timeTrial ~= nil and timeTrial.hasGhost and car ~= nil and car.currentLap > 0 then
        local lapDiff = timeTrial:getLapDiff(car.currentLap) -- seconds
        if lapDiff ~= nil then
            GM.hudState.lapDiffMs = math.floor(lapDiff * 1000)
        end
    else
        GM.hudState.lapDiffMs = 0
    end
    GM.hudState.lastLapDiffMs = math.floor(lastLapSplitDiff * 1000)
end

-- Mirrors TimeTrialClientGamemode.RenderFinishedText.
local function timeParts(totalMs)
    local minutes = math.floor(totalMs / 60000)
    local seconds = math.floor((totalMs % 60000) / 1000)
    local millis = math.floor(totalMs % 1000)
    return minutes, seconds, millis
end

local function formatElapsed(totalMs)
    local minutes, seconds, millis = timeParts(totalMs)
    return string.format("%02d:%02d.%03d", minutes, seconds, millis)
end

local function formatBest(totalMs)
    local minutes, seconds, millis = timeParts(totalMs)
    return string.format("%02d:%02d:%02d", minutes, seconds, millis)
end

local function renderFinishedText()
    local text = "Finished! Time: " .. formatElapsed(math.floor(timer.elapsed * 1000))

    local newBest = false
    if timeTrial == nil or not timeTrial.hasGhost then
        newBest = true
    elseif recordedSplitCount > 0 then
        local diff = timeTrial:getLastSplitDiff() -- seconds
        newBest = diff ~= nil and diff < 0
    end

    if newBest then
        text = text .. "\nNew best time!"
    end

    if (timeTrial ~= nil and timeTrial.hasGhost) or newBest then
        local currentLastSplit = timeTrial ~= nil and timeTrial:getLastSplitTime() or nil  -- seconds or nil
        local bestLastSplit = timeTrial ~= nil and timeTrial:getBestLastSplitTime() or nil -- seconds or nil

        local bestTimeMs
        if currentLastSplit ~= nil and bestLastSplit ~= nil then
            bestTimeMs = math.min(currentLastSplit, bestLastSplit) * 1000
        elseif currentLastSplit ~= nil then
            bestTimeMs = currentLastSplit * 1000
        elseif bestLastSplit ~= nil then
            bestTimeMs = bestLastSplit * 1000
        end

        if bestTimeMs ~= nil then
            text = text .. "\nBest time: " .. formatBest(bestTimeMs)
        end
    end

    text = text .. "\nPress R to restart"
    GM.hudState.stateText = text
end

function OnBegin()
    state = "notStarted"
    GM.hudState.lap = 1
    GM.hudState.totalLaps = GM.stage.nlaps
    GM.hudState.position = 1
    GM.hudState.totalRacers = 1
    GM.hudState.stateText = nil
end

function OnReset()
    tick = 0
    written = false
    timer:reset()

    if simulation then
        state = "inProgress"
        timer:start()
    else
        state = "countdown"
        countdown = 3
        inner = 0
    end

    lastCheckpoint = 0
    lastLap = 0
    recordedSplitCount = 0
    lastCheckpointSplitDiff = 0
    lastLapSplitDiff = 0

    GM:removeFakePlayers()
    setupPlayer()

    if timeTrial ~= nil and timeTrial.hasGhost then
        local ghost = GM:clonePlayer(GM.players[1])
        GM.client:getClientCarCallbacks(ghost.car).alphaOverride = 0.5
        ghost.car.currentLap = 0
    end
end

function OnGameTick()
    if state == "notStarted" then
        OnReset()
        return
    end

    if state == "countdown" then
        inner = inner - 1
        if inner <= 0 then
            inner = GM.countdownInterval
            countdown = countdown - 1
            GM.hudState.countdownTimer = countdown
            if countdown <= 0 then
                state = "inProgress"
                timer:start()
            end
        end
        renderInfo()
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

            -- On checkpoint change: record the split and update lap diff tracking
            -- (TimeTrialClientGamemode.OnAfterPhysics).
            if car.currentCheckpoint ~= lastCheckpoint then
                if timeTrial ~= nil and timeTrial.hasGhost and recordedSplitCount > 0 then
                    local diff = timeTrial:getLastSplitDiff() -- seconds
                    if diff ~= nil then
                        lastCheckpointSplitDiff = diff
                    end
                end

                local currentLapSplitDiff = 0
                if timeTrial ~= nil and timeTrial.hasGhost and lastLap > 0 then
                    local lapDiff = timeTrial:getLapDiff(lastLap) -- seconds
                    if lapDiff ~= nil then
                        currentLapSplitDiff = lapDiff
                    end
                end

                if timeTrial ~= nil then
                    timeTrial:recordSplit(timer.elapsed) -- seconds
                end
                recordedSplitCount = recordedSplitCount + 1

                if lastLap ~= car.currentLap then
                    lastLapSplitDiff = currentLapSplitDiff
                end
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
        renderInfo()
        return
    end

    if state == "finished" then
        GM.players[1].car:drive(GM.stage)
        if not written then
            written = true
            if timeTrial ~= nil then
                timeTrial:save()
            end
            renderFinishedText()
        end
        renderInfo()
    end
end

---@param key integer
function OnKeyPressed(key)
    if key == Key.R then
        OnReset()
    end
end
