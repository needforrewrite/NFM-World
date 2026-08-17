-- Test Lua server gamemode: echoes client events and finishes on demand.
-- Used to validate the Lua server gamemode framework wiring.

local finished = false

function OnBegin()
    -- nothing to set up
end

function OnStartRace()
    SGM:broadcastEvent("started", { serverTime = 0 })
end

function OnGameTick()
    -- no-op: server gamemodes advance via client events
end

function OnClientEvent(playerId, type, payload)
    -- Echo the event back to everyone, tagged with the sender.
    SGM:broadcastEvent("echo", { from = playerId, type = type, payload = payload })
end
