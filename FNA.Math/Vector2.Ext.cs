namespace Microsoft.Xna.Framework;

public partial struct Vector2
{
    public static implicit operator System.Numerics.Vector2(Vector2 v)
    {
        return new System.Numerics.Vector2(v.X, v.Y);
    }
}