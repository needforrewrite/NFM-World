using Maxine.Extensions.Mathematics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface.DriverInterface;
using SignalsDotnet;

namespace NFMWorld.Reactor.Sx;

/// <summary>
/// The component API: build a <see cref="Node"/> tree from signals, and the renderer keeps
/// the tree in sync.
/// </summary>
/// <remarks>
/// A "component" is just a method that returns a node - there is no vnode and no diffing.
/// Reactivity is per slot: every <c>Func&lt;T&gt;</c> parameter below becomes its own
/// effect, so a signal write re-applies only the property or text it feeds.
///
/// Children may be a node, a <c>Func</c> returning one (a reactive slot), or a string.
/// </remarks>
public static class Sx
{
    // ── Style helpers ──────────────────────────────────────────────────
    //
    // Object-initialiser terse form, so a style reads like the rest of the tree:
    //   Sx.View(style: Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 8))
    // Every parameter is optional; an omitted one stays null, i.e. "not set", so the
    // result still merges correctly through Styles.Over.

    public static Styles Styles(
        Visibility? visibility = null,
        float? opacity = null,
        Direction? direction = null,
        FlexDirection? flexDirection = null,
        Justify? justifyContent = null,
        Align? alignItems = null,
        Align? alignSelf = null,
        Align? alignContent = null,
        Position? position = null,
        Wrap? flexWrap = null,
        Overflow? overflow = null,
        Display? display = null,
        BoxSizing? boxSizing = null,
        float? flex = null,
        float? flexGrow = null,
        float? flexShrink = null,
        float? minWidth = null,
        float? minHeight = null,
        float? maxWidth = null,
        float? maxHeight = null,
        float? aspectRatio = null,
        MeasurementWidthHeight? width = null,
        MeasurementWidthHeight? height = null,
        MeasurementMarginPosition? top = null,
        MeasurementMarginPosition? right = null,
        MeasurementMarginPosition? bottom = null,
        MeasurementMarginPosition? left = null,
        MeasurementGap? gapRow = null,
        MeasurementGap? gapColumn = null,
        float? paddingTop = null,
        float? paddingBottom = null,
        float? paddingLeft = null,
        float? paddingRight = null,
        float? marginTop = null,
        float? marginBottom = null,
        float? marginLeft = null,
        float? marginRight = null,
        float? borderTop = null,
        float? borderBottom = null,
        float? borderLeft = null,
        float? borderRight = null,
        Color? backgroundColor = null,
        Color? borderColor = null,
        CornerRadius? borderRadius = null,
        bool? pointerEvents = null,
        int? zIndex = null)
        => new()
        {
            Visibility = visibility,
            Opacity = opacity,
            Direction = direction,
            FlexDirection = flexDirection,
            JustifyContent = justifyContent,
            AlignItems = alignItems,
            AlignSelf = alignSelf,
            AlignContent = alignContent,
            Position = position,
            FlexWrap = flexWrap,
            Overflow = overflow,
            Display = display,
            BoxSizing = boxSizing,
            Flex = flex,
            FlexGrow = flexGrow,
            FlexShrink = flexShrink,
            MinWidth = minWidth.ToMeasurementWidthHeight(),
            MinHeight = minHeight.ToMeasurementWidthHeight(),
            MaxWidth = maxWidth.ToMeasurementWidthHeight(),
            MaxHeight = maxHeight.ToMeasurementWidthHeight(),
            AspectRatio = aspectRatio,
            Width = width.ToMeasurementWidthHeight(),
            Height = height.ToMeasurementWidthHeight(),
            Top = top.ToMeasurementMarginPosition(),
            Right = right.ToMeasurementMarginPosition(),
            Bottom = bottom.ToMeasurementMarginPosition(),
            Left = left.ToMeasurementMarginPosition(),
            GapRow = gapRow.ToMeasurementGap(),
            GapColumn = gapColumn.ToMeasurementGap(),
            PaddingTop = paddingTop.ToMeasurementPadding(),
            PaddingBottom = paddingBottom.ToMeasurementPadding(),
            PaddingLeft = paddingLeft.ToMeasurementPadding(),
            PaddingRight = paddingRight.ToMeasurementPadding(),
            MarginTop = marginTop.ToMeasurementMarginPosition(),
            MarginBottom = marginBottom.ToMeasurementMarginPosition(),
            MarginLeft = marginLeft.ToMeasurementMarginPosition(),
            MarginRight = marginRight.ToMeasurementMarginPosition(),
            BorderTop = borderTop,
            BorderBottom = borderBottom,
            BorderLeft = borderLeft,
            BorderRight = borderRight,
            BackgroundColor = backgroundColor,
            BorderColor = borderColor,
            BorderRadius = borderRadius,
            PointerEvents = pointerEvents,
            ZIndex = zIndex,
        };

