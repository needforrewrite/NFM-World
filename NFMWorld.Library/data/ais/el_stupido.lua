---@meta

-- ElStupido AI implementation
-- Handles AI decision making, path finding, and control inputs based on difficulty and race conditions.

local ElStupido = {}
ElStupido.__index = ElStupido

-- Static helper function: Pythagorean distance squared (fix64 version)
-- Used for fast distance comparisons without square root
local function pyo(x1, x2, z1, z2)
    local dx = x1 - x2
    local dz = z1 - z2
    return (dx * dx) + (dz * dz)
end

-- Calculate angle difference, normalized to -180 to 180 range
local function angleDiff(a, b)
    local diff = a - b
    diff = ((diff + fix64.create(180)) % fix64.create(360)) - fix64.create(180)
    if diff:compareTo(fix64.create(-180)) < 0 then
        diff = diff + fix64.create(360)
    end
    return diff
end

---Creates a new ElStupido AI instance
---@param gamemode LuaGamemodeInstance
---@param racePhase IRaceValuesInstance
---@return table
function ElStupido.new(gamemode, racePhase)
    local self = setmetatable({}, ElStupido)

    self.gamemode = gamemode
    self.racePhase = racePhase

    -- Initialize state
    self.pan = fix64.zero
    self.difficulty = fix64.one  -- 0.0 (easy) to 1.0 (hard)

    -- Sequence tracking: {startNode, endNode, currentNode, traversingBackwards}
    self.sequence = nil

    self.targetFixRoadStartNode = nil
    self.bouncing = false
    self._targetNode = 0

    -- Obstacle avoidance state
    self._stuckCounter = 0
    self._avoidanceAngle = fix64.zero
    self._avoidanceTimer = 0
    self.smallturn = false

    return self
end

---Main AI update function. Called every frame to compute control inputs for the AI vehicle.
---@param self table
---@param car IInGameCarInstance
---@param currentCarIndex integer
function ElStupido:runAi(car, currentCarIndex)
    local u = car.control
    local position = car.placement
    local mad = car.mad

    -- Initialize random number generator with deterministic seed based on car position
    local conto = ContO.new(car)
    local seedValue = conto.x.value.m_rawValue
    local random = DeterministicRandom.new(seedValue)

    -- Calculate rubberbanding factor
    -- 1.0 = last place, 0.0 = first place
    local numCars = self.racePhase.carsInRace:count()
    local rubberbandingFactor = fix64.create(position) / fix64.create(numCars - 1)

    if car.wasted then return end

    local grounded
    if self.bouncing then
        grounded = mad.wtouch  -- Use wheel touch when bounce enabled
    else
        grounded = mad.mtouch  -- Use main/body touch otherwise
    end

    self:findDrivingTarget(car, rubberbandingFactor, mad, random)

    if grounded then
        -- Check if we're stuck against a wall
        self:detectAndAvoidObstacles(car, mad, self.racePhase.currentStage)

        self:steer(car, mad, u)
    end
end

---Detects when the car is stuck against a wall and applies avoidance steering.
---@param self table
---@param car IInGameCarInstance
---@param mad MadInstance
---@param stage BackendStageInstance
function ElStupido:detectAndAvoidObstacles(car, mad, stage)
    -- Decrease avoidance timer
    if self._avoidanceTimer > 0 then
        self._avoidanceTimer = self._avoidanceTimer - 1
        -- Override pan with avoidance angle while timer is active
        self.pan = self._avoidanceAngle
        FrameTrace.addMessage("Avoiding obstacle, timer: " .. tostring(self._avoidanceTimer))
        return
    end

    -- Check if car is stuck (low speed despite wanting to go forward)
    local isThrottling = car.control.up
    local isStuck = isThrottling and mad.speed:compareTo(fix64.create(20)) < 0

    if isStuck then
        self._stuckCounter = self._stuckCounter + 1

        -- If stuck for multiple frames, initiate avoidance
        if self._stuckCounter > 10 then  -- Stuck for ~0.16 seconds at 60fps
            FrameTrace.addMessage("Car stuck! Speed: " .. mad.speed:toString() .. ", initiating avoidance")

            -- Check which direction to turn by sampling points to the left and right
            local currentHeading = car.rotation.yaw.degrees
            local leftAngle = currentHeading - fix64.create(90)
            local rightAngle = currentHeading + fix64.create(90)

            -- Sample points at 45 degrees left and right
            local sampleDistance = fix64.create(500)

            local leftAngleRad = leftAngle * fix64.pi / fix64.create(180)
            local rightAngleRad = rightAngle * fix64.pi / fix64.create(180)

            local leftX = car.position.x + fix64.sin(leftAngleRad) * sampleDistance
            local leftZ = car.position.z + fix64.cos(leftAngleRad) * sampleDistance

            local rightX = car.position.x + fix64.sin(rightAngleRad) * sampleDistance
            local rightZ = car.position.z + fix64.cos(rightAngleRad) * sampleDistance

            -- Check if there are walls in those directions
            local leftClearance = self:getClearanceInDirection(car, leftX, leftZ, stage)
            local rightClearance = self:getClearanceInDirection(car, rightX, rightZ, stage)

            FrameTrace.addMessage("Left clearance: " .. leftClearance:toString() .. ", Right: " .. rightClearance:toString())

            -- Turn toward the more open direction
            if leftClearance:compareTo(rightClearance) > 0 then
                self._avoidanceAngle = leftAngle
            else
                self._avoidanceAngle = rightAngle
            end

            -- Set avoidance timer (about 1 second)
            self._avoidanceTimer = 60
            self._stuckCounter = 0
        end
    else
        -- Reset stuck counter if moving normally
        self._stuckCounter = 0
    end
