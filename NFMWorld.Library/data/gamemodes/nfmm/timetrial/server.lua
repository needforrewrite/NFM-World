--- TimeTrial server just waits for the client to finish and then ends the race. It does nothing else.

function OnStartRace()
    SGM:broadcastEvent("started", { serverTime = 0 })
end

---@param playerId string
---@param type string
---@param payload table
function OnClientEvent(playerId, type, payload)
    if type == 'finished' then
        SGM:finishRace({
            {
                playerId = SGM.players[1].id,
                position = 0,
                finished = true
            }
        })
    end
end