    private static MeasurementWidthHeight ToMeasurementWidthHeight(this MeasurementWidthHeight? v)
        => v ?? MeasurementWidthHeight.Undefined;

    private static MeasurementWidthHeight ToMeasurementWidthHeight(this float? v)
        => v is { } p ? p : MeasurementWidthHeight.Undefined;

    private static MeasurementMarginPosition ToMeasurementMarginPosition(this MeasurementMarginPosition? v)
        => v ?? MeasurementMarginPosition.Undefined;

    private static MeasurementGap ToMeasurementGap(this MeasurementGap? v)
        => v ?? MeasurementGap.Undefined;

    private static MeasurementPadding ToMeasurementPadding(this float? v)
        => v is { } p ? p : MeasurementPadding.Undefined;

    private static MeasurementMarginPosition ToMeasurementMarginPosition(this float? v)
        => v is { } p ? p : MeasurementMarginPosition.Undefined;

    public static TextStyles TextStyles(
        Color? foregroundColor = null,
        Color? strokeColor = null,
        FontFamily? fontFamily = null,
        float? fontSize = null,
        FontStyle? fontStyle = null,
        BreakType? breakType = null,
        OverflowBehavior? overflowBehavior = null,
        TextHorizontalAlignment? horizontalAlignment = null,
        TextVerticalAlignment? verticalAlignment = null)
        => new()
        {
            ForegroundColor = foregroundColor,
            StrokeColor = strokeColor,
            FontFamily = fontFamily,
            FontSize = fontSize,
            FontStyle = fontStyle,
            BreakType = breakType,
            OverflowBehavior = overflowBehavior,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
        };

    public static NFMWorld.Reactor.TextInputStyles TextInputStyles(
        Color? cursorColor = null,
        Color? selectionColor = null,
        Color? placeholderColor = null)
        => new()
        {
            CursorColor = cursorColor,
            SelectionColor = selectionColor,
            PlaceholderColor = placeholderColor,
        };

    private static class Measurement
    {
    }

    // ── Elements ───────────────────────────────────────────────────────

    public static View View(
        Func<Styles>? style = null,
        Func<string?>? name = null,
        Action<MouseEvent>? onmousedown = null,
        Action<MouseDragEvent>? onmousedrag = null,
        Action<MouseMoveEvent>? onmouseenter = null,
        Action<MouseMoveEvent>? onmouseleave = null,
        Action<KeyboardEvent>? onkeydown = null,
        int? tabIndex = null,
        Action<View>? onmount = null,
        params object?[] children)
    {
        var view = new View();
        ApplyStyle(view, style);
        ApplyName(view, name);
        view.MousePressed = onmousedown;
        view.MouseDragged = onmousedrag;
        view.MouseEntered = onmouseenter;
        view.MouseLeft = onmouseleave;

        if (onkeydown is not null)
            view.KeyPressed = onkeydown;

        if (tabIndex is { } index)
        {
            view.TabIndex = index;
            view.IsFocusable = true;
        }

        AddChildren(view, children);

        // Runs inline, once, at the end of construction: everything the callback needs
        // (handlers, children) is wired by now.
        onmount?.Invoke(view);

        return view;
    }

    /// <summary>
    /// A reactive derived value. Kept as the SignalsDotnet type so <c>.Value</c> reads
    /// inside a slot's effect subscribe to it.
    /// </summary>
    public static IReadOnlySignal<T> Computed<T>(Func<T> compute) => Signal.Computed(compute);

    public static Text Text(
        Func<TextStyles>? style = null,
        Func<string?>? text = null,
        Func<Styles>? layout = null,
        params object?[] children)
    {
        var node = new Text();
        ApplyTextStyle(node, style);
        ApplyStyle(node, layout);

        if (text is not null)
            node.Bind(b => b.Bind(text, value => node.TextContent = value));

        AddChildren(node, children);
        return node;
    }

    public static TextInput TextInput(
        Func<Styles>? style = null,
        Func<string?>? text = null,
        Action<string>? onchange = null,
        Func<string?>? placeholder = null,
        Action<string>? onsubmit = null,
        params object?[] children)
    {
        var node = new TextInput();
        ApplyStyle(node, style);

        // Text: the input mirrors TextStyles onto its inner Text, so it has to be the
        // resolved form or that child would read nulls.
        node.TextStyles = node.TextStyles.Resolved;

        if (text is not null)
            node.Bind(b => b.Bind(text, value => node.SetText(value ?? "")));

        if (placeholder is not null)
            node.Bind(b => b.Bind(placeholder, value => node.Placeholder = value ?? ""));

        node.TextChanged = onchange;
        node.Submitted = onsubmit;

        AddChildren(node, children);
        return node;
    }

