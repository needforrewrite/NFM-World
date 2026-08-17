-- Test Lua gamemode: countdown + physics + checkpoint flow.
-- Used to validate the Lua gamemode framework wiring.

local countdown = 3

function OnBegin()
    GM.hudState.totalLaps = GM.stage.nlaps
    GM.hudState.lap = 1
    GM.hudState.position = 1
    GM.hudState.totalRacers = #GM.players
end

function OnReset()
    countdown = 3
    local car = GM:createCar(0, fixed64(0), fixed64(0))
    car.currentCheckpoint = 0
    car.currentLap = 0
    GM.hudState.lap = 1
    GM.hudState.totalLaps = GM.stage.nlaps
end

function OnGameTick()
    if countdown > 0 then
        countdown = countdown - 1
        GM.hudState.countdownTimer = countdown
        return
    end

    GM.physics:gameTick()
    GM:handleFixHoops(GM.players[1].car)
    GM:handleCheckPoint(GM.players[1].car)
    GM:calculatePositions()

    local player = GM.players[1]
    if player ~= nil and player.car ~= nil then
        GM.hudState.lap = player.car.currentLap + 1
        GM.hudState.position = player.car.placement + 1
    end
end

function OnServerEvent(type, payload)
    -- no-op: client-only test gamemode
end
