---@meta

-- ElStupido AI implementation
-- Handles AI decision making, path finding, and control inputs based on difficulty and race conditions.

-- Static helper function: Pythagorean distance squared (fix64 version)
-- Used for fast distance comparisons without square root
---@generic T : fixed64 | number
---@param x1 T
---@param x2 T
---@param z1 T
---@param z2 T
---@return T
local function pyo(x1, x2, z1, z2)
    local dx = x1 - x2
    local dz = z1 - z2
    return (dx * dx) + (dz * dz)
end

-- Calculate angle difference, normalized to -180 to 180 range
local function angleDiff(a, b)
    local diff = a - b
    diff = ((diff + fixed64(180)) % fixed64(360)) - fixed64(180)
    if diff < fixed64(-180) then
        diff = diff + fixed64(360)
    end
    return diff
end

---Compare two values
---@param a number
---@param b number
---@return number
function compare(a, b)
    if a < b then
        return -1
    elseif a > b then
        return 1
    else
        return 0
    end
end

-- Initialize state
local pan = fixed64(0)
local difficulty = fixed64(1)  -- 0.0 (easy) to 1.0 (hard)

-- Sequence tracking: {startNode, endNode, currentNode, traversingBackwards}
local sequence = nil

local targetFixRoadStartNode = nil
local bouncing = false
local _targetNode = 0

-- Obstacle avoidance state
local _stuckCounter = 0
local _avoidanceAngle = fixed64(0)
local _avoidanceTimer = 0
local smallturn = false

---Estimates clearance in a given direction by finding the nearest node.
---@param car BackendCar
---@param targetX fixed64
---@param targetZ fixed64
---@param stage BackendStage
---@return fixed64
local function getClearanceInDirection(car, targetX, targetZ, stage)
    local minDistSq = f64math.maxValue

    for i = 1, #stage.nodes do
        local node = stage.nodes[i]
        local distSq = pyo(targetX, node.position.x, targetZ, node.position.z)
        if distSq < minDistSq then
            minDistSq = distSq
        end
    end

    return f64math.sqrt(minDistSq)
end

---Detects when the car is stuck against a wall and applies avoidance steering.
---@param car BackendCar
---@param mad CarPhysics
---@param stage BackendStage
local function detectAndAvoidObstacles(car, mad, stage)
    -- Decrease avoidance timer
    if _avoidanceTimer > 0 then
        _avoidanceTimer = _avoidanceTimer - 1
        -- Override pan with avoidance angle while timer is active
        pan = _avoidanceAngle
        FrameTrace.addMessage("Avoiding obstacle, timer: " .. tostring(_avoidanceTimer))
        return
    end

    -- Check if car is stuck (low speed despite wanting to go forward)
    local isThrottling = car.control.up
    local isStuck = isThrottling and mad.speed - fixed64(20) < 0

    if isStuck then
        _stuckCounter = _stuckCounter + 1

        -- If stuck for multiple frames, initiate avoidance
        if _stuckCounter > 10 then  -- Stuck for ~0.16 seconds at 60fps
            FrameTrace.addMessage("Car stuck! Speed: " .. tostring(mad.speed) .. ", initiating avoidance")

            -- Check which direction to turn by sampling points to the left and right
            local currentHeading = car.rotation.yaw.deg
            local leftAngle = currentHeading - fixed64(90)
            local rightAngle = currentHeading + fixed64(90)

            -- Sample points at 45 degrees left and right
            local sampleDistance = fixed64(500)

            local leftAngleRad = leftAngle * f64math.pi / fixed64(180)
            local rightAngleRad = rightAngle * f64math.pi / fixed64(180)

            local leftX = car.position.x + f64math.sin(leftAngleRad) * sampleDistance
            local leftZ = car.position.z + f64math.cos(leftAngleRad) * sampleDistance

            local rightX = car.position.x + f64math.sin(rightAngleRad) * sampleDistance
            local rightZ = car.position.z + f64math.cos(rightAngleRad) * sampleDistance

            -- Check if there are walls in those directions
            local leftClearance = getClearanceInDirection(car, leftX, leftZ, stage)
            local rightClearance = getClearanceInDirection(car, rightX, rightZ, stage)

            FrameTrace.addMessage("Left clearance: " .. tostring(leftClearance) .. ", Right: " .. tostring(rightClearance))

            -- Turn toward the more open direction
            if leftClearance >= rightClearance then
                _avoidanceAngle = leftAngle
            else
                _avoidanceAngle = rightAngle
            end

            -- Set avoidance timer (about 1 second)
            _avoidanceTimer = 60
            _stuckCounter = 0
        end
    else
        -- Reset stuck counter if moving normally
        _stuckCounter = 0
    end