end

---Estimates clearance in a given direction by finding the nearest node.
---@param self table
---@param car IInGameCarInstance
---@param targetX fix64Instance
---@param targetZ fix64Instance
---@param stage BackendStageInstance
---@return fix64Instance
function ElStupido:getClearanceInDirection(car, targetX, targetZ, stage)
    local minDistSq = fix64.maxValue

    for i = 0, stage.nodes:count() - 1 do
        local node = stage.nodes[i]
        local distSq = pyo(targetX, node.position.x, targetZ, node.position.z)
        if distSq:compareTo(minDistSq) < 0 then
            minDistSq = distSq
        end
    end

    return fix64.sqrt(minDistSq)
end

---Finds the target node to drive toward
---@param self table
---@param car IInGameCarInstance
---@param rubberbandingFactor fix64Instance
---@param mad MadInstance
---@param random DeterministicRandomInstance
function ElStupido:findDrivingTarget(car, rubberbandingFactor, mad, random)
    local numNodes = self.racePhase.currentStage.nodes:count()
    local targetNodeIndex = self._targetNode

    -- Ensure we're targeting at least the next checkpoint
    if targetNodeIndex < car.lastCheckpointNode + 1 then
        targetNodeIndex = car.lastCheckpointNode + 1
        if targetNodeIndex >= numNodes then
            targetNodeIndex = 0
        end
    end

    -- Find final checkpoint node
    local numCheckpoints = self.racePhase.currentStage.checkpoints:count()
    local finalCheckpoint = self.racePhase.currentStage.checkpoints[numCheckpoints - 1]
    local finalCheckpointNodeIndex = 0
    for i = 0, numNodes - 1 do
        if self.racePhase.currentStage.nodes[i] == finalCheckpoint then
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
    local nextCheckpointIndex = car.currentCheckpoint
    local nextCheckpoint = self.racePhase.currentStage.checkpoints[nextCheckpointIndex]
    local nextCheckpointNodeIndex = 0
    for i = 0, numNodes - 1 do
        if self.racePhase.currentStage.nodes[i] == nextCheckpoint then
            nextCheckpointNodeIndex = i
            break
        end
    end

    for i = targetNodeIndex + 1, nextCheckpointNodeIndex do
        local nodeIndex = i
        if nodeIndex >= numNodes then
            nodeIndex = nodeIndex - numNodes
        end

        local node = self.racePhase.currentStage.nodes[nodeIndex]
        local distanceToNodeSq = pyo(car.position.x, node.position.x, car.position.z, node.position.z)
        local speedSq = mad.speed * mad.speed
        local threshold = fix64.create(200) * speedSq

        if distanceToNodeSq:compareTo(threshold) < 0 then
            self._targetNode = nodeIndex
            targetNodeIndex = nodeIndex
            print("Advanced _targetNode to " .. nodeIndex .. " (visited ahead of current target)")
            break
        end
    end

    -- Skip non-drivable nodes and nodes we're close to
    local targetNode = self.racePhase.currentStage.nodes[targetNodeIndex]
    if targetNode.kind ~= AiNodeKind.checkPoint then
        while true do
            targetNode = self.racePhase.currentStage.nodes[targetNodeIndex]
            local kind = targetNode.kind

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
                local threshold = fix64.create(100) * speedSq

                if distanceToTargetSq:compareTo(threshold) < 0 then
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

    self._targetNode = targetNodeIndex

    -- Handle sequences and node skipping
    if not self.sequence then
        -- Skip nodes based on difficulty and rubberbanding
        local skipFactor = self.difficulty * fix64.create(3) * (fix64.one - rubberbandingFactor)
        local nodesToSkip = fix64.floorToInt(skipFactor)

        for i = 1, nodesToSkip do
            local kind = self.racePhase.currentStage.nodes[targetNodeIndex].kind
            if kind == AiNodeKind.auto or kind == AiNodeKind.road or
               kind == AiNodeKind.ramp or kind == AiNodeKind.halfpipe then
                -- Don't skip ramps when low on power
                if mad.power:compareTo(fix64.create(80)) < 0 and
                   (kind == AiNodeKind.ramp or kind == AiNodeKind.halfpipe) then
                    break
                end

                targetNodeIndex = targetNodeIndex + 1
                if targetNodeIndex >= numNodes then
                    targetNodeIndex = 0
                end
            end
        end

        -- Check for sequence start
        if self.racePhase.currentStage.nodes[targetNodeIndex].kind == AiNodeKind.sequenceStart then
            for i = targetNodeIndex + 1, numNodes - 1 do
                if self.racePhase.currentStage.nodes[i].kind == AiNodeKind.sequenceEnd then
                    self.sequence = {
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
        local maxmagThreshold = mad.stat.maxmag * fix64.create(0.8)
        local wantFix = mad.hitmag:compareTo(maxmagThreshold) > 0 and
                        random:nextF64():compareTo(rubberbandingFactor) < 0

        if wantFix then
            -- Find all fix road nodes
            local fixRoadNodes = {}
            for i = 0, numNodes - 1 do
                local kind = self.racePhase.currentStage.nodes[i].kind
                if kind == AiNodeKind.fixRoadStart or kind == AiNodeKind.fixRoadEnd then
                    table.insert(fixRoadNodes, i)
                end
            end

            if #fixRoadNodes > 0 then
                local selectedIndex = random:next(0, #fixRoadNodes - 1)
                self.targetFixRoadStartNode = fixRoadNodes[selectedIndex + 1]
                targetNodeIndex = self.targetFixRoadStartNode

                local kind = self.racePhase.currentStage.nodes[targetNodeIndex].kind
                if kind == AiNodeKind.fixRoadStart then
                    -- Find corresponding FixRoadEnd
                    for i = targetNodeIndex + 1, numNodes - 1 do
                        if self.racePhase.currentStage.nodes[i].kind == AiNodeKind.fixRoadEnd then
                            self.sequence = {
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
                        if self.racePhase.currentStage.nodes[i].kind == AiNodeKind.fixRoadStart then
                            self.sequence = {
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
        local seq = self.sequence

        if targetNodeIndex < seq.startNode or targetNodeIndex > seq.endNode then
            -- Outside of sequence, drive back to start
            targetNodeIndex = seq.startNode
        elseif targetNodeIndex == seq.currentNode then
            if not seq.traversingBackwards then
                -- Move to next node in sequence
                local nextNodeIndex = targetNodeIndex + 1
                if nextNodeIndex > seq.endNode then
                    -- End of sequence reached
                    self.sequence = nil
                else
                    self.sequence.currentNode = nextNodeIndex
                    targetNodeIndex = nextNodeIndex
                end
            else
                -- Move to previous node in sequence
                local prevNodeIndex = targetNodeIndex - 1
                if prevNodeIndex < seq.startNode then
                    -- Start of sequence reached
                    self.sequence = nil
                else
                    self.sequence.currentNode = prevNodeIndex
                    targetNodeIndex = prevNodeIndex
                end
            end
        else
            -- Drive back to start of sequence
            targetNodeIndex = seq.startNode
        end
    end

    local finalTargetNode = self.racePhase.currentStage.nodes[targetNodeIndex]
    FrameTrace.addMessage("Targeting node index: " .. targetNodeIndex ..
                         ", kind: " .. tostring(finalTargetNode.kind))
    FrameTrace.addMessage("Actual node target: " .. self._targetNode)

    self:target(car, finalTargetNode.position)
end

---Applies steering, throttle, and brake controls
---@param self table
---@param car IInGameCarInstance
---@param mad MadInstance
---@param u ControlInstance
function ElStupido:steer(car, mad, u)
    -- Reset input controls
    u.up = false
    u.down = false
    u.left = false
    u.right = false
    u.handb = false

    local myxz = car.rotation.yaw.degrees
    if u.zyinv then
        myxz = myxz + fix64.create(180)  -- Adjust if car is inverted
    end

    -- Steering control logic
    local diff = angleDiff(myxz, self.pan)
    FrameTrace.addMessage("Angle diff: " .. diff:toString())

    local five = fix64.create(5)
    local negFive = fix64.create(-5)
    local one = fix64.one
    local negOne = fix64.create(-1)

    if diff:compareTo(five) > 0 then
        u.right = true
    elseif diff:compareTo(negFive) < 0 then
        u.left = true
    else
        if diff:compareTo(one) > 0 then
            u.right = self.smallturn
            self.smallturn = not self.smallturn
        elseif diff:compareTo(one) < 0 then
            u.left = self.smallturn
            self.smallturn = not self.smallturn
        end
    end

    -- Throttle and brake control logic
    if mad.speed:compareTo(mad.stat.swits.x) > 0 then
        local absDiff = fix64.abs(diff)
        local fifty = fix64.create(50)
        local onetwenty = fix64.create(120)

        if absDiff:compareTo(fifty) < 0 then
            u.up = true
        elseif absDiff:compareTo(onetwenty) < 0 then
            u.down = true
        else
            u.handb = true
        end
    else
        u.up = true
    end
end

---Calculates target angle to drive toward a position
---@param self table
---@param car IInGameCarInstance
---@param position f64Vector3Instance
function ElStupido:target(car, position)
    -- Calculate direction vector
    local dx = position.x - car.position.x
    local dz = position.z - car.position.z

    -- Calculate angle in degrees using atan2
    local angleRad = fix64.atan2(dx, dz)
    local angleDeg = angleRad * (fix64.create(180) / fix64.pi)
    self.pan = -angleDeg
end

return ElStupido