    /// <summary>
    /// A host for flow content: a real node for the renderer to write into, but with
    /// <see cref="Display.Contents"/> so it contributes no box of its own and its
    /// children lay out in the grandparent. Without this the host is a plain row
    /// container and every slotted child stacks sideways.
    /// </summary>
    internal static View FlowHost()
    {
        // Set on the field, not through the Styles setter: the setter pushes the value
        // into Yoga, which needs a graphics backend, and a flow host has nothing to push
        // (Display.Contents is dropped from the layout pass entirely).
        var host = new View();
        host.MakeFlowHost();
        return host;
    }

    // ── Flow ───────────────────────────────────────────────────────────

    /// <summary>
    /// One of two branches, chosen by a signal. The unchosen branch does not exist.
    /// </summary>
    public static Node Show(Func<bool> when, Func<object?> then, Func<object?>? otherwise = null)
    {
        var host = FlowHost();
        var scope = new RenderScope();

        // What is currently mounted: null, or which branch. Compared by value, not by the
        // condition, so flipping `when` back and forth remounts rather than being ignored.
        var mounted = new bool?[] { null };
        var branchScope = new RenderScope?[] { null };

        void Mount(bool condition)
        {
            if (scope.IsDisposed || mounted[0] == condition)
                return;

            // The outgoing branch's effects must die with its nodes: this component runs
            // once, so a mounted branch's effects are only ever released here or when the
            // host itself leaves the tree.
            mounted[0] = condition;
            branchScope[0]?.Dispose();
            Clear(host);

            var next = new RenderScope();
            branchScope[0] = next;

            var content = Renderer.RunInScope(next, () => condition ? then() : otherwise?.Invoke());
            if (content is not null)
            {
                // Owned by this component's scope, so a full tree teardown releases it too;
                // Disposing is idempotent so the switch path below can dispose it first.
                scope.Adopt(next);
                AddChildren(host, [content]);
            }
            else
            {
                next.Dispose();
                branchScope[0] = null;
            }
        }

        Mount(when());
        host.Bind(s => s.Bind(when, Mount));
        host.Own(scope);
        host.Disposing += () => branchScope[0]?.Dispose();
        return host;
    }

    /// <summary>
    /// Renders one node per item, keyed by the item's identity. A changed list reuses the
    /// nodes whose item is unchanged; only additions and removals touch the tree.
    /// </summary>
    /// <remarks>
    /// Items must be stable identifiers: a reused row is NOT re-rendered, so an item type
    /// with value equality whose other fields change would keep showing its old content,
    /// and two equal items would collapse into one row.
    /// </remarks>
    public static Node For<T>(Func<IReadOnlyList<T>> each, Func<T, int, object?> row)
    {
        var host = FlowHost();
        var scope = new RenderScope();

        var rows = new Dictionary<T, (Node Node, RenderScope Scope)>(EqualityComparer<T>.Default);

        void Update()
        {
            if (scope.IsDisposed)
                return;

            var items = each();
            var seen = new HashSet<T>(EqualityComparer<T>.Default);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                seen.Add(item);

                if (rows.ContainsKey(item))
                    continue;

                var rowScope = new RenderScope();
                var node = Materialize(Scope.Root(rowScope, () => row(item, i)));
                rows[item] = (node, rowScope);
                host.AddChild(node);
            }

            foreach (var (item, entry) in rows.ToArray())
            {
                if (seen.Contains(item))
                    continue;

                // Dispose before detaching: the detach raises Disposing, and the scope
                // must not be released twice.
                entry.Scope.Dispose();
                host.RemoveAt(host.Children.IndexOf(entry.Node));
                rows.Remove(item);
            }

            // Rows are keyed by value, but the item order is authoritative: without this
            // a reused row keeps its old slot, so changing which items a page has (the
            // menu's BACK, whose delegate compares equal on every page) leaves them in
            // the previous order.
            for (var target = 0; target < items.Count; target++)
            {
                var node = rows[items[target]].Node;
                var current = host.Children.IndexOf(node);
                if (current != target)
                    host.Children.MoveChild(current, target);
            }
        }

        Update();
        host.Bind(scope => scope.Bind(each, _ => Update()));
        host.Own(scope);

