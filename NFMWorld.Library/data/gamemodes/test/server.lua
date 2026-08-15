-- Test Lua server gamemode: echoes client events and finishes on demand.
-- Used to validate the Lua server gamemode framework wiring.

local finished = false

function on_begin()
    -- nothing to set up
end

function on_start_race()
    broadcast_event("started", { serverTime = 0 })
end

function on_game_tick()
    -- no-op: server gamemodes advance via client events
end

function on_client_event(playerId, type, payload)
    -- Echo the event back to everyone, tagged with the sender.
    broadcast_event("echo", { from = playerId, type = type, payload = payload })
end
