using System.Numerics;
using Maxine.Extensions.Mathematics;
using NFMWorld.ClayDom.Events;
using NFMWorldLibrary.Util;

namespace NFMWorld.Reactor;

public static class FocusManager
{
    public static Node? FocusedNode
    {
        get;
        set
        {
            // Browser-like: only fire Unfocused/Focused when the focused node actually
            // changes. Without this guard, clicking an already-focused element would
            // re-fire Unfocused→Focused, which e.g. made a dropdown that closes on blur
            // also close on a re-click of its own (still-focused) trigger.
            if (ReferenceEquals(field, value))
                return;
            if (field is Component cmp)
                cmp.Unfocused?.Invoke();
            field = value;
            if (value is Component cmp1)
                cmp1.Focused?.Invoke();
        }
    }
    public static Node? ActiveNode { get; set; }

    /// <summary>Move focus to the next focusable element in Tab order.</summary>
    public static bool FocusNext(Node root)
    {
        var all = GetFocusableDescendants(root).ToArray();
        if (all.Length == 0) return false;

        var idx = FocusedNode is not null
            ? all.IndexOf(FocusedNode)
            : -1;

        var next = (idx + 1) % all.Length;
        FocusedNode = all[next];
        return true;
    }

    /// <summary>Move focus to the previous focusable element.</summary>
    public static bool FocusPrev(Node root)
    {
        var all = GetFocusableDescendants(root).ToArray();
        if (all.Length == 0) return false;

        var idx = FocusedNode is not null
            ? all.IndexOf(FocusedNode)
            : all.Length;

        var prev = (idx - 1 + all.Length) % all.Length;
        FocusedNode = all[prev];
        return true;
    }

