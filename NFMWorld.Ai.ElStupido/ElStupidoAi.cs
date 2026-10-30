using FixedMathSharp;
using JetBrains.Annotations;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Util;

[assembly: Ai(Name = "ElStupido", Description = "Follows the stage's AI nodes toward the next checkpoint.")]

namespace NFMWorld.Ai.ElStupido;

/// <summary>
/// Port of <c>data/ais/el_stupido.luau</c>.
/// </summary>
/// <remarks>
/// Steering, node targeting and stuck-avoidance follow the original exactly, including
/// its magic thresholds. State is per instance: two bots on one track must not share a
/// target node or an avoidance timer.
/// </remarks>
[UsedImplicitly]
public sealed class ElStupidoAi(ClientSidePlayer player, string aiId, BaseClientGamemode gamemode)
    : PluginAi(player, aiId, gamemode)
{
    private fix64 _pan;

    private bool _bouncing;
    private int _targetNode = 1;

    /// <summary>Sequence of nodes to traverse, or null when not in one.</summary>
    private Sequence? _sequence;

    /// <summary>Obstacle avoidance state.</summary>
    private int _stuckCounter;
    private fix64 _avoidanceAngle;
    private int _avoidanceTimer;
    private bool _smallTurn;

    private static readonly fix64 Difficulty = (fix64)1;

    private sealed class Sequence
    {
        public required int StartNode;
        public required int EndNode;
        public required int CurrentNode;
        public required bool TraversingBackwards;
    }

    public override void RunAi()
    {
        if (Car is not { } car)
            return;

        if (car.Wasted)
            return;

        var mad = car.CarPhysics;
        var position = car.Placement;

        // Seeded from the car's position so the same replay produces the same decisions.
        var random = new DeterministicRandom((ulong)car.Position.X.rawValue);

        var rubberbandingFactor = (fix64)position / (fix64)(Players.Count - 1);

        var grounded = _bouncing ? mad.Wtouch : mad.Mtouch;

        FindDrivingTarget(car, rubberbandingFactor, mad, ref random);

        if (!grounded)
            return;

        DetectAndAvoidObstacles(car, mad);
        Steer(car, mad);
    }

    /// <summary>Pythagorean distance squared, for comparisons that do not need the root.</summary>
    private static fix64 DistanceSquared(fix64 x1, fix64 x2, fix64 z1, fix64 z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return (dx * dx) + (dz * dz);
    }

    private static fix64 AngleDifference(fix64 a, fix64 b)
    {
        var diff = a - b;
        diff = ((diff + (fix64)180) % (fix64)360) - (fix64)180;
        if (diff < (fix64)(-180))
            diff += (fix64)360;
        return diff;
    }

    /// <summary>Estimates clearance in a direction by finding the nearest node.</summary>
    private fix64 GetClearanceInDirection(fix64 targetX, fix64 targetZ)
    {
        var minDistanceSquared = fix64.MaxValue;
        var nodes = CurrentStage.Nodes;

        for (var i = 0; i < nodes.Count; i++)
        {
            var position = nodes[i].Position;
            var distanceSquared = DistanceSquared(targetX, position.X, targetZ, position.Z);
            if (distanceSquared < minDistanceSquared)
                minDistanceSquared = distanceSquared;
        }

        return minDistanceSquared == fix64.MaxValue ? fix64.Zero : FixedMath.Sqrt(minDistanceSquared);
    }

    /// <summary>Steers away from a wall the car has been pushing against.</summary>
    private void DetectAndAvoidObstacles(BackendCar car, CarPhysics mad)
    {
        if (_avoidanceTimer > 0)
        {
            _avoidanceTimer--;
            _pan = _avoidanceAngle;
            return;
        }

        var isThrottling = car.Control.Up;
        var isStuck = isThrottling && mad.Speed - (fix64)20 < fix64.Zero;

        if (!isStuck)
        {
            _stuckCounter = 0;
            return;
        }

        _stuckCounter++;
        if (_stuckCounter <= 10)
            return;

        // Sample 90 degrees either side and turn toward the more open one.
        var currentHeading = car.Rotation.Yaw.Degrees;
        var leftAngle = currentHeading - (fix64)90;
        var rightAngle = currentHeading + (fix64)90;
        const int sampleDistance = 500;

        var leftRadians = leftAngle * FixedMath.PI / (fix64)180;
        var rightRadians = rightAngle * FixedMath.PI / (fix64)180;

        var leftX = car.Position.X + (FixedMath.Sin(leftRadians) * sampleDistance);
        var leftZ = car.Position.Z + (FixedMath.Cos(leftRadians) * sampleDistance);
        var rightX = car.Position.X + (FixedMath.Sin(rightRadians) * sampleDistance);
        var rightZ = car.Position.Z + (FixedMath.Cos(rightRadians) * sampleDistance);

        _avoidanceAngle = GetClearanceInDirection(leftX, leftZ) >= GetClearanceInDirection(rightX, rightZ)
            ? leftAngle
            : rightAngle;

        _avoidanceTimer = 60;
        _stuckCounter = 0;
    }

    /// <summary>Points the steering at a position.</summary>
    private void SetTarget(BackendCar car, f64Vector3 position)
    {
        var dx = position.X - car.Position.X;
        var dz = position.Z - car.Position.Z;
        var angleRadians = FixedMath.Atan2(dx, dz);
        var angleDegrees = angleRadians * ((fix64)180 / FixedMath.PI);
        _pan = -angleDegrees;
    }

    private static int NextNode(int index, int count) => index >= count ? 1 : index + 1;

    /// <summary>Picks the node to drive toward and aims at it.</summary>
    private void FindDrivingTarget(BackendCar car, fix64 rubberbandingFactor, CarPhysics mad, ref DeterministicRandom random)
    {
        var nodes = CurrentStage.Nodes;
        var checkpoints = CurrentStage.Checkpoints;
        var numNodes = nodes.Count;

        if (numNodes == 0)
            return;

        var checkpointCount = checkpoints.Count;

        // The script's node arrays were 1-based; these are 0-based throughout.
        var targetNodeIndex = _targetNode;
        var lastCheckpointNode = car.LastCheckpointNode + 1;
        if (targetNodeIndex < lastCheckpointNode + 1)
            targetNodeIndex = NextNode(lastCheckpointNode, numNodes);

        // Stop before the fix hoops that follow the final checkpoint.
        var finalCheckpointNodeIndex = checkpointCount > 0 ? IndexOf(nodes, checkpoints[checkpointCount - 1]) : -1;
        if (finalCheckpointNodeIndex >= 0 && targetNodeIndex > finalCheckpointNodeIndex)
            targetNodeIndex = 1;

        // A new lap starts over from the first node.
        if (targetNodeIndex == finalCheckpointNodeIndex && car.LastCheckpointNode == -1)
            targetNodeIndex = 1;

        // Skip ahead to any node we are already close to, which handles ramps and shortcuts.
        var nextCheckpointNodeIndex = checkpointCount > 0 ? IndexOf(nodes, checkpoints[Math.Min(car.CurrentCheckpoint, checkpointCount - 1)]) : -1;
        for (var i = targetNodeIndex + 1; i <= nextCheckpointNodeIndex; i++)
        {
            var nodeIndex = i;
            if (nodeIndex >= numNodes)
                nodeIndex -= numNodes;

            if (nodeIndex < 0 || nodeIndex >= numNodes)
                continue;

            var position = nodes[nodeIndex].Position;
            var distanceToNodeSquared = DistanceSquared(car.Position.X, position.X, car.Position.Z, position.Z);
            var threshold = (fix64)200 * (mad.Speed * mad.Speed);

            if (distanceToNodeSquared >= threshold)
                continue;

            _targetNode = nodeIndex;
            targetNodeIndex = nodeIndex;
            break;
        }

        targetNodeIndex = Math.Clamp(targetNodeIndex, 0, numNodes - 1);

        // Walk past non-drivable and already-reached nodes, unless we are sitting on a
        // checkpoint (which must be touched to count).
        var targetNode = nodes[targetNodeIndex];
        if (targetNode.Kind != AiNodeKind.CheckPoint)
        {
            // The loop is bounded by node count: a stage made entirely of non-drivable
            // nodes would otherwise spin forever.
            for (var hop = 0; hop < numNodes; hop++)
            {
                targetNode = nodes[targetNodeIndex];
                var kind = targetNode.Kind;

                if (kind is not (AiNodeKind.Road or AiNodeKind.CheckPoint or AiNodeKind.Ramp or AiNodeKind.Halfpipe or AiNodeKind.Auto))
                {
                    targetNodeIndex = NextNode(targetNodeIndex, numNodes) - 1;
                    continue;
                }

                var distanceToTargetSquared = DistanceSquared(car.Position.X, targetNode.Position.X, car.Position.Z, targetNode.Position.Z);
                var threshold = (fix64)100 * (mad.Speed * mad.Speed);
                if (distanceToTargetSquared >= threshold)
                    break;

                targetNodeIndex = NextNode(targetNodeIndex, numNodes) - 1;
            }

            targetNodeIndex = Math.Clamp(targetNodeIndex, 0, numNodes - 1);
        }

        _targetNode = targetNodeIndex;

        if (_sequence is null)
        {
            targetNodeIndex = ApplyDifficultySkip(targetNodeIndex, numNodes, mad, rubberbandingFactor);
            targetNodeIndex = TryStartSequence(targetNodeIndex, numNodes, mad, rubberbandingFactor, ref random);
        }
        else
        {
            targetNodeIndex = TraverseSequence(targetNodeIndex) ?? targetNodeIndex;
        }

        targetNodeIndex = Math.Clamp(targetNodeIndex, 0, numNodes - 1);
        SetTarget(car, nodes[targetNodeIndex].Position);
    }

    /// <summary>
    /// Skip nodes based on difficulty and rubberbanding: last place skips ahead, first
    /// place drives the actual line. Returns the possibly-advanced entry point for the
    /// sequence search, which is only meaningful when <see cref="_sequence"/> is null.
    /// </summary>
    private int ApplyDifficultySkip(int targetNodeIndex, int numNodes, CarPhysics mad, fix64 rubberbandingFactor)
    {
        var skipFactor = Difficulty * (fix64)3 * ((fix64)1 - rubberbandingFactor);
        var nodesToSkip = (int)FixedMath.Floor(skipFactor);

        for (var i = 0; i < nodesToSkip; i++)
        {
            var kind = CurrentStage.Nodes[targetNodeIndex].Kind;
            if (kind is not (AiNodeKind.Auto or AiNodeKind.Road or AiNodeKind.Ramp or AiNodeKind.Halfpipe))
                break;

            // Do not skip ramps while low on power.
            if (mad.Power < (fix64)80 && kind is AiNodeKind.Ramp or AiNodeKind.Halfpipe)
                break;

            targetNodeIndex = NextNode(targetNodeIndex, numNodes) - 1;
        }

        return targetNodeIndex;
    }

    /// <summary>
    /// Starts a sequence when the target lands on one, or when the car wants a fix road.
    /// Returns the node to drive toward.
    /// </summary>
    private int TryStartSequence(int targetNodeIndex, int numNodes, CarPhysics mad, fix64 rubberbandingFactor, ref DeterministicRandom random)
    {
        var nodes = CurrentStage.Nodes;

        if (nodes[targetNodeIndex].Kind == AiNodeKind.SequenceStart)
        {
            for (var i = targetNodeIndex + 1; i < numNodes; i++)
            {
                if (nodes[i].Kind != AiNodeKind.SequenceEnd)
                    continue;

                _sequence = new Sequence
                {
                    StartNode = targetNodeIndex,
                    EndNode = i,
                    CurrentNode = targetNodeIndex,
                    TraversingBackwards = false
                };
                break;
            }
        }

        // A damaged car occasionally detours to a fix road, more often the further back it is.
        var maxMagThreshold = (fix64)mad.Stat.Maxmag * (fix64)0.8;
        var wantFix = (fix64)mad.DamagePoints > maxMagThreshold && random.NextFixed6401() < rubberbandingFactor;
        if (!wantFix)
            return targetNodeIndex;

        var fixRoadNodes = new List<int>();
        for (var i = 0; i < numNodes; i++)
        {
            if (nodes[i].Kind is AiNodeKind.FixRoadStart or AiNodeKind.FixRoadEnd)
                fixRoadNodes.Add(i);
        }

        if (fixRoadNodes.Count == 0)
            return targetNodeIndex;

        var selected = random.Next(0, fixRoadNodes.Count);
        targetNodeIndex = fixRoadNodes[selected];

        var kind = nodes[targetNodeIndex].Kind;
        if (kind == AiNodeKind.FixRoadStart)
        {
            for (var i = targetNodeIndex + 1; i < numNodes; i++)
            {
                if (nodes[i].Kind != AiNodeKind.FixRoadEnd)
                    continue;

                _sequence = new Sequence
                {
                    StartNode = targetNodeIndex,
                    EndNode = i,
                    CurrentNode = targetNodeIndex,
                    TraversingBackwards = false
                };
                break;
            }
        }
        else if (kind == AiNodeKind.FixRoadEnd)
        {
            for (var i = targetNodeIndex - 1; i >= 0; i--)
            {
                if (nodes[i].Kind != AiNodeKind.FixRoadStart)
                    continue;

                _sequence = new Sequence
                {
                    StartNode = i,
                    EndNode = targetNodeIndex,
                    CurrentNode = i,
                    TraversingBackwards = true
                };
                break;
            }
        }

        return targetNodeIndex;
    }

    /// <summary>
    /// Advances through an active sequence. Returns the node to drive toward, or null
    /// when the sequence just ended (the caller keeps the current target).
    /// </summary>
    private int? TraverseSequence(int targetNodeIndex)
    {
        var sequence = _sequence!;

        if (targetNodeIndex < sequence.StartNode || targetNodeIndex > sequence.EndNode)
            return sequence.StartNode;

        if (targetNodeIndex != sequence.CurrentNode)
            return sequence.StartNode;

        if (sequence.TraversingBackwards)
        {
            var previous = targetNodeIndex - 1;
            if (previous < sequence.StartNode)
            {
                _sequence = null;
                return null;
            }

            sequence.CurrentNode = previous;
            return previous;
        }

        var next = targetNodeIndex + 1;
        if (next > sequence.EndNode)
        {
            _sequence = null;
            return null;
        }

        sequence.CurrentNode = next;
        return next;
    }

    private static int IndexOf(UnlimitedArray<StageObject> nodes, StageObject node)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], node))
                return i;
        }

        return -1;
    }

    /// <summary>Applies steering, throttle and brake to the car's controls.</summary>
    private void Steer(BackendCar car, CarPhysics mad)
    {
        var u = car.Control;
        u.Up = false;
        u.Down = false;
        u.Left = false;
        u.Right = false;
        u.Handb = false;

        var heading = car.Rotation.Yaw.Degrees;
        if (u.Zyinv)
            heading += (fix64)180;

        var diff = AngleDifference(heading, _pan);

        if (diff > (fix64)5)
        {
            u.Right = true;
        }
        else if (diff < (fix64)(-5))
        {
            u.Left = true;
        }
        else if (diff > (fix64)1)
        {
            // Within the deadband, pulse the steering so the car creeps toward the line
            // instead of holding a turn.
            u.Right = _smallTurn;
            _smallTurn = !_smallTurn;
        }
        else if (diff < (fix64)(-1))
        {
            u.Left = _smallTurn;
            _smallTurn = !_smallTurn;
        }

        if (mad.Speed <= mad.Stat.Swits.X)
        {
            u.Up = true;
            return;
        }

        var absDiff = FixedMath.Abs(diff);
        if (absDiff < (fix64)50)
            u.Up = true;
        else if (absDiff < (fix64)120)
            u.Down = true;
        else
            u.Handb = true;
    }
}
