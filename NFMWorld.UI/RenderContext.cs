using System.Numerics;
using Maxine.Extensions.Mathematics;

namespace NFMWorld.Reactor;

public readonly record struct RenderContext(
    Vector2 TopLeft,
    float InheritedOpacity = 1f,
    RectangleF? Clip = null);