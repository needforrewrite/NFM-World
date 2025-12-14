using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Transform
{
    public virtual Vector3 Position { get; set; } = Vector3.Zero;
    public virtual Euler Rotation { get; set; } = new();
    
    public virtual void GameTick()
    {
    }
}