namespace Microsoft.Xna.Framework;

public partial struct Vector4
{
    public static implicit operator System.Numerics.Vector4(Vector4 v)
    {
        return new System.Numerics.Vector4(v.X, v.Y, v.Z, v.W);
    }
}