        host.Disposing += () =>
        {
            foreach (var (_, entry) in rows)
                entry.Scope.Dispose();
            rows.Clear();
        };

        return host;
    }

    /// <summary>
    /// One branch chosen by a key, where each branch is a component of its own.
    /// </summary>
    /// <remarks>
    /// The branch builds under its own scope, so switching disposes the outgoing
    /// branch's effects. Keep the key signal to rare changes - a key that changes every
    /// frame rebuilds the subtree every frame.
    /// </remarks>
    public static Node Switch<TKey>(
        IReadOnlySignal<TKey> key,
        IReadOnlyDictionary<TKey, Func<Node>> branches,
        Func<Node>? fallback = null)
    {
        var host = FlowHost();
        var owner = new RenderScope();
        var mounted = false;
        var currentKey = default(TKey)!;
        var currentScope = new RenderScope?[] { null };

        void Update()
        {
            var nextKey = key.Value;
            if (mounted && EqualityComparer<TKey>.Default.Equals(currentKey, nextKey))
                return;

            if (!branches.TryGetValue(nextKey, out var build))
            {
                build = fallback
                        ?? throw new InvalidOperationException($"No branch for {nextKey}.");
            }

            currentScope[0]?.Dispose();
            Clear(host);

            var scope = new RenderScope();
            currentScope[0] = scope;
            currentKey = nextKey;
            mounted = true;

            // Owned by the host's scope, so a tree teardown releases whichever branch is
            // live - not just the one that happened to be mounted at construction.
            owner.Adopt(scope);
            host.AddChild(Materialize(Renderer.RunInScope(scope, build)));
        }

        Update();
        host.Bind(b => b.Bind(() => key.Value, _ => Update()));
        host.Own(owner);
        host.Disposing += () => currentScope[0]?.Dispose();
        return host;
    }

    // ── Internals ──────────────────────────────────────────────────────

    internal static void ApplyStyle(Component component, Func<Styles>? style)
    {
        if (style is null)
            return;

        component.Bind(b => b.Bind(style, value => component.Styles = value));
    }

    private static void ApplyTextStyle(Text text, Func<TextStyles>? style)
    {
        if (style is null)
            return;

        text.Bind(b => b.Bind(style, value => text.TextStyles = value));
    }

    private static void ApplyName(Component component, Func<string?>? name)
    {
        if (name is not null)
            component.Bind(b => b.Bind(name, value => component.Name = value));
    }

    /// <summary>Runs a component method, mounting anything it creates under its own scope.</summary>
    private static Node Materialize(object? value)
        => value switch
        {
            Node node => node,
            null => throw new InvalidOperationException("A component returned null."),
            _ => throw new InvalidOperationException($"A component returned {value.GetType().Name}, not a Node.")
        };

    internal static void AddChildren(Component parent, object?[] children)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case null:
                    continue;
                case Node node:
                    parent.AddChild(node);
                    break;
                case string text:
                    parent.AddChild(new TextNode { Text = text });
                    break;
                case Func<object?> slot:
                    parent.AddChild(MountSlot(slot));
                    break;
                default:
                    throw new ArgumentException($"Unsupported child {child.GetType().Name} on {parent.GetType().Name}.");
            }
        }
    }

    /// <summary>
    /// A slot child: an anchor the slot's content replaces whenever its signals change.
    /// </summary>
    private static View MountSlot(Func<object?> slot)
    {
        var anchor = FlowHost();
        var scope = new RenderScope();
        var current = new Node?[] { null };
        var slotScope = new RenderScope?[] { null };

        void Update()
        {
            if (scope.IsDisposed)
                return;

            var next = new RenderScope();
            var node = Materialize(Renderer.RunInScope(next, slot));

            if (ReferenceEquals(current[0], node))
            {
                next.Dispose();
                return;
            }

            slotScope[0]?.Dispose();
            slotScope[0] = next;
            current[0] = node;
            Clear(anchor);
            anchor.AddChild(node);
        }

        Update();
        anchor.Bind(b => b.Bind(slot, _ => Update()));
        anchor.Own(scope);
        anchor.Disposing += () => slotScope[0]?.Dispose();
        return anchor;
    }

    private static void Clear(Component parent)
    {
        while (parent.VisualChildren.Count > 0)
            parent.RemoveAt(0);
    }
}

file static class Scope
{
    /// <summary>Runs <paramref name="build"/> with <paramref name="scope"/> as the current mount scope.</summary>
    public static T Root<T>(RenderScope scope, Func<T> build) => Renderer.RunInScope(scope, build);
}
