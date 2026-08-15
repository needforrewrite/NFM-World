-- Time trial gamemode (client-only).
-- State machine: notStarted -> countdown -> inProgress -> finished.

local state = "notStarted"
local countdown = 3
local inner = 0
local tick = 0
local written = false

local function setup_player()
    local car = create_car(0, 0, 0)
    car.currentCheckpoint = 0
    car.currentLap = 0
    hud.lap = 1
    hud.totalLaps = stage.nlaps
    reset_checkpoint_glow()
    time_trial:begin(car)
end

function on_begin()
    state = "notStarted"
    hud.lap = 1
    hud.totalLaps = stage.nlaps
    hud.position = 1
    hud.totalRacers = 1
end

function on_reset()
    state = "countdown"
    countdown = 3
    inner = 0
    tick = 0
    written = false

    remove_fake_players()
    setup_player()

    if time_trial:hasGhost() then
        local ghostIndex = add_ghost_player()
        players:get(ghostIndex).car.currentLap = 0
    end
end

function on_game_tick()
    if state == "notStarted" then
        on_reset()
        return
    end

    if state == "countdown" then
        inner = inner - 1
        if inner <= 0 then
            inner = countdown_interval()
            countdown = countdown - 1
            hud.countdownTimer = countdown
            if countdown <= 0 then
                state = "inProgress"
            end
        end
        return
    end

    if state == "inProgress" then
        drive(0)
        handle_fix_hoops(0)
        handle_checkpoint(0)

        local car = players[0].car
        if car ~= nil then
            update_hud(0)
            hud.lap = car.currentLap + 1
            update_checkpoint_glow(car.currentCheckpoint,
                car.currentCheckpoint == stage.checkpointCount - 1 and car.currentLap == stage.nlaps - 1)

            time_trial:record(car)

            if time_trial:hasGhost() then
                local ghost = players:get(1)
                if ghost ~= nil and ghost.car ~= nil then
                    time_trial:applyGhost(ghost.car, tick)
                    drive(1)
                end
            end

            if car.currentLap >= stage.nlaps then
                state = "finished"
                car.carPhysics.halted = true
            end
        end

        tick = tick + 1
        return
    end

    if state == "finished" then
        drive(0)
        if not written then
            written = true
            time_trial:save()
            hud.stateText = "Finished!"
        end
    end
end

function on_key_pressed(key)
    -- Escape etc. handled by the race phase.
end
