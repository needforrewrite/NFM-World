-- PvP racing gamemode (client). config.constraint: "racing" | "wasting" | "both".
-- Client simulates locally, sends checkpoint events, receives authoritative standings.

local state = "countdown"      -- countdown, inProgress, finished
local countdown = 3
local inner = 0
local clientTick = 0
local lastCheckpoint = -1
local lastLap = 0

---@type { constraint: "racing" | "wasting" | "both" }
local config = GM.config

local function setupCars()
    for i = 1, #GM.players do
        local car = GM:createCar(i, fixed64(-500 + (400 * i)), fixed64(0))
        car.currentCheckpoint = 0
        car.currentLap = 0

        local player = GM.players[i]
        if player ~= nil and player.info.isBot then
            -- TODO: attach bot AI
            -- attach_bot(i)
        end
    end
end

function OnBegin()
    GM:clientReset()
    setupCars()
    state = "countdown"
    countdown = 3
    inner = 0
    clientTick = 0
    lastCheckpoint = -1
    lastLap = 0
end

function OnReset()
    OnBegin()
end

function OnGameTick()
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
        GM.physics:gameTick()

        for i = 1, #GM.players do
            if GM.players[i] ~= nil and GM.players[i].car ~= nil then
                ---@type BackendCar
                local car = GM.players[i].car
                GM:handleFixHoops(car)
                GM:handleCheckPoint(car)
            end
        end

        GM:calculatePositions()

        local myCar = GM.clientPlayer.car
        if myCar ~= nil then
            -- Client-side finish fallback (server result is authoritative).
            if myCar.currentLap >= GM.stage.nlaps then
                state = "finished"
            end

            if myCar.currentCheckpoint ~= lastCheckpoint or myCar.currentLap ~= lastLap then
                GM:sendEvent("checkpoint", {
                    index = myCar.currentCheckpoint,
                    lap = myCar.currentLap,
                    tick = clientTick
                })
                lastCheckpoint = myCar.currentCheckpoint
                lastLap = myCar.currentLap
            end
        end

        clientTick = clientTick + 1

        if myCar ~= nil then
            GM:updateHudAndSounds(myCar)
        end
        return
    end

    if state == "finished" then
        GM.physics:gameTick()
    end
end

---@param type string
---@param payload table
function OnServerEvent(type, payload)
    if type == "standings" and payload ~= nil then
        local me = payload[tostring(GM.clientPlayer.index)]
        if me ~= nil and me.position ~= nil then
            GM.hudState.position = me.position
            GM.hudState.totalRacers = #GM.players
        end
    end

    if type == "countdown_go" then
        state = "inProgress"
    end
end
