using NFMWorld.PluginSystem;

namespace NFMWorld.DriverInterface.DriverInterface;

[PluginVisible(Visible.IncludingAllMembersOfType)]
public static class TheGraphics
{
    public static IGraphics G => IBackend.Backend.Graphics;
    public static IGraphics3D G3D => IBackend.Backend.Graphics3D;
}