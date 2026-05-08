using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world_library.SoftFloat;

namespace nfm_world;

public class WallCollision : GameObject, ICollidable
{
    public WallCollision(Rad3dBoxDef[] boxes)
    {
        Boxes = boxes;
        
        int maxRadius = 0;
        foreach (var box in Boxes)
        {
            int boxMax = (int)fix64.Ceiling(fix64.Max(box.Radius.X, fix64.Max(box.Radius.Y, box.Radius.Z)));
            if (boxMax > maxRadius)
            {
                maxRadius = boxMax;
            }
        }
        MaxRadius = maxRadius;
    }

    public Rad3dBoxDef[] Boxes { get; }

    public int MaxRadius { get; }
}