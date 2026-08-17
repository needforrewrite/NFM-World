-- Time trial gamemode (client-only).
-- State machine: notStarted -> countdown -> inProgress -> finished.

local state = "notStarted"
local countdown = 3
local inner = 0
local tick = 0
local written = false

local function setupPlayer()
    local car = GM:createCar(1, fixed64(0), fixed64(0))
    car.currentCheckpoint = 0
    car.currentLap = 0
    GM.hudState.lap = 1
    GM.hudState.totalLaps = GM.stage.nlaps
    GM.client:resetCheckpointGlow()
    GM.timeTrial:begin(car)
end

function OnBegin()
    state = "notStarted"
    GM.hudState.lap = 1
    GM.hudState.totalLaps = GM.stage.nlaps
    GM.hudState.position = 1
    GM.hudState.totalRacers = 1
end

function OnReset()
    state = "countdown"
    countdown = 3
    inner = 0
    tick = 0
    written = false

    GM:removeFakePlayers()
    setupPlayer()

    if GM.timeTrial.hasGhost then
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
            end
        end
        return
    end

    if state == "inProgress" then
        local car = GM.players[1].car
        if car ~= nil then
            GM:handleFixHoops(car)
            GM:handleCheckPoint(car)
            car:drive(GM.stage)

            GM:updateHudAndSounds(car)
            GM.hudState.lap = car.currentLap + 1
            GM.client:updateCheckpointGlow(car.currentCheckpoint,
            car.currentCheckpoint == #GM.stage.checkpoints - 1 and car.currentLap == GM.stage.nlaps - 1)

            GM.timeTrial:record(car)

            if GM.timeTrial.hasGhost then
                local ghost = GM.players[2]
                if ghost ~= nil and ghost.car ~= nil then
                    GM.timeTrial:applyGhost(ghost.car, tick)
                    ghost.car:drive(GM.stage)
                end
            end

            if car.currentLap >= GM.stage.nlaps then
                state = "finished"
                car.carPhysics.halted = true
                GM:sendEvent('finished', {})
            end
        end

        tick = tick + 1
        return
    end

    if state == "finished" then
        GM.players[1].car:drive(GM.stage)
        if not written then
            written = true
            GM.timeTrial:save()
            GM.hudState.stateText = "Finished!"
        end
    end
end

---@param key integer
function OnKeyPressed(key)
    if key == Key.R then
        OnReset()
    end
end
