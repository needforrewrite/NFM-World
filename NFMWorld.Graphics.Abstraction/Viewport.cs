namespace NFMWorld.Graphics;

public readonly record struct Viewport(float X, float Y, float Width, float Height, float MinDepth = 0f, float MaxDepth = 1f);
