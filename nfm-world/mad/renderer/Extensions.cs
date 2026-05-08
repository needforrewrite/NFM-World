using System.Runtime.InteropServices;

namespace nfm_world;

public static class Extensions
{
    extension(RectangleF rectangle)
    {
        public bool Contains(Vector2 vec) => rectangle.Contains(vec.X, vec.Y);
    }
}