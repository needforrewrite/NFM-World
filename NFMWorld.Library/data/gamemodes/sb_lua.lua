local newTick = 0

GM:add_OnEnter(function()
    newTick = 0

    print(GM.players.count)
    for i = 1, GM.players.count do
        local player = GM.players[i]
        GM.carsInRace[i] = GM:createBackendCar(player.carName, i, fix64.create(0), fix64.create(0))
        print("Created car for player " .. player.playerName .. " with car name " .. player.carName)
    end

    GM:reset()
end)

GM:add_OnExit(function()
end)

GM:add_OnGameTick(function()
    FrameTrace.addMessage("Hello from Lua")
    FrameTrace.addMessage("contox: " .. tostring(GM.carsInRace[1].position.x) .. ", contoz: " .. tostring(GM.carsInRace[1].position.z) .. ", contoy: " .. tostring(GM.carsInRace[1].position.y))

    -- TODO enums
    -- if GM.raceState == RaceState.InProgress then
    -- Inter-car collision is run at the original tickrate (21.4TPS) to emulate original physics behavior
    -- We round this up to 3 ticks per 63TPS tick.
    newTick = newTick + 1
    if newTick == 3 then
        for i = 1, GM.carsInRace.count do
            for j = 1, GM.carsInRace.count do
                if i ~= j then
                    GM.carsInRace[i]:collide(GM.carsInRace[j])
                end
            end
        end

        newTick = 0;
    end

    for i = 1, GM.carsInRace.count do
        GM.carsInRace[i]:drive(GM.currentStage)
    end
end)

GM:add_OnReset(function()

end)