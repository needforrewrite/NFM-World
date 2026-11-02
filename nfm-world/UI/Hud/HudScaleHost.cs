using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.UI.Hud;

internal sealed class HudScaleHost : Component
{
    private readonly Component _content;

    public HudScaleHost(Component content)
    {
        _content = content;
        Styles = Sx.Styles(position: Position.Absolute, left: 0, top: 0,
            width: "100%", height: "100%", flexShrink: 0, pointerEvents: false);
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var previous = G.Scale;
        try
        {
            G.Scale = previous * SettingsMenu.HudScale;
            var bounds = HudSafeZone.Bounds(position, size);
            _content.LayoutAndRender(new Vector2(bounds.Width, bounds.Height), new Vector2(bounds.X, bounds.Y));
        }
        finally
        {
            G.Scale = previous;
        }
    }
}
