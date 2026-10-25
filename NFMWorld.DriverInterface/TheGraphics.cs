namespace NFMWorld.DriverInterface.DriverInterface;

public static class TheGraphics
{
    public static IGraphics G => IBackend.Backend.Graphics;
}