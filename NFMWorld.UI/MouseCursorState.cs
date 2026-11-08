using System.Numerics;

namespace NFMWorld.Reactor;

public sealed class MouseCursorState
{
    private bool _hidden;
    private Vector2 _menuPosition;

    public bool Locked { get; private set; }

    public (bool Visible, Vector2? WarpTo) Update(
        bool menuVisible,
        bool windowFocused,
        Vector2 position,
        Vector2 viewport
    )
    {
        Locked = windowFocused && !menuVisible;
        if (!windowFocused) return (true, null);

        if (!menuVisible)
        {
            if (!_hidden)
            {
                _menuPosition = position;
                _hidden = true;
            }

            var center = new Vector2(MathF.Floor(viewport.X / 2), MathF.Floor(viewport.Y / 2));
            return (false, position == center ? null : center);
        }

        if (!_hidden) return (true, null);
        _hidden = false;
        var restored = Vector2.Clamp(_menuPosition, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport - Vector2.One));
        return (true, position == restored ? null : restored);
    }
}
