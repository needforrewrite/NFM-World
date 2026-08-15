-- PvP racing gamemode (client). config.constraint: "racing" | "wasting" | "both".
-- Client simulates locally, sends checkpoint events, receives authoritative standings.

local state = "countdown"      -- countdown, inProgress, finished
local countdown = 3
local inner = 0
local clientTick = 0
local lastCheckpoint = -1
local lastLap = 0

local function setup_cars()
    for i = 0, players.count - 1 do
        local car = create_car(i, -500 + (400 * i), 0)
        car.currentCheckpoint = 0
        car.currentLap = 0

        local player = players:get(i)
        if player ~= nil and player.parameters.isBot then
            attach_bot(i)
        end
    end
end

function on_begin()
    reset_client_state()
    setup_cars()
    state = "countdown"
    countdown = 3
    inner = 0
    clientTick = 0
    lastCheckpoint = -1
    lastLap = 0
end

function on_reset()
    on_begin()
end

function on_game_tick()
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
        physics_tick()

        for i = 0, players.count - 1 do
            handle_fix_hoops(i)
            handle_checkpoint(i)
        end

        calculate_positions()

        local myCar = players:get(client_index()).car
        if myCar ~= nil then
            -- Client-side finish fallback (server result is authoritative).
            if myCar.currentLap >= stage.nlaps then
                state = "finished"
            end

            if myCar.currentCheckpoint ~= lastCheckpoint or myCar.currentLap ~= lastLap then
                send_event("checkpoint", {
                    index = myCar.currentCheckpoint,
                    lap = myCar.currentLap,
                    tick = clientTick
                })
                lastCheckpoint = myCar.currentCheckpoint
                lastLap = myCar.currentLap
            end
        end

        clientTick = clientTick + 1
        update_hud(client_index())
        return
    end

    if state == "finished" then
        physics_tick()
    end
end

function on_server_event(type, payload)
    if type == "standings" and payload ~= nil then
        local me = payload[tostring(client_index())]
        if me ~= nil and me.position ~= nil then
            hud.position = me.position
            hud.totalRacers = players.count
        end
    end

    if type == "countdown_go" then
        state = "inProgress"
    end
end
