using NFMWorld.DriverInterface;

namespace NFMWorld;

internal sealed class GarageCameraControls
{
    private bool _dragging;
    private Vector2 _lastMouse;
    public float Angle { get; private set; }
    public float Height { get; private set; }

    public void Reset()
    {
        _dragging = false;
        Angle = 0;
        Height = 0;
    }

    public void Tick(float seconds)
    {
        if (!_dragging)
            Angle = (Angle + seconds * 0.08f) % MathF.Tau;
    }

    public void Press(int x, int y, bool captured, MouseButton button)
    {
        if (captured || button != MouseButton.Primary || x <= UI.Routes.GarageView.SidebarWidth)
            return;
        _lastMouse = new Vector2(x, y);
        _dragging = true;
    }

    public void Move(int x, int y, bool captured, MouseButtons buttons)
    {
        if (captured || (buttons & MouseButtons.Primary) == 0)
            _dragging = false;
        if (!_dragging) return;
        var mouse = new Vector2(x, y);
        var delta = mouse - _lastMouse;
        _lastMouse = mouse;
        Angle -= delta.X * 0.006f;
        Height = Math.Clamp(Height + delta.Y * 2, -220, 850);
    }

    public void Release(MouseButton button)
    {
        if (button == MouseButton.Primary)
            _dragging = false;
    }
}
