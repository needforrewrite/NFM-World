using FixedMathSharp.Utility;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Mad;

namespace NFMWorldLibrary.Backend.AI;

/// <summary>
/// Handles AI decision making, path finding, and control inputs based on difficulty and race conditions.
/// </summary>
public class ElStupido(BaseGamemode gamemode, IRaceValues racePhase) : BaseAi
{
    /// <summary>
    /// Pythagorean distance squared calculation (integer version).
    /// Used for fast distance comparisons without square root.
    /// </summary>
    private static int pyo(int x1, int x2, int z1, int z2) {
        return (((x1 - x2) * (x1 - x2)) + ((z1 - z2) * (z1 - z2)));
    }
    
    /// <summary>
    /// Pythagorean distance squared calculation (fixed-point version).
    /// Used for fast distance comparisons without square root.
    /// </summary>
    private static fix64 pyo(fix64 x1, fix64 x2, fix64 z1, fix64 z2) {
        return (((x1 - x2) * (x1 - x2)) + ((z1 - z2) * (z1 - z2)));
    }

    private fix64 pan = fix64.Zero;
    private fix64 difficulty = 1; // 0.0 (easy) to 1.0 (hard)
    
    // if the next node is a sequence start node, we start a sequence and store sequenceStartNode and sequenceEndNode
    // then, we must traverse the nodes in the sequence in order, until we reach sequenceEndNode
    // if the target node is not the next sequence node in order, we drive back to the start of the sequence
    // we also support driving backwards from a FixRoadEnd to a FixHoop
    private readonly record struct Sequence(int StartNode, int EndNode, int CurrentNode, bool TraversingBackwards);
    private Sequence? sequence;
    
    private int? targetFixRoadStartNode = null;
    private bool bouncing;
    private int _targetNode;
    
    // Obstacle avoidance state
    private int _stuckCounter;
    private fix64 _avoidanceAngle;
    private int _avoidanceTimer;
    private bool smallturn;

    /// <summary>
    /// Main AI update function. Called every frame to compute control inputs for the AI vehicle.
    /// </summary>
    /// <param name="car">The AI-controlled car</param>
    /// <param name="u">Control output structure</param>
    /// <param name="position">Current race position</param>
    /// <param name="currentCarIndex">Index of the current car</param>
    public override void RunAi(IInGameCar car, int currentCarIndex)
    {
        // Get current race state information
        var u = car.Control;
        var position = car.placement;
        var mad = car.Mad;

        // Initialize random number generator with deterministic seed based on car position
        var conto = new ContO(car);
        DeterministicRandom random = new((ulong)(conto.X.Value.m_rawValue ^ conto.Y.Value.m_rawValue ^ conto.Z.Value.m_rawValue));
        
        // Calculate rubberbanding factor
        // 1.0 = last place, 0.0 = first place
        var rubberbandingFactor = (fix64)position / (fix64)(racePhase.CarsInRace.Count - 1);
        
        if (car.Wasted) return;

        bool grounded;
        if (bouncing)
        {
            grounded = mad.Wtouch; // Use wheel touch when bounce enabled
        }
        else
        {
            grounded = mad.Mtouch; // Use main/body touch otherwise
        }

        FindDrivingTarget(car, rubberbandingFactor, mad, ref random);

        if (grounded)
        {
            // Check if we're stuck against a wall
            DetectAndAvoidObstacles(car, mad, racePhase.CurrentStage);
            
            Steer(car, mad, u);
        }
    }

