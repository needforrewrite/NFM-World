namespace NFMWorldLibrary.Collision;

public readonly struct ShapeRoad(f64Vector3 rad, f64Vector3 trackersPosition, fix64 contoXz, f64Vector3 contoPosition)
{
    public f64Vector3 Radius => rad;
    public f64Vector3 GameObjectPosition => contoPosition;
    public f64Vector3 TrackersPosition => trackersPosition;
    public fix64 GameObjectXz => contoXz;
    
    private readonly f64Vector3 worldBoxPosition = trackersPosition.RotateXz(contoXz) + contoPosition;

    public readonly struct Collision(fix64 newY)
    {
        public readonly fix64 newY = newY;
    }

    public Collision? ResolveCollision(in f64Vector3 position) {
        var localPosition = (position + (contoPosition * -1)).RotateXz(-contoXz) + (trackersPosition * -1);
        if (fix64.Abs(localPosition.X) > rad.X || fix64.Abs(localPosition.Y) > rad.Y || fix64.Abs(localPosition.Z) > rad.Z) { // Inside?
            return null;
        }

        if (worldBoxPosition.Y == World.Ground || localPosition.Y <= -5) {
            return null;
        }

        return new Collision(worldBoxPosition.Y);
    }
}