end

---Calculates target angle to drive toward a position
---@param car BackendCar
---@param position fixed64vector3
local function target(car, position)
    -- Calculate direction vector
    local dx = position.x - car.position.x
    local dz = position.z - car.position.z

    -- Calculate angle in degrees using atan2
    local angleRad = f64math.atan2(dx, dz)
    local angleDeg = angleRad * (fixed64(180) / f64math.pi)
    pan = -angleDeg
end

---Finds the target node to drive toward
---@param car BackendCar
---@param rubberbandingFactor fixed64
---@param mad CarPhysics
---@param random DeterministicRandom
local function findDrivingTarget(car, rubberbandingFactor, mad, random)
    local numNodes = #AI.stage.nodes
    local targetNodeIndex = _targetNode

    -- Ensure we're targeting at least the next checkpoint
    if targetNodeIndex < car.lastCheckpointNode + 1 then
        targetNodeIndex = car.lastCheckpointNode + 1
        if targetNodeIndex >= numNodes then
            targetNodeIndex = 0
        end
    end

    -- Find final checkpoint node
    local numCheckpoints = #AI.stage.checkpoints
    local finalCheckpoint = AI.stage.checkpoints[numCheckpoints - 1]
    local finalCheckpointNodeIndex = 0
    for i = 1, numNodes do
        if AI.stage.nodes[i] == finalCheckpoint then
            finalCheckpointNodeIndex = i
            break
        end
    end

    -- Skip fix hoop nodes after final checkpoint
    if targetNodeIndex > finalCheckpointNodeIndex then
        targetNodeIndex = 0
    end

    -- Special case: new lap starting
    if targetNodeIndex == finalCheckpointNodeIndex and car.lastCheckpointNode == -1 then
        targetNodeIndex = 0
    end

    -- Check if we're close to any node ahead (natural skip-ahead for ramps/shortcuts)
    local nextCheckpointIndex = car.currentCheckpoint + 1
    local nextCheckpoint = AI.stage.checkpoints[nextCheckpointIndex]
    local nextCheckpointNodeIndex = 0
    for i = 0, numNodes - 1 do
        if AI.stage.nodes[i] == nextCheckpoint then
            nextCheckpointNodeIndex = i
            break
        end
    end

    for i = targetNodeIndex + 1, nextCheckpointNodeIndex do
        local nodeIndex = i
        if nodeIndex >= numNodes then
            nodeIndex = nodeIndex - numNodes
        end

        local node = AI.stage.nodes[nodeIndex]
        local distanceToNodeSq = pyo(car.position.x, node.position.x, car.position.z, node.position.z)
        local speedSq = mad.speed * mad.speed
        local threshold = fixed64(200) * speedSq

        if distanceToNodeSq < threshold then
            _targetNode = nodeIndex
            targetNodeIndex = nodeIndex
            print("Advanced _targetNode to " .. nodeIndex .. " (visited ahead of current target)")
            break
        end
    end

    -- Skip non-drivable nodes and nodes we're close to
    local targetNode = AI.stage.nodes[targetNodeIndex]
    if targetNode.nodeKind ~= AiNodeKind.checkPoint then
        while true do
            targetNode = AI.stage.nodes[targetNodeIndex]
            local kind = targetNode.nodeKind

            -- Check if it's a non-drivable node type
            if kind ~= AiNodeKind.road and kind ~= AiNodeKind.checkPoint and
            kind ~= AiNodeKind.ramp and kind ~= AiNodeKind.halfpipe and
            kind ~= AiNodeKind.auto then
                targetNodeIndex = targetNodeIndex + 1
                if targetNodeIndex >= numNodes then
                    targetNodeIndex = 0
                end
            else
                local distanceToTargetSq = pyo(car.position.x, targetNode.position.x,
                                            car.position.z, targetNode.position.z)
                local speedSq = mad.speed * mad.speed
                local threshold = fixed64(100) * speedSq

                if distanceToTargetSq < threshold then
                    targetNodeIndex = targetNodeIndex + 1
                    if targetNodeIndex >= numNodes then
                        targetNodeIndex = 0
                    end
                else
                    break
                end
            end
        end
    end

    _targetNode = targetNodeIndex

    -- Handle sequences and node skipping
    if not sequence then
        -- Skip nodes based on difficulty and rubberbanding
        local skipFactor = difficulty * fixed64(3) * (fixed64(1) - rubberbandingFactor)
        local nodesToSkip = tonumber(f64math.floor(skipFactor))

        for i = 1, nodesToSkip do
            local kind = AI.stage.nodes[targetNodeIndex].nodeKind
            if kind == AiNodeKind.auto or kind == AiNodeKind.road or
            kind == AiNodeKind.ramp or kind == AiNodeKind.halfpipe then
                -- Don't skip ramps when low on power
                if mad.power < fixed64(80) and (kind == AiNodeKind.ramp or kind == AiNodeKind.halfpipe) then
                    break
                end

                targetNodeIndex = targetNodeIndex + 1
                if targetNodeIndex >= numNodes then
                    targetNodeIndex = 0
                end
            end
        end

        -- Check for sequence start
        if AI.stage.nodes[targetNodeIndex].nodeKind == AiNodeKind.sequenceStart then
            for i = targetNodeIndex + 1, numNodes - 1 do
                if AI.stage.nodes[i].nodeKind == AiNodeKind.sequenceEnd then
                    sequence = {
                        startNode = targetNodeIndex,
                        endNode = i,
                        currentNode = targetNodeIndex,
                        traversingBackwards = false
                    }
                    break
                end
            end
        end

        -- Check if we should look for a fix road
        local maxmagThreshold = mad.stat.maxmag * fixed64(0.8)
        local wantFix = mad.hitmag > maxmagThreshold and random:nextf64() < rubberbandingFactor

        if wantFix then
            -- Find all fix road nodes
            local fixRoadNodes = {}
            for i = 0, numNodes - 1 do
                local kind = AI.stage.nodes[i].nodeKind
                if kind == AiNodeKind.fixRoadStart or kind == AiNodeKind.fixRoadEnd then
                    table.insert(fixRoadNodes, i)
                end
            end

            if #fixRoadNodes > 0 then
                local selectedIndex = random:nextBetween(0, #fixRoadNodes - 1)
                targetFixRoadStartNode = fixRoadNodes[selectedIndex + 1]
                targetNodeIndex = targetFixRoadStartNode

                local kind = AI.stage.nodes[targetNodeIndex].nodeKind
                if kind == AiNodeKind.fixRoadStart then
                    -- Find corresponding FixRoadEnd
                    for i = targetNodeIndex + 1, numNodes - 1 do
                        if AI.stage.nodes[i].nodeKind == AiNodeKind.fixRoadEnd then
                            sequence = {
                                startNode = targetNodeIndex,
                                endNode = i,
                                currentNode = targetNodeIndex,
                                traversingBackwards = false
                            }
                            break
                        end
                    end
                elseif kind == AiNodeKind.fixRoadEnd then
                    -- Find corresponding FixRoadStart (traverse backwards)
                    for i = targetNodeIndex - 1, 0, -1 do
                        if AI.stage.nodes[i].nodeKind == AiNodeKind.fixRoadStart then
                            sequence = {
                                startNode = i,
                                endNode = targetNodeIndex,
                                currentNode = i,
                                traversingBackwards = true
                            }
                            break
                        end
                    end
                end
            end
        end
    else
        -- We're in a sequence, handle sequence traversal
        local seq = sequence

        if targetNodeIndex < seq.startNode or targetNodeIndex > seq.endNode then
            -- Outside of sequence, drive back to start
            targetNodeIndex = seq.startNode
        elseif targetNodeIndex == seq.currentNode then
            if not seq.traversingBackwards then
                -- Move to next node in sequence
                local nextNodeIndex = targetNodeIndex + 1
                if nextNodeIndex > seq.endNode then
                    -- End of sequence reached
                    sequence = nil
                else
                    sequence.currentNode = nextNodeIndex
                    targetNodeIndex = nextNodeIndex
                end
            else
                -- Move to previous node in sequence
                local prevNodeIndex = targetNodeIndex - 1
                if prevNodeIndex < seq.startNode then
                    -- Start of sequence reached
                    sequence = nil
                else
                    sequence.currentNode = prevNodeIndex
                    targetNodeIndex = prevNodeIndex
                end
            end
        else
            -- Drive back to start of sequence
            targetNodeIndex = seq.startNode
        end
    end

    local finalTargetNode = AI.stage.nodes[targetNodeIndex]
    FrameTrace.addMessage("Targeting node index: " .. targetNodeIndex .. ", kind: " .. tostring(finalTargetNode.nodeKind))
    FrameTrace.addMessage("Actual node target: " .. _targetNode)

    target(car, finalTargetNode.position)
end

---Applies steering, throttle, and brake controls
---@param car BackendCar
---@param mad CarPhysics
---@param u Control
local function steer(car, mad, u)
    -- Reset input controls
    u.up = false
    u.down = false
    u.left = false
    u.right = false
    u.handb = false

    local myxz = car.rotation.yaw.deg
    if u.zyinv then
        myxz = myxz + fixed64(180)  -- Adjust if car is inverted
    end

    -- Steering control logic
    local diff = angleDiff(myxz, pan)
    FrameTrace.addMessage("Angle diff: " .. diff:toString())

    local five = fixed64(5)
    local negFive = fixed64(-5)
    local one = fixed64(1)
    local negOne = fixed64(-1)

    if diff > five then
        u.right = true
    elseif diff < negFive then
        u.left = true
    else
        if diff > one then
            u.right = smallturn
            smallturn = not smallturn
        elseif diff < negOne then
            u.left = smallturn
            smallturn = not smallturn
        end
    end

    -- Throttle and brake control logic
    if mad.speed > mad.stat.swits.x then
        local absDiff = f64math.abs(diff)
        local fifty = fixed64(50)
        local onetwenty = fixed64(120)

        if absDiff < fifty then
            u.up = true
        elseif absDiff < onetwenty then
            u.down = true
        else
            u.handb = true
        end
    else
        u.up = true
    end
end

---Main AI update function. Called every frame to compute control inputs for the AI vehicle.
function RunAi()
    local car = AI.player.car
    if car == nil then
        return
    end

    local u = car.control
    local position = car.placement
    local mad = car.carPhysics

    -- Initialize random number generator with deterministic seed based on car position
    local seedValue = car.position.x.raw
    local random = DeterministicRandom.new(seedValue)

    -- Calculate rubberbanding factor
    -- 1.0 = last place, 0.0 = first place
    local numCars = #AI.players
    local rubberbandingFactor = fixed64(position) / fixed64(numCars - 1)

    if car.wasted then return end

    local grounded
    if bouncing then
        grounded = mad.wtouch  -- Use wheel touch when bounce enabled
    else
        grounded = mad.mtouch  -- Use main/body touch otherwise
    end

    findDrivingTarget(car, rubberbandingFactor, mad, random)

    if grounded then
        -- Check if we're stuck against a wall
        detectAndAvoidObstacles(car, mad, AI.stage)

        steer(car, mad, u)
    end
end
