-- Test Lua gamemode: countdown + physics + checkpoint flow.
-- Used to validate the Lua gamemode framework wiring.

local countdown = 3

function on_begin()
    hud.totalLaps = stage.nlaps
    hud.lap = 1
    hud.position = 1
    hud.totalRacers = players.count
end

function on_reset()
    countdown = 3
    local car = create_car(0, 0, 0)
    car.currentCheckpoint = 0
    car.currentLap = 0
    hud.lap = 1
    hud.totalLaps = stage.nlaps
end

function on_game_tick()
    if countdown > 0 then
        countdown = countdown - 1
        hud.countdownTimer = countdown
        return
    end

    physics_tick()
    handle_fix_hoops(0)
    handle_checkpoint(0)
    calculate_positions()

    local player = players[0]
    if player ~= nil and player.car ~= nil then
        hud.lap = player.car.currentLap + 1
        hud.position = player.car.placement + 1
    end
end

function on_server_event(type, payload)
    -- no-op: client-only test gamemode
end
