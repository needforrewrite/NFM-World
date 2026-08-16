using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Helpers;

public class CheckPointHelper
{
    public static void CalculatePositions(
        BackendStage currentStage,
        IReadOnlyList<ClientSidePlayer> players
    )
    {
        foreach (var player in players)
        {
            player.Car?.Placement = 0;
        }

        for (int i = 0; i < players.Count; i++)
        {
            var player1 = players[i];
            if (player1.Car is not { } car1) continue;
            for (int j = i + 1; j < players.Count; j++)
            {
                var player2 = players[j];
                if (player2.Car is not { } car2) continue;
                if (car1.TotalCheckpoint != car2.TotalCheckpoint)
                {
                    if (car1.TotalCheckpoint < car2.TotalCheckpoint)
                    {
                        car1.Placement++;
                    }
                    else
                    {
                        car2.Placement++;
                    }
                }
                else
                {
                    int c = car1.CurrentCheckpoint + 1;
                    if (c >= currentStage.Checkpoints.Count)
                    {
                        c = 0;
                    }

                    if (UMath.Py(
                            car1.Position.X / 100,
                            currentStage.Checkpoints[c].Position.X / 100,
                            car1.Position.Z / 100,
                            currentStage.Checkpoints[c].Position.Z / 100
                        ) >
                        UMath.Py(
                            car2.Position.X / 100,
                            currentStage.Checkpoints[c].Position.X / 100,
                            car2.Position.Z / 100,
                            currentStage.Checkpoints[c].Position.Z / 100
                        )
                       )
                    {
                        car1.Placement++;
                    }
                    else
                    {
                        car2.Placement++;
                    }
                }
            }
        }
    }

    public static bool HandleCheckPoint(
        BackendStage currentStage,
        BackendCar car)
    {
        if (car.CurrentCheckpoint >= currentStage.Checkpoints.Count)
            return false;

        var nextCheckpoint = currentStage.Checkpoints[car.CurrentCheckpoint];
        f64Vector3 carPos = car.Position;
        var mad = car.CarPhysics;
        f64Vector3 velocity = new f64Vector3(
            mad.Scx[0] + mad.Scx[1] + mad.Scx[2] + mad.Scx[3],
            mad.Scy[0] + mad.Scy[1] + mad.Scy[2] + mad.Scy[3],
            mad.Scz[0] + mad.Scz[1] + mad.Scz[2] + mad.Scz[3]) / 4;
        f64Vector3 zDir = new f64Vector3(0, 0, 1);
        f64Vector3 rad = new f64Vector3(700, 450,
            60 + fix64.Abs(f64Vector3.Dot(velocity, zDir.RotateXz(nextCheckpoint.Rotation.Xz.Degrees))));
        f64Vector3 trackersPosition = new f64Vector3(0, -350, 0);
        var box = new CollisionBox(rad, trackersPosition, nextCheckpoint.Rotation.Xz.Degrees, nextCheckpoint.Position);

        if (box.ResolveCollision(carPos) is not null)
        {
            car.CurrentCheckpoint++;
            if (car.CurrentCheckpoint >= currentStage.Checkpoints.Count)
            {
                car.LastCheckpointNode = -1;
                car.CurrentCheckpoint = 0;
                car.CurrentLap++;
            }
            else
            {
                car.LastCheckpointNode = currentStage.Nodes.IndexOf(nextCheckpoint);
            }

            car.TotalCheckpoint = car.CurrentCheckpoint + car.CurrentLap * currentStage.Checkpoints.Count;
            return true;
        }

        return false;
    }
}