    /// <summary>
    /// Depth-first enumeration of all focusable Nodes under a root.
    /// Respects Visibility (skips Hidden/Collapsed subtrees).
    /// </summary>
    private static IEnumerable<Node> GetFocusableDescendants(Node root)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is not Component { IsDisplayed: true } element)
                continue;

            if (element.IsFocusable)
                yield return element;

            foreach (var descendant in GetFocusableDescendants(element))
                yield return descendant;
        }
    }

    /// <summary>
    /// Hit-test: find the topmost focusable Node at a screen position.
    /// Walks children topmost-first — higher subtree z-index wins over lower, and among
    /// equal z-index the later sibling (rendered last) is on top.
    /// </summary>
    public static Node? HitTest(Component root, Vector2 screenPos)
    {
        var zCache = new Dictionary<Component, int>();
        return HitTestRecursive(root, screenPos, zCache);
    }

    private static Component? HitTestRecursive(Component node, Vector2 pos, Dictionary<Component, int> zCache)
    {
        if (!node.Styles.PointerEvents)
            return null;

        // Skip nodes (and their descendants) clipped out by an overflow ancestor.
        if (node.ClipRect is { } clip && !clip.Contains(pos.X, pos.Y))
            return null;

        // Walk children topmost-first for z-index aware occlusion: a high-z subtree
        // (e.g. a dropdown popup) is hit-tested before lower-z siblings of an ancestor,
        // even if it appears earlier in the tree.
        foreach (var visual in GetTopmostFirst(node, zCache))
        {
            if (!visual.IsDisplayed)
                continue;

            var result = HitTestRecursive(visual, pos, zCache);
            if (result is not null) return result;
        }

        // Check self
        var bounds = new RectangleF(
            node.FocusOrigin.X, node.FocusOrigin.Y,
            node.FocusSize.X, node.FocusSize.Y);

        if (bounds.Contains(pos.X, pos.Y))
            return node;

        return null;
    }

    /// <summary>
    /// Hit-tests and returns the full ancestor chain (root→leaf) of nodes at
    /// <paramref name="screenPos"/>. Empty when nothing hit. Uses the same z-index
    /// aware ordering as <see cref="HitTest"/> so hover matches the mousedown hit.
    /// </summary>
    public static List<Component> HitTestChain(Component root, LuaVector2 screenPos)
    {
        var chain = new List<Component>();
        var zCache = new Dictionary<Component, int>();
        HitTestChainRecursive(root, screenPos, chain, zCache);
        return chain;
    }

    /// <summary>
    /// Enumerates a node's visual children topmost-first for hit-testing:
    /// higher subtree z-index first, then later (rendered-last) siblings first.
    /// A node's effective z for ordering is the max z-index of itself and its subtree,
    /// so a high-z descendant (e.g. a dropdown popup) is hit-tested before lower-z
    /// siblings of an ancestor. Falls back to plain reverse (later-first) when the
    /// whole subtree has no positive z-index, preserving the old zero-allocation path.
    /// </summary>
    private static IEnumerable<Component> GetTopmostFirst(Component node, Dictionary<Component, int> zCache)
    {
        // Fast path: no positive z-index under this node → plain reverse (later-first),
        // no per-node list allocation (common case — the Lua UI mostly uses z=0).
        if (Component.EscapeZ(node, zCache) <= 0)
        {
            var children = node.VisualChildren;
            for (int i = children.Count - 1; i >= 0; i--)
                if (children[i] is Component c)
                    yield return c;
            yield break;
        }

        var list = new List<(int idx, Component c)>();
        int n = 0;
        foreach (var visual in node.VisualChildren)
        {
            if (visual is Component c)
            {
                list.Add((n, c));
                n++;
            }
        }

        list.Sort((x, y) =>
        {
            int zx = Component.EscapeZ(x.c, zCache);
            int zy = Component.EscapeZ(y.c, zCache);
            if (zx != zy) return zy.CompareTo(zx);   // higher z first
            return y.idx.CompareTo(x.idx);           // later sibling first on tie
        });

        foreach (var (_, c) in list)
            yield return c;
    }

    private static bool HitTestChainRecursive(Component node, LuaVector2 pos, List<Component> chain, Dictionary<Component, int> zCache)
    {
        if (!node.Styles.PointerEvents)
            return false;

        // Skip nodes (and their descendants) clipped out by an overflow ancestor.
        if (node.ClipRect is { } clip && !clip.Contains(pos.X, pos.Y))
            return false;

        var selfHit = false;

        var bounds = new RectangleF(
            node.FocusOrigin.X, node.FocusOrigin.Y,
            node.FocusSize.X, node.FocusSize.Y);

        if (bounds.Contains(pos.X, pos.Y))
        {
            chain.Add(node);       // ancestor added before children
            selfHit = true;
        }

        // If self isn't hit, children can't be hit either (they're contained within self).
        // Exception: non-focusable containers — they pass through even though selfHit=false.
        if (!selfHit && node.IsFocusable)
            return false;

        foreach (var visual in GetTopmostFirst(node, zCache))
        {
            if (!visual.IsDisplayed)
                continue;

            if (HitTestChainRecursive(visual, pos, chain, zCache))
                return true;           // deepest child hit — stop
        }

        return selfHit;
    }

    private static List<Component> _hoveredChain = [];

    /// <summary>
    /// Hit-tests under the cursor, diffs against the previous hover chain,
    /// and dispatches MouseEntered / MouseLeft / MouseMoved as appropriate.
    /// Call once per frame from your MouseMoved handler.
    ///
    /// The hit-test chain already contains exactly the nodes under the cursor
    /// (every ancestor of the topmost hit node), so hover changes are
    /// dispatched only for nodes that actually entered or left the cursor.
    /// Each node's <see cref="Component.DispatchMouseEntered"/> /
    /// <see cref="Component.DispatchMouseLeft"/> are self-only (they do not
    /// recurse into children), so siblings and un-hit descendants are never
    /// spuriously hovered.
    /// </summary>
    public static void DispatchMouseMove(Component root, BaseMouseMoveEvent @event)
    {
        var newChain = HitTestChain(root, @event.Position);

        // Snapshot the previous hover chain before dispatching. MouseLeft /
        // MouseEntered callbacks can re-entrantly mutate _hoveredChain (a React
        // commit triggered by an event handler calls ResetHover, which clears
        // the list), so iterate over a stable copy instead of indexing the
        // live list while it shrinks.
        var prevChain = new List<Component>(_hoveredChain);

        // MouseLeft — nodes that were hovered but no longer are (leaf→root).
        for (int i = prevChain.Count - 1; i >= 0; i--)
        {
            var node = prevChain[i];
            if (!newChain.Contains(node))
                node.DispatchMouseLeft(@event);
        }

        // MouseEntered — nodes under the cursor that weren't before (root→leaf).
        for (int i = 0; i < newChain.Count; i++)
        {
            var node = newChain[i];
            if (!prevChain.Contains(node))
                node.DispatchMouseEntered(@event);
        }

        _hoveredChain = newChain;

        // MouseMoved — always fire from root so it propagates to all children
        root.DispatchMouseMoved(@event);
    }

    /// <summary>Clear focus entirely.</summary>
    public static void ClearFocus()
    {
        FocusedNode = null;
        ActiveNode = null;
    }

    /// <summary>
    /// Handles a mouse press for focus purposes. Hit-tests the chain under the
    /// cursor and focuses the topmost (deepest) focusable element in it; if the
    /// clicked tree contains no focusable element, clears focus entirely
    /// (unfocuses whatever was focused). Call once per mouse press, before
    /// dispatching the press event into the tree.
    /// </summary>
    public static void HandleMousePressed(Component root, BaseMouseEvent @event)
    {
        var chain = HitTestChain(root, @event.Position);

        // Chain is root→leaf; the deepest focusable element under the cursor wins.
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i].IsFocusable)
            {
                ActiveNode = chain[i];
                FocusedNode = chain[i];
                return;
            }
        }

        // Clicked a tree with no focusable element → drop focus entirely.
        ClearFocus();
    }

    /// <summary>
    /// Handles a mouse release: clears the pressed/active state. Call once per
    /// mouse release, before dispatching the release event into the tree.
    /// </summary>
    public static void HandleMouseReleased()
    {
        ActiveNode = null;
    }

    /// <summary>
    /// Clear the hover chain, resetting <see cref="Component.IsHovered"/> on all currently
    /// hovered elements WITHOUT firing MouseLeft events. Use when deactivating the UI
    /// (phase change) or tearing down the whole tree, where firing leave callbacks against
    /// state that is going away is undesirable.
    /// </summary>
    public static void ClearHover()
    {
        if (_hoveredChain.Count == 0) return;

        for (int i = _hoveredChain.Count - 1; i >= 0; i--)
            _hoveredChain[i].IsHovered = false;

        _hoveredChain.Clear();
    }

    /// <summary>
    /// Reconcile the hover chain after a structural change, mirroring how a browser
    /// re-hit-tests the pointer against the new DOM. Browsers only fire mouseleave on
    /// elements whose hover state actually changed — inserting/removing an unrelated
    /// subtree does NOT fire mouseleave on a sibling still under the cursor. So instead of
    /// wiping the whole chain (which spuriously fired MouseLeft on still-hovered nodes,
    /// e.g. whenever a devtools pane or a Show toggled), we drop stale (disposed)
    /// references and keep the surviving hovered nodes; the next
    /// <see cref="DispatchMouseMove"/> re-diffs against the new tree and fires
    /// leave/enter only for nodes that actually changed. Full teardown / phase changes
    /// should use <see cref="ClearHover"/>.
    /// </summary>
    public static void ResetHover()
    {
        if (_hoveredChain.Count == 0) return;

        // Drop stale (disposed) references silently; keep surviving hovered nodes so the
        // next DispatchMouseMove diffs against them instead of re-firing MouseEntered on
        // everything under the cursor.
        for (int i = _hoveredChain.Count - 1; i >= 0; i--)
        {
            if (_hoveredChain[i].IsDisposed)
                _hoveredChain.RemoveAt(i);
        }
    }
}