    /// <summary>
    /// Detects when the car is stuck against a wall and applies avoidance steering.
    /// Checks if the car has low speed despite throttle input, indicating a collision.
    /// </summary>
    private void DetectAndAvoidObstacles(IInGameCar car, Mad.Mad mad, IStage stage)
    {
        // Decrease avoidance timer
        if (_avoidanceTimer > 0)
        {
            _avoidanceTimer--;
            // Override pan with avoidance angle while timer is active
            pan = _avoidanceAngle;
            FrameTrace.AddMessage($"Avoiding obstacle, timer: {_avoidanceTimer}, angle: {_avoidanceAngle}");
            return;
        }

        // Check if car is stuck (low speed despite wanting to go forward)
        var isThrottling = car.Control.Up;
        var isStuck = isThrottling && mad.Speed < 20; // Speed threshold for "stuck"

        if (isStuck)
        {
            _stuckCounter++;
            
            // If stuck for multiple frames, initiate avoidance
            if (_stuckCounter > 10) // Stuck for ~0.16 seconds at 60fps
            {
                FrameTrace.AddMessage($"Car stuck! Speed: {mad.Speed}, initiating avoidance");
                
                // Check which direction to turn by sampling points to the left and right
                var currentHeading = car.Rotation.Yaw.Degrees;
                var leftAngle = currentHeading - 90;
                var rightAngle = currentHeading + 90;
                
                // Sample points at 45 degrees left and right
                var sampleDistance = (fix64)500; // Distance to check ahead
                
                var leftX = car.Position.X + fix64.Sin(leftAngle * fix64.Pi / 180) * sampleDistance;
                var leftZ = car.Position.Z + fix64.Cos(leftAngle * fix64.Pi / 180) * sampleDistance;
                
                var rightX = car.Position.X + fix64.Sin(rightAngle * fix64.Pi / 180) * sampleDistance;
                var rightZ = car.Position.Z + fix64.Cos(rightAngle * fix64.Pi / 180) * sampleDistance;
                
                // Check if there are walls in those directions by checking node distances
                var leftClearance = GetClearanceInDirection(car, leftX, leftZ, stage);
                var rightClearance = GetClearanceInDirection(car, rightX, rightZ, stage);
                
                FrameTrace.AddMessage($"Left clearance: {leftClearance}, Right clearance: {rightClearance}");
                
                // Turn toward the more open direction
                if (leftClearance > rightClearance)
                {
                    _avoidanceAngle = leftAngle;
                }
                else
                {
                    _avoidanceAngle = rightAngle;
                }
                
                // Set avoidance timer (about 1 second)
                _avoidanceTimer = 60;
                _stuckCounter = 0;
            }
        }
        else
        {
            // Reset stuck counter if moving normally
            _stuckCounter = 0;
        }
    }

    /// <summary>
    /// Estimates clearance in a given direction by finding the nearest node.
    /// Higher values = more open space.
    /// </summary>
    private fix64 GetClearanceInDirection(IInGameCar car, fix64 targetX, fix64 targetZ, IStage stage)
    {
        // Find closest node to the sample point
        fix64 minDistSq = fix64.MaxValue;
        
        foreach (var node in stage.nodes)
        {
            var distSq = pyo(targetX, node.Position.X, targetZ, node.Position.Z);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
            }
        }
        
