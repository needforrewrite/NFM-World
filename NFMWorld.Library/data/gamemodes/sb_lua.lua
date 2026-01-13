local newTick = 0

GM:AddListener_OnEnter(function()
    newTick = 0

    for i = 1, #GM.players do
        local player = GM.players[i]
        GM.carsInRace[i] = GM:createBackendCar(player.carName, i, fix64.create(0), fix64.create(0))
        print("Created car for player " .. player.name .. " with car name " .. player.carName)
    end

    GM:reset()
end)

GM:AddListener_OnExit(function()
end)

GM:AddListener_OnGameTick(function()
    FrameTrace.AddMessage("Hello from Lua")
    FrameTrace.AddMessage("contox: " .. GM.carsInRace[1].position.x .. ", contoz: " .. GM.carsInRace[1].position.z .. ", contoy: " .. GM.carsInRace[1].position.y)

    -- TODO enums
    -- if GM.raceState == RaceState.InProgress then
    -- Inter-car collision is run at the original tickrate (21.4TPS) to emulate original physics behavior
    -- We round this up to 3 ticks per 63TPS tick.
    newTick = newTick + 1
    if newTick == 3 then
        for i = 1, #GM.carsInRace do
            for j = 1, #GM.carsInRace do
                if i ~= j then
                    GM.carsInRace[i]:collide(GM.carsInRace[j])
                end
            end
        end

        newTick = 0;
    end

    for i = 1, #GM.carsInRace do
        GM.carsInRace[i]:drive(GM.currentStage)
    end
end)

GM:AddListener_OnReset(function()

end)