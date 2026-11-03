using System.Runtime.CompilerServices;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;

namespace NFMWorld.Reactor;

public sealed class KeyboardNavigation
{
    private sealed class Options
    {
        public bool Row;
        public bool Scope;
        public Func<bool>? Preferred;
        public Func<Key, bool>? HandleKey;
        public Component? LastFocused;
        public int LastFocusIndex;
    }

    private static readonly ConditionalWeakTable<Node, Options> Settings = new();
    private Node? _scope;

    public static T Row<T>(T node) where T : Node
    {
        Settings.GetOrCreateValue(node).Row = true;
        return node;
    }

    public static T Scope<T>(T node) where T : Node
    {
        Settings.GetOrCreateValue(node).Scope = true;
        return node;
    }

    public static T Bind<T>(T control, Func<Key, bool> handleKey, Action<bool> focusChanged,
        Func<bool>? preferred = null) where T : Component
    {
        control.IsFocusable = true;
        var options = Settings.GetOrCreateValue(control);
        options.HandleKey = handleKey;
        options.Preferred = preferred;
        control.Focused += () => focusChanged(true);
        control.Unfocused += () => focusChanged(false);
        return control;
    }

    public void UpdateFocus(Node root)
    {
        var scope = FindScope(root);
        var controls = Controls(scope).ToArray();
        var state = Settings.GetOrCreateValue(scope);
        if (!ReferenceEquals(scope, _scope))
        {
            _scope = scope;
            var saved = state.LastFocused;
            Focus(controls.Contains(saved) ? saved : controls.FirstOrDefault(control =>
                Settings.TryGetValue(control, out var options) && options.Preferred?.Invoke() == true)
                ?? controls.FirstOrDefault());
        }
        else if (!controls.Contains(FocusManager.FocusedNode) && controls.Length > 0)
            Focus(controls[Math.Clamp(state.LastFocusIndex, 0, controls.Length - 1)]);

        state.LastFocused = FocusManager.FocusedNode as Component;
        state.LastFocusIndex = Array.IndexOf(controls, state.LastFocused);
    }

    public bool HandleKey(Node root, Key key)
    {
        UpdateFocus(root);
        var scope = _scope!;
        var controls = Controls(scope).ToArray();
        if (FocusManager.FocusedNode is not Component focused || !controls.Contains(focused))
            return key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Space;

        if (Settings.TryGetValue(focused, out var options) && options.HandleKey?.Invoke(key) == true)
            return true;

        if (key is Key.Up or Key.Down)
        {
            var rows = controls.GroupBy(control => FindRow(control, scope)).Select(row => row.ToArray()).ToArray();
            var index = Array.FindIndex(rows, row => row.Contains(focused));
            var next = rows[(index + (key == Key.Up ? -1 : 1) + rows.Length) % rows.Length];
            Focus(next.MinBy(control => Math.Abs(control.LayoutBorderPosition.X - focused.LayoutBorderPosition.X)));
        }
        else if (key is Key.Left or Key.Right)
        {
            if (focused is TextInput) return false;
            var row = controls.Where(control => ReferenceEquals(FindRow(control, scope), FindRow(focused, scope))).ToArray();
            var index = Array.IndexOf(row, focused);
            Focus(row[(index + (key == Key.Left ? -1 : 1) + row.Length) % row.Length]);
        }
        else if (key is Key.Enter or Key.Space)
        {
            if (focused is TextInput) return false;
            var e = new KeyboardEvent(key, key, default);
            focused.KeyPressed?.Invoke(e);
            focused.OnKeyPressed(e);
        }
        else return false;

        Settings.GetOrCreateValue(scope).LastFocused = FocusManager.FocusedNode as Component;
        Settings.GetOrCreateValue(scope).LastFocusIndex = Array.IndexOf(controls, FocusManager.FocusedNode as Component);
        return true;
    }

    private static void Focus(Component? control)
    {
        if (control is null) return;
        control.Focus();
        control.ScrollIntoView();
    }

    private static Node FindScope(Node root)
    {
        var scope = root;
        foreach (var child in root.VisualChildren)
        {
            if (child is Component { IsDisplayed: false }) continue;
            var nested = FindScope(child);
            if (!ReferenceEquals(nested, child) || Settings.TryGetValue(child, out var options) && options.Scope)
                scope = nested;
        }
        return scope;
    }

    private static IEnumerable<Component> Controls(Node root)
    {
        if (root is Component { IsDisplayed: false }) yield break;
        if (root is Component { IsFocusable: true } control) yield return control;
        foreach (var child in root.VisualChildren)
            foreach (var descendant in Controls(child))
                yield return descendant;
    }

    private static Node FindRow(Component control, Node scope)
    {
        for (var parent = control.VisualParent; parent is not null && !ReferenceEquals(parent, scope); parent = parent.VisualParent)
            if (Settings.TryGetValue(parent, out var options) && options.Row) return parent;
        return control;
    }
}