        // Return the distance to nearest node (higher = more open)
        return fix64.Sqrt(minDistSq);
    }

    private void FindDrivingTarget(IInGameCar car, fix64 rubberbandingFactor, Mad.Mad mad, ref DeterministicRandom random)
    {
        // If distance to target node <5000 units, target next node, except if the current node is a checkpoint
        var targetNodeIndex = _targetNode;
        if (targetNodeIndex < car.lastCheckpointNode + 1)
        {
            targetNodeIndex = car.lastCheckpointNode + 1;
            if (targetNodeIndex >= racePhase.CurrentStage.nodes.Count)
            {
                targetNodeIndex = 0;
            }
        }
        // Sometimes there can be fix hoop nodes after the last checkpoint, so we need to skip those
        var finalCheckpointNodeIndex = racePhase.CurrentStage.nodes.IndexOf(racePhase.CurrentStage.checkpoints[^1]);
        if (targetNodeIndex > finalCheckpointNodeIndex)
        {
            targetNodeIndex = 0;
        }

        // Special case: if we've just crossed the final checkpoint and are starting a new lap
        if (targetNodeIndex == finalCheckpointNodeIndex && car.lastCheckpointNode == -1)
        {
            targetNodeIndex = 0;
        }
        
        // Check if we're close to any node ahead of _targetNode but before the next checkpoint
        // This allows the AI to naturally skip ahead when taking ramps or shortcuts
        var nextCheckpointIndex = car.currentCheckpoint;
        var nextCheckpointNodeIndex = racePhase.CurrentStage.nodes.IndexOf(racePhase.CurrentStage.checkpoints[nextCheckpointIndex]);
        
        for (int i = targetNodeIndex + 1; i <= nextCheckpointNodeIndex; i++)
        {
            var nodeIndex = i;
            if (nodeIndex >= racePhase.CurrentStage.nodes.Count)
            {
                nodeIndex -= racePhase.CurrentStage.nodes.Count;
            }
            
            var node = racePhase.CurrentStage.nodes[nodeIndex];
            var distanceToNodeSq = pyo(car.Position.X, node.Position.X, car.Position.Z, node.Position.Z);
            
            // If we're close to this node (within speed-based threshold), advance target to it
            if (distanceToNodeSq < (200 * mad.Speed * mad.Speed))
            {
                _targetNode = nodeIndex;
                targetNodeIndex = nodeIndex;
                Logging.Info($"Advanced _targetNode to {nodeIndex} (visited ahead of current target)");
                break;
            }
        }
        
        var targetNode = racePhase.CurrentStage.nodes[targetNodeIndex];
        if (targetNode.Kind is not AiNodeKind.CheckPoint)
        {
            while (true)
            {
                targetNode = racePhase.CurrentStage.nodes[targetNodeIndex];
                if (targetNode.Kind is not AiNodeKind.Road and not AiNodeKind.CheckPoint and not AiNodeKind.Ramp
                    and not AiNodeKind.Halfpipe and not AiNodeKind.Auto)
                {
                    targetNodeIndex++;
                    if (targetNodeIndex >= racePhase.CurrentStage.nodes.Count)
                    {
                        targetNodeIndex = 0;
                    }

                    continue;
                }

                var distanceToTargetSq = pyo(car.Position.X, targetNode.Position.X, car.Position.Z, targetNode.Position.Z);
                if (distanceToTargetSq < (100 * car.Mad.Speed * car.Mad.Speed))
                {
                    targetNodeIndex++;
                    if (targetNodeIndex >= racePhase.CurrentStage.nodes.Count)
                    {
                        targetNodeIndex = 0;
                    }
                }
                else
                {
                    break;
                }
            }
        }
        
        _targetNode = targetNodeIndex;

        if (sequence is not { } theSequence)
        {
            // Skip a number of non-checkpoint nodes based on difficulty and rubberbanding
            var nodesToSkip = (int)(difficulty * 3 * (1 - rubberbandingFactor));
            for (int i = 0; i < nodesToSkip; i++)
            {
                if (racePhase.CurrentStage.nodes[targetNodeIndex].Kind is AiNodeKind.Auto or AiNodeKind.Road or AiNodeKind.Ramp or AiNodeKind.Halfpipe)
                {
                    // Do not skip ramps when low on power
                    if (mad.Power < 80 && racePhase.CurrentStage.nodes[targetNodeIndex].Kind is AiNodeKind.Ramp or AiNodeKind.Halfpipe)
                    {
                        break;
                    }

                    targetNodeIndex++;
                    if (targetNodeIndex >= racePhase.CurrentStage.nodes.Count)
                    {
                        targetNodeIndex = 0;
                    }
                }
            }

            if (racePhase.CurrentStage.nodes[targetNodeIndex].Kind is AiNodeKind.SequenceStart)
            {
                // Find corresponding SequenceEnd node
                for (int i = targetNodeIndex + 1; i < racePhase.CurrentStage.nodes.Count; i++)
                {
                    if (racePhase.CurrentStage.nodes[i].Kind is AiNodeKind.SequenceEnd)
                    {
                        sequence = new Sequence(targetNodeIndex, i, targetNodeIndex, false);
                        break;
                    }
                }
            }

            // If high on damage, find a random FixRoadStart node and enter it as a sequence
            var wantFix = mad.Hitmag > mad.Stat.Maxmag * (fix64)0.8f && random.NextF64() < rubberbandingFactor;
            if (wantFix)
            {
                var fixRoadStartNodes = racePhase.CurrentStage.nodes
                    .Select((node, index) => (node, index))
                    .Where(n => n.node.Kind is AiNodeKind.FixRoadStart or AiNodeKind.FixRoadEnd)
                    .ToArray();
                if (fixRoadStartNodes.Length > 0)
                {
                    var selectedIndex = random.Next(0, fixRoadStartNodes.Length - 1);
                    targetFixRoadStartNode = fixRoadStartNodes[selectedIndex].index;
                    targetNodeIndex = targetFixRoadStartNode.Value;

                    if (racePhase.CurrentStage.nodes[targetNodeIndex].Kind is AiNodeKind.FixRoadStart)
                    {
                        // Find corresponding FixRoadEnd node
                        for (int i = targetNodeIndex + 1; i < racePhase.CurrentStage.nodes.Count; i++)
                        {
                            if (racePhase.CurrentStage.nodes[i].Kind is AiNodeKind.FixRoadEnd)
                            {
                                sequence = new Sequence(targetNodeIndex, i, targetNodeIndex, false);
                                break;
                            }
                        }
                    }
                    else if (racePhase.CurrentStage.nodes[targetNodeIndex].Kind is AiNodeKind.FixRoadEnd)
                    {
                        // Find corresponding FixRoadStart node and set up backwards traversal
                        for (int i = targetNodeIndex - 1; i >= 0; i--)
                        {
                            if (racePhase.CurrentStage.nodes[i].Kind is AiNodeKind.FixRoadStart)
                            {
                                sequence = new Sequence(i, targetNodeIndex, i, true);
                                break;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (targetNodeIndex < theSequence.StartNode || targetNodeIndex > theSequence.EndNode)
            {
                // Outside of sequence, drive back to start
                targetNodeIndex = theSequence.StartNode;
            }
            else if (targetNodeIndex == theSequence.CurrentNode)
            {
                if (!theSequence.TraversingBackwards)
                {
                    // Move to next node in sequence
                    var nextNodeIndex = targetNodeIndex + 1;
                    if (nextNodeIndex > theSequence.EndNode)
                    {
                        // End of sequence reached
                        sequence = null;
                    }
                    else
                    {
                        sequence = theSequence with { CurrentNode = nextNodeIndex };
                        targetNodeIndex = nextNodeIndex;
                    }
                }
                else
                {
                    // Move to previous node in sequence
                    var prevNodeIndex = targetNodeIndex - 1;
                    if (prevNodeIndex < theSequence.StartNode)
                    {
                        // Start of sequence reached
                        sequence = null;
                    }
                    else
                    {
                        sequence = theSequence with { CurrentNode = prevNodeIndex };
                        targetNodeIndex = prevNodeIndex;
                    }
                }
            }
            else
            {
                // Drive back to start of sequence
                targetNodeIndex = theSequence.StartNode;
            }
        }

        FrameTrace.AddMessage($"Targeting node index: {targetNodeIndex}, Position: {racePhase.CurrentStage.nodes[targetNodeIndex].Position}, kind: {racePhase.CurrentStage.nodes[targetNodeIndex].Kind}");
        FrameTrace.AddMessage($"Actual node target: {_targetNode}, Position: {racePhase.CurrentStage.nodes[_targetNode].Position}, kind: {racePhase.CurrentStage.nodes[_targetNode].Kind}");
        FrameTrace.AddMessage($"Sequence: {sequence}");
        FrameTrace.AddMessage($"targetFixRoadStartNode: {targetFixRoadStartNode}");
        Target(car, racePhase.CurrentStage.nodes[targetNodeIndex].Position);
    }

    private void Steer(IInGameCar car, Mad.Mad mad, Control u)
    {
        // Reset input controls
        u.Up = false;
        u.Down = false;
        u.Left = false;
        u.Right = false;
        u.Handb = false;

        var myxz = car.Rotation.Yaw.Degrees;
        if (u.Zyinv) {
            myxz += 180; // Adjust if car is inverted
        }

        // Steering control logic
        var angleDiff = AngleDiff(myxz, pan);
        FrameTrace.AddMessage($"Angle: {angleDiff}");
        if (angleDiff > 5)
        {
            u.Right = true;
        }
        else if (angleDiff < -5)
        {
            u.Left = true;
        }
        else
        {
            if (angleDiff > 1)
            {
                u.Right = smallturn;
                smallturn = !smallturn;
            }
            else if (angleDiff < 1)
            {
                u.Left = smallturn;
                smallturn = !smallturn;
            }
        }

        // Throttle and brake control logic
        if (mad.Speed > mad.Stat.Swits[0])
        {
            if (fix64.Abs(angleDiff) < 50)
            {
                u.Up = true;
            }
            else if (fix64.Abs(angleDiff) < 120)
            {
                u.Down = true;
            }
            else
            {
                u.Handb = true;
            }
        }
        else
        {
            u.Up = true;
        }
    }

    private static fix64 AngleDiff(fix64 a, fix64 b)
    {
        var angleDiff = a - b;
        angleDiff = ((angleDiff + 180) % 360) - 180;
        if (angleDiff < -180) {
            angleDiff += 360;
        }
        return angleDiff;
    }

    private void Target(IInGameCar car, f64Vector3 position)
    {
        // Target object position
        var targetX = position.X;
        var targetZ = position.Z;

        // Calculate direction vector
        var dx = targetX - car.Position.X;
        var dz = targetZ - car.Position.Z;

        // Calculate angle in degrees using atan2
        // atan2(dx, dz) gives angle from +Z axis, clockwise
        pan = -(fix64.Atan2(dx, dz) * (180 / fix64.Pi));
    }
    // End of RunAi method
}