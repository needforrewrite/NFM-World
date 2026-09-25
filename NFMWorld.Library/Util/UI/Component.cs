using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor.Layout;
using NFMWorld.ClayDom.Events;
using NFMWorld.Lua;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.Reactor;

// ReSharper disable InconsistentNaming
/// <summary>
/// Represents a single node in the Yoga layout system.
/// </summary>
[DebuggerDisplay("{DebugToString()}"), LuaVisible]
public abstract partial class Component : Node, IAnimationCallback
{
    internal static readonly YogaConfig Config;
    internal YogaNode NodeInternal = new(Config);

    internal readonly string __INTERNAL_CtorCallerFilePath = "";
    internal readonly int __INTERNAL_CtorCallerLineNumber = 0;
    internal readonly string __INTERNAL_CtorCallerMemberName = "";

    public virtual bool DebugIsContentfulNode => Styles.BackgroundColor != null || Styles.BorderColor != null;

    static Component()
    {
        Config = new YogaConfig();
        Config.UseWebDefaults = true;
        Config.SetExperimentalFeatureEnabled(YogaExperimentalFeature.FixFlexBasisFitContent, true);
    }

    // ── Visual abstracts ────────────────────────────────────────────────
    /// <summary>
    /// Gets the visual children of this visual element.
    /// </summary>
    [LuaName]
    public override ReadOnlyLuaArray<Node> VisualChildren { get; } = [];

    /// <summary>
    /// Gets the Yoga node associated with this visual element representing its contents.
    /// </summary>
    internal virtual YogaNode Contents => NodeInternal;

    // ── Children API (no-op for leaf nodes) ──────────────────────────────
    /// <summary>
    /// Whether this visual can accept child nodes. <see cref="Component"/> returns false;
    /// <see cref="View"/> returns true.
    /// </summary>
    [LuaName]
    public virtual bool CanHaveChildren => false;

    /// <summary>Add a child to the end of the children list.</summary>
    [LuaName]
    public virtual void AddChild(Node child) { }

    /// <summary>Insert a child at the given index.</summary>
    [LuaName]
    public virtual void InsertAt(int index, Node child) { }

    /// <summary>Remove the child at the given index.</summary>
    [LuaName]
    public virtual void RemoveAt(int index) { }

    [LuaName]
    public string? Name { get; set; }

    // Reusable snapshot buffer so dispatch methods don't allocate a new list
    // every time VisualChildren is iterated. Allocated once per Visual, cleared
    // and repopulated on each use.
    private List<Node>? _childSnapshot;

    private protected List<Node> GetChildSnapshot()
    {
        var list = _childSnapshot ??= [];
        list.Clear();
        list.AddRange(VisualChildren);
        return list;
    }

    public Styles Styles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnStylesChanged();
            }
        }
    } = new();

    protected virtual void OnStylesChanged()
    {
        NodeInternal.Style.Direction = Styles.Direction.ToYogaDirection();
        NodeInternal.FlexDirection = Styles.FlexDirection.ToYogaFlexDirection();
        NodeInternal.JustifyContent = Styles.JustifyContent.ToYogaJustify();
        NodeInternal.AlignItems = Styles.AlignItems.ToYogaAlign();
        NodeInternal.AlignSelf = Styles.AlignSelf.ToYogaAlign();
        NodeInternal.AlignContent = Styles.AlignContent.ToYogaAlign();
        NodeInternal.PositionType = Styles.Position.ToYogaPositionType();
        NodeInternal.FlexWrap = Styles.FlexWrap.ToYogaWrap();
        NodeInternal.Overflow = Styles.Overflow.ToYogaOverflow();
        NodeInternal.Display = Styles.Display.ToYogaDisplay();
        NodeInternal.Style.BoxSizing = Styles.BoxSizing.ToYogaBoxSizing();
        NodeInternal.Style.Flex = Styles.Flex ?? float.NaN;
        NodeInternal.FlexGrow = Styles.FlexGrow ?? float.NaN;
        NodeInternal.FlexShrink = Styles.FlexShrink ?? float.NaN;
        NodeInternal.FlexBasis = Styles.FlexBasis;
        NodeInternal.SetPosition(YogaEdge.Left, Styles.Left.Scale(G.Scale));
        NodeInternal.SetPosition(YogaEdge.Top, Styles.Top.Scale(G.Scale));
        NodeInternal.SetPosition(YogaEdge.Right, Styles.Right.Scale(G.Scale));
        NodeInternal.SetPosition(YogaEdge.Bottom, Styles.Bottom.Scale(G.Scale));
        NodeInternal.SetMargin(YogaEdge.Top, Styles.MarginTop.Scale(G.Scale));
        NodeInternal.SetMargin(YogaEdge.Bottom, Styles.MarginBottom.Scale(G.Scale));
        NodeInternal.SetMargin(YogaEdge.Left, Styles.MarginLeft.Scale(G.Scale));
        NodeInternal.SetMargin(YogaEdge.Right, Styles.MarginRight.Scale(G.Scale));
        NodeInternal.SetPadding(YogaEdge.Top, Styles.PaddingTop.Scale(G.Scale));
        NodeInternal.SetPadding(YogaEdge.Bottom, Styles.PaddingBottom.Scale(G.Scale));
        NodeInternal.SetPadding(YogaEdge.Left, Styles.PaddingLeft.Scale(G.Scale));
        NodeInternal.SetPadding(YogaEdge.Right, Styles.PaddingRight.Scale(G.Scale));
        NodeInternal.SetBorder(YogaEdge.Top, Styles.BorderTop?.Value * G.Scale ?? float.NaN);
        NodeInternal.SetBorder(YogaEdge.Bottom, Styles.BorderBottom?.Value * G.Scale ?? float.NaN);
        NodeInternal.SetBorder(YogaEdge.Left, Styles.BorderLeft?.Value * G.Scale ?? float.NaN);
        NodeInternal.SetBorder(YogaEdge.Right, Styles.BorderRight?.Value * G.Scale ?? float.NaN);
        NodeInternal.SetGap(YogaGutter.Column, Styles.GapColumn.Scale(G.Scale));
        NodeInternal.SetGap(YogaGutter.Row, Styles.GapRow.Scale(G.Scale));
        NodeInternal.Width = Styles.Width.Scale(G.Scale);
        NodeInternal.Height = Styles.Height.Scale(G.Scale);
        NodeInternal.MinWidth = Styles.MinWidth.Scale(G.Scale);
        NodeInternal.MinHeight = Styles.MinHeight.Scale(G.Scale);
        NodeInternal.MaxWidth = Styles.MaxWidth.Scale(G.Scale);
        NodeInternal.MaxHeight = Styles.MaxHeight.Scale(G.Scale);
        NodeInternal.AspectRatio = Styles.AspectRatio?.Value ?? float.NaN;
    }

    public Action? AnimationFrameBegan { get; set; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual string DebugToString()
    {
        return $"{GetType().Name}(Name={Name}, LayoutX={LayoutX}, LayoutY={LayoutY}, LayoutWidth={LayoutWidth}, LayoutHeight={LayoutHeight})";
    }

    #region Focus

    [LuaName]
    public bool IsFocusable { get; set; }

    public LuaVector2 FocusOrigin => LayoutPaddingPosition;
    public LuaVector2 FocusSize => LayoutPaddingSize;

    public bool IsHovered
    {
        get;
        set;
    }

    public bool IsActive
    {
        get => IsFocusable && ReferenceEquals(FocusManager.ActiveNode, this);
        set
        {
            if (IsFocusable && value)
            {
                FocusManager.ActiveNode = this;
            }
            else if (IsFocusable && ReferenceEquals(FocusManager.ActiveNode, this))
            {
                FocusManager.ActiveNode = null;
            }
        }
    }

    public bool IsFocused
    {
        get => IsFocusable && ReferenceEquals(FocusManager.FocusedNode, this);
        set
        {
            if (IsFocusable && value)
            {
                FocusManager.FocusedNode = this;
            }
            else if (IsFocusable && ReferenceEquals(FocusManager.FocusedNode, this))
            {
                FocusManager.FocusedNode = null;
            }
        }
    }

    public int? TabIndex { get; set; }

    #endregion

    #region Layout

    // https://www.w3schools.com/css/css_boxmodel.asp
    private protected LuaVector2 Root;

    /// <summary>
    /// In the CSS box model, gets the top-left position of the margin box.
    /// </summary>
    [LuaName] public LuaVector2 LayoutMarginPosition => Root + new LuaVector2(LayoutX, LayoutY);

    /// <summary>
    /// In the CSS box model, gets the size of the margin box, from the top-left to the bottom-right.
    /// </summary>
    [LuaName] public LuaVector2 LayoutMarginSize => new(LayoutWidth, LayoutHeight);

    /// <summary>
    /// In the CSS box model, gets the top-left position of the border box.
    /// </summary>
    [LuaName] public LuaVector2 LayoutBorderPosition => Root + new LuaVector2(LayoutX + LayoutMarginLeft, LayoutY + LayoutMarginTop);

    /// <summary>
    /// In the CSS box model, gets the size of the border box, from the top-left to the bottom-right.
    /// </summary>
    [LuaName] public LuaVector2 LayoutBorderSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom));

    /// <summary>
    /// In the CSS box model, gets the top-left position of the padding box.
    /// </summary>
    [LuaName] public LuaVector2 LayoutPaddingPosition => Root + new LuaVector2(LayoutX + LayoutMarginLeft + LayoutBorderLeft, LayoutY + LayoutMarginTop + LayoutBorderTop);

    /// <summary>
    /// In the CSS box model, gets the size of the padding box, from the top-left to the bottom-right.
    /// </summary>
    [LuaName] public LuaVector2 LayoutPaddingSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight + LayoutBorderLeft + LayoutBorderRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom + LayoutBorderTop + LayoutBorderBottom));

    /// <summary>
    /// In the CSS box model, gets the top-left position of the content box.
    /// </summary>
    [LuaName] public LuaVector2 LayoutContentPosition => Root + new LuaVector2(LayoutX + LayoutMarginLeft + LayoutBorderLeft + LayoutPaddingLeft, LayoutY + LayoutMarginTop + LayoutBorderTop + LayoutPaddingTop);

    /// <summary>
    /// In the CSS box model, gets the size of the content box, from the top-left to the bottom-right.
    /// </summary>
    [LuaName] public LuaVector2 LayoutContentSize => new(LayoutWidth - (LayoutMarginLeft + LayoutMarginRight + LayoutBorderLeft + LayoutBorderRight + LayoutPaddingLeft + LayoutPaddingRight), LayoutHeight - (LayoutMarginTop + LayoutMarginBottom + LayoutBorderTop + LayoutBorderBottom + LayoutPaddingTop + LayoutPaddingBottom));

    /// <summary>
    /// Gets the margin width and height of the node as a <see cref="LuaVector2"/>.
    /// </summary>
    [LuaName] public LuaVector2 LayoutMargin => new(LayoutMarginLeft + LayoutMarginRight, LayoutMarginTop + LayoutMarginBottom);

    /// <summary>
    /// Gets the padding width and height of the node as a <see cref="LuaVector2"/>.
    /// </summary>
    [LuaName] public LuaVector2 LayoutPadding => new(LayoutPaddingLeft + LayoutPaddingRight, LayoutPaddingTop + LayoutPaddingBottom);

    /// <summary>
    /// Gets the border width and height of the node as a <see cref="LuaVector2"/>.
    /// </summary>
    [LuaName] public LuaVector2 LayoutBorder => new(LayoutBorderLeft + LayoutBorderRight, LayoutBorderTop + LayoutBorderBottom);

    /// <summary>
    /// The effective visible region (current scissor rect) this node is constrained to,
    /// in screen space. This is the intersection of every clipping ancestor's padding box
    /// (set during <see cref="Render"/> as <see cref="ClipRect"/>), or the full root/viewport
    /// rect when nothing clips. Exposed to Lua so popups (e.g. a dropdown) can decide which
    /// direction to open based on how much space remains inside the visible region.
    /// </summary>
    [LuaName]
    public LuaRect ScissorRect
    {
        get
        {
            if (ClipRect is { } clip)
                return new LuaRect(clip.X, clip.Y, clip.Width, clip.Height);

            var root = RootComponent();
            var pos = root.LayoutMarginPosition;
            var size = root.LayoutMarginSize;
            return new LuaRect(pos.X, pos.Y, size.X, size.Y);
        }
    }

    /// <summary>Walks up the visual-parent chain to the root component (fills the viewport).</summary>
    private Component RootComponent()
    {
        var current = this;
        while (current.VisualParent is Component parent)
            current = parent;
        return current;
    }

    /// <summary>
    /// Gets the width of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and does not include margins, borders, or padding.
    /// </summary>
    [LuaName] public float LayoutWidth => NodeInternal.LayoutWidth;

    /// <summary>
    /// Gets the height of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and does not include margins, borders, or padding.
    /// </summary>
    [LuaName] public float LayoutHeight => NodeInternal.LayoutHeight;

    /// <summary>
    /// Gets the X position of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the left edge of the parent node's content box to the left edge of this node's margin box.
    /// </summary>
    [LuaName] public float LayoutX => NodeInternal.LayoutX;

    /// <summary>
    /// Gets the Y position of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the top edge of the parent node's content box to the top edge of this node's margin box.
    /// </summary>
    [LuaName] public float LayoutY => NodeInternal.LayoutY;

    /// <summary>
    /// Gets the layout direction of the node as determined by the Yoga layout engine after a layout pass.
    /// </summary>
    [LuaName] public Direction LayoutDirection => NodeInternal.Style.Direction.ToNfmDirection();

    /// <summary>
    /// Gets a Value indicating whether the node's content overflowed its layout bounds during the last layout pass.
    /// </summary>
    [LuaName] public bool HadOverflow => NodeInternal.Layout.HadOverflow;

    /// <summary>
    /// Gets the top margin of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the top edge of this node's margin box to the top edge of its border box.
    /// </summary>
    [LuaName] public float LayoutMarginTop => NodeInternal.LayoutMarginTop;

    /// <summary>
    /// Gets the bottom margin of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the bottom edge of this node's margin box to the bottom edge of its border box.
    /// </summary>
    [LuaName] public float LayoutMarginBottom => NodeInternal.LayoutMarginBottom;

    /// <summary>
    /// Gets the left margin of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the left edge of this node's margin box to the left edge of its border box.
    /// </summary>
    [LuaName] public float LayoutMarginLeft => NodeInternal.LayoutMarginLeft;

    /// <summary>
    /// Gets the right margin of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the right edge of this node's margin box to the right edge of its border box.
    /// This Value is in points and represents the distance from the right edge of this node's margin box to the right edge of its border box.
    /// </summary>
    [LuaName] public float LayoutMarginRight => NodeInternal.LayoutMarginRight;

    /// <summary>
    /// Gets the top padding of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the top edge of this node's border box to the top edge of its padding box.
    /// </summary>
    [LuaName] public float LayoutPaddingTop => NodeInternal.LayoutPaddingTop;

    /// <summary>
    /// Gets the bottom padding of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the bottom edge of this node's border box to the bottom edge of its padding box.
    /// </summary>
    [LuaName] public float LayoutPaddingBottom => NodeInternal.LayoutPaddingBottom;

    /// <summary>
    /// Gets the left padding of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the left edge of this node's border box to the left edge of its padding box.
    /// </summary>
    [LuaName] public float LayoutPaddingLeft => NodeInternal.LayoutPaddingLeft;

    /// <summary>
    /// Gets the right padding of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the right edge of this node's border box to the right edge of its padding box.
    /// </summary>
    [LuaName] public float LayoutPaddingRight => NodeInternal.LayoutPaddingRight;

    /// <summary>
    /// Gets the top border of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the top edge of this node's border box to the top edge of its margin box.
    /// </summary>
    [LuaName] public float LayoutBorderTop => NodeInternal.LayoutBorderTop;

    /// <summary>
    /// Gets the bottom border of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the bottom edge of this node's border box to the bottom edge of its margin box.
    /// </summary>
    [LuaName] public float LayoutBorderBottom => NodeInternal.LayoutBorderBottom;

    /// <summary>
    /// Gets the left border of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the left edge of this node's border box to the left edge of its margin box.
    /// </summary>
    [LuaName] public float LayoutBorderLeft => NodeInternal.LayoutBorderLeft;

    /// <summary>
    /// Gets the right border of the node's layout as determined by the Yoga layout engine after a layout pass.
    /// This Value is in points and represents the distance from the right edge of this node's border box to the right edge of its margin box.
    /// </summary>
    [LuaName] public float LayoutBorderRight => NodeInternal.LayoutBorderRight;

    /// <summary>
    /// Gets or sets whether the node's Yoga layout changed. Must be reset by setting it to false.
    /// </summary>
    [LuaName] public bool HasNewLayout
    {
        get => NodeInternal.HasNewLayout;
        set => NodeInternal.HasNewLayout = value;
    }

    /// <summary>
    /// Gets or sets whether the node's Yoga layout results are dirty due to it or its children changing.
    /// </summary>
    [LuaName] public bool IsDirty
    {
        get => NodeInternal.IsDirty;
        set => NodeInternal.SetDirty(value);
    }

    /// <summary>
    /// Gets or sets whether this node is set as the reference baseline.
    /// </summary>
    [LuaName] public bool IsReferenceBaseline
    {
        set => NodeInternal.IsReferenceBaseline = value;
        get => NodeInternal.IsReferenceBaseline;
    }

    /// <summary>
    /// Gets or sets whether a leaf node's layout results may be truncated during layout rounding.
    /// </summary>
    public NodeType NodeType
    {
        get => NodeInternal.NodeType.ToNfmNodeType();
        set => NodeInternal.NodeType = value.ToYogaNodeType();
    }

    /// <summary>
    /// Make it so that this node will always form a containing block for any
    /// descendant nodes. This is useful for when a node has a property outside of
    /// of Yoga that will form a containing block. For example, transforms or some of
    /// the others listed in
    /// https://developer.mozilla.org/en-US/docs/Web/CSS/Containing_block
    /// </summary>
    public bool AlwaysFormsContainingBlock
    {
        get => NodeInternal.AlwaysFormsContainingBlock;
        set => NodeInternal.AlwaysFormsContainingBlock = value;
    }

    #endregion

    #region Scrolling

    private float _scrollLeft;
    private float _scrollTop;
    private float _scrollableWidth;
    private float _scrollableHeight;
    private static bool _textRenderReachedLogged;

    /// <summary>
    /// Horizontal scroll offset in points. Clamped to [0, <see cref="ScrollableWidth"/>].
    /// </summary>
    [LuaName]
    public float ScrollLeft
    {
        get => _scrollLeft;
        set => _scrollLeft = Math.Clamp(value, 0f, _scrollableWidth);
    }

    /// <summary>
    /// Vertical scroll offset in points. Clamped to [0, <see cref="ScrollableHeight"/>].
    /// </summary>
    [LuaName]
    public float ScrollTop
    {
        get => _scrollTop;
        set => _scrollTop = Math.Clamp(value, 0f, _scrollableHeight);
    }

    /// <summary>
    /// Maximum horizontal scroll offset (content width minus viewport width).
    /// </summary>
    [LuaName]
    public float ScrollableWidth => _scrollableWidth;

    /// <summary>
    /// Maximum vertical scroll offset (content height minus viewport height).
    /// </summary>
    [LuaName]
    public float ScrollableHeight => _scrollableHeight;

    /// <summary>
    /// Whether this node clips its children to its padding box.
    /// </summary>
    [LuaName]
    public bool IsClipping => Styles.Overflow is Overflow.Hidden or Overflow.Scroll;

    /// <summary>
    /// The effective clip rectangle (intersection of every clipping ancestor's
    /// padding box) in screen space, or null when nothing clips. Set during
    /// <see cref="Render"/>; used by hit-testing and mouse dispatch.
    /// </summary>
    internal RectangleF? ClipRect;

    /// <summary>
    /// Attempts to scroll this node by the wheel delta. Returns true when the
    /// scroll offset actually changed (i.e. the event was consumed).
    /// </summary>
    public bool TryScroll(float deltaX, float deltaY, float factor = 1f)
    {
        if (Styles.Overflow != Overflow.Scroll)
            return false;

        var newLeft = Math.Clamp(ScrollLeft - deltaX * factor, 0f, _scrollableWidth);
        var newTop = Math.Clamp(ScrollTop - deltaY * factor, 0f, _scrollableHeight);

        if (newLeft == ScrollLeft && newTop == ScrollTop)
            return false;

        _scrollLeft = newLeft;
        _scrollTop = newTop;
        return true;
    }

    /// <summary>
    /// Scrolls every scrollable ancestor so that this element's bounds become
    /// visible within it. Uses layout offsets (scroll-independent), so it is
    /// safe to call before the next render.
    /// </summary>
    [LuaName]
    public void ScrollIntoView()
    {
        for (var ancestor = VisualParent as Component; ancestor != null; ancestor = ancestor.VisualParent as Component)
        {
            if (ancestor.Styles.Overflow == Overflow.Scroll)
            {
                ScrollIntoAncestorView(ancestor);
            }
        }
    }

    private void ScrollIntoAncestorView(Component ancestor)
    {
        // Offset of this node's margin box relative to the ancestor's content
        // box, summed from layout values (unaffected by scroll offsets).
        float offsetX = 0f;
        float offsetY = 0f;
        for (var n = (Node)this; n != null && n != ancestor; n = n.VisualParent)
        {
            if (n is Component c)
            {
                offsetX += c.LayoutX;
                offsetY += c.LayoutY;
            }
        }

        // Yoga positions are relative to the ancestor's border box; convert to
        // content-box coordinates to match ScrollLeft/ScrollTop.
        offsetX -= ancestor.LayoutBorderLeft + ancestor.LayoutPaddingLeft;
        offsetY -= ancestor.LayoutBorderTop + ancestor.LayoutPaddingTop;

        var viewWidth = ancestor.LayoutContentSize.X;
        var viewHeight = ancestor.LayoutContentSize.Y;
        var elemWidth = LayoutMarginSize.X;
        var elemHeight = LayoutMarginSize.Y;

        if (offsetY < ancestor.ScrollTop)
        {
            ancestor.ScrollTop = offsetY;
        }
        else if (offsetY + elemHeight > ancestor.ScrollTop + viewHeight)
        {
            ancestor.ScrollTop = offsetY + elemHeight - viewHeight;
        }

        if (offsetX < ancestor.ScrollLeft)
        {
            ancestor.ScrollLeft = offsetX;
        }
        else if (offsetX + elemWidth > ancestor.ScrollLeft + viewWidth)
        {
            ancestor.ScrollLeft = offsetX + elemWidth - viewWidth;
        }
    }

    private void UpdateScrollExtent()
    {
        // Floating-point rounding shouldn't create a scrollbar.
        const float overflowEpsilon = 0.5f;

        _scrollableWidth = 0f;
        _scrollableHeight = 0f;

        if (Styles.Overflow != Overflow.Scroll)
            return;

        // Yoga positions children relative to the parent's border box, so
        // convert child offsets into content-box coordinates before measuring.
        var originX = LayoutBorderLeft + LayoutPaddingLeft;
        var originY = LayoutBorderTop + LayoutPaddingTop;
        var contentSize = LayoutContentSize;
        float maxRight = 0f;
        float maxBottom = 0f;

        foreach (var child in GetChildSnapshot())
        {
            if (child is not Component c)
                continue;

            maxRight = Math.Max(maxRight, c.LayoutX - originX + c.LayoutWidth + c.LayoutMarginRight);
            maxBottom = Math.Max(maxBottom, c.LayoutY - originY + c.LayoutHeight + c.LayoutMarginBottom);
        }

        _scrollableWidth = Math.Max(0f, maxRight - contentSize.X);
        _scrollableHeight = Math.Max(0f, maxBottom - contentSize.Y);

        if (_scrollableWidth < overflowEpsilon)
            _scrollableWidth = 0f;
        if (_scrollableHeight < overflowEpsilon)
            _scrollableHeight = 0f;

        // Re-clamp against the freshly computed extents (content may have shrank).
        _scrollLeft = Math.Clamp(_scrollLeft, 0f, _scrollableWidth);
        _scrollTop = Math.Clamp(_scrollTop, 0f, _scrollableHeight);
    }

    #endregion

    #region Focus

    [LuaName]
    public void Focus()
    {
        IsFocused = true;
    }

    [LuaName]
    public void Blur()
    {
        IsFocused = false;
    }

    public Action<MouseEvent>? MousePressed { get; set; }
    public Action<MouseEvent>? MouseReleased { get; set; }
    public Action<MouseDragEvent>? MouseDragged { get; set; }
    public Action<MouseWheelEvent>? MouseScrolled { get; set; }
    public Action<MouseMoveEvent>? MouseMoved { get; set; }
    public Action<MouseMoveEvent>? MouseEntered { get; set; }
    public Action<MouseMoveEvent>? MouseLeft { get; set; }
    public Action<KeyboardTypingEvent>? KeyTyped { get; set; }
    public Action<KeyboardEvent>? KeyPressed { get; set; }
    public Action<KeyboardEvent>? KeyReleased { get; set; }
    public Action? Unfocused { get; set; }
    public Action? Focused { get; set; }

    #endregion

    [LuaName]
    public bool IsDisplayed => Styles.Display != Display.None && Styles.Opacity > 0 && Styles.Visibility != Visibility.Hidden;

    private float _lastScale = 1f;

    /// <summary>
    /// Do not use directly.
    /// </summary>
    /// <returns>true if scale changed</returns>
    internal bool Rescale()
    {
        if (Math.Abs(_lastScale - G.Scale) > 0.001f)
        {
            // Re-trigger all size-related onChanged handlers so they re-scale with new G.Scale
#pragma warning disable CA2245
            NodeInternal.Width = Styles.Width.Scale(G.Scale);
            NodeInternal.Height = Styles.Height.Scale(G.Scale);
            NodeInternal.MinWidth = Styles.MinWidth.Scale(G.Scale);
            NodeInternal.MinHeight = Styles.MinHeight.Scale(G.Scale);
            NodeInternal.MaxWidth = Styles.MaxWidth.Scale(G.Scale);
            NodeInternal.MaxHeight = Styles.MaxHeight.Scale(G.Scale);
            NodeInternal.SetPosition(YogaEdge.Left, Styles.Left.Scale(G.Scale));
            NodeInternal.SetPosition(YogaEdge.Top, Styles.Top.Scale(G.Scale));
            NodeInternal.SetPosition(YogaEdge.Right, Styles.Right.Scale(G.Scale));
            NodeInternal.SetPosition(YogaEdge.Bottom, Styles.Bottom.Scale(G.Scale));
            NodeInternal.SetMargin(YogaEdge.Top, Styles.MarginTop.Scale(G.Scale));
            NodeInternal.SetMargin(YogaEdge.Bottom, Styles.MarginBottom.Scale(G.Scale));
            NodeInternal.SetMargin(YogaEdge.Left, Styles.MarginLeft.Scale(G.Scale));
            NodeInternal.SetMargin(YogaEdge.Right, Styles.MarginRight.Scale(G.Scale));
            NodeInternal.SetPadding(YogaEdge.Top, Styles.PaddingTop.Scale(G.Scale));
            NodeInternal.SetPadding(YogaEdge.Bottom, Styles.PaddingBottom.Scale(G.Scale));
            NodeInternal.SetPadding(YogaEdge.Left, Styles.PaddingLeft.Scale(G.Scale));
            NodeInternal.SetPadding(YogaEdge.Right, Styles.PaddingRight.Scale(G.Scale));
            NodeInternal.SetBorder(YogaEdge.Top, Styles.BorderTop?.Value * G.Scale ?? float.NaN);
            NodeInternal.SetBorder(YogaEdge.Bottom, Styles.BorderBottom?.Value * G.Scale?? float.NaN);
            NodeInternal.SetBorder(YogaEdge.Left, Styles.BorderLeft?.Value * G.Scale ?? float.NaN);
            NodeInternal.SetBorder(YogaEdge.Right, Styles.BorderRight?.Value * G.Scale ?? float.NaN);
            NodeInternal.SetGap(YogaGutter.Column, Styles.GapColumn.Scale(G.Scale));
            NodeInternal.SetGap(YogaGutter.Row, Styles.GapRow.Scale(G.Scale));
            NodeInternal.FlexBasis = Styles.FlexBasis.Scale(G.Scale);
#pragma warning restore CA2245

            _lastScale = G.Scale;

            return true;
        }

        return false;
    }

    protected virtual void OnScaleChanged()
    {
    }

    internal void NotifyUiScaleChanged()
    {
        if (Rescale())
        {
            OnScaleChanged();
            foreach (var child in GetChildSnapshot())
            {
                if (child is Component cmp)
                    cmp.NotifyUiScaleChanged();
            }
        }
    }

    internal bool PostLayout()
    {
        var layoutChanged = false;

        if (OnPostLayout())
        {
            layoutChanged = true;
        }

        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
            {
                if (cmp.PostLayout())
                {
                    layoutChanged = true;
                }
            }
        }

        return layoutChanged;
    }

    protected virtual bool OnPostLayout()
    {
        return false;
    }

    protected virtual void RenderBackground(LuaVector2 position, LuaVector2 size)
    {
        if (Styles.BackgroundColor is {} backgroundColor && backgroundColor != Color.Transparent)
        {
            G.SetColor(backgroundColor);
            G.FillVariableBorderRect(
                position.X - LayoutBorderLeft, // FillVariableBorderRect expects the whole size not the inner size
                position.Y - LayoutBorderTop,
                size.X + LayoutBorderLeft + LayoutBorderRight,
                size.Y + LayoutBorderTop + LayoutBorderBottom,
                Styles.BorderTop?.Value ?? 0,
                Styles.BorderRight?.Value ?? 0,
                Styles.BorderBottom?.Value ?? 0,
                Styles.BorderLeft?.Value ?? 0,
                Styles.BorderTopLeftRadius,
                Styles.BorderTopRightRadius,
                Styles.BorderBottomRightRadius,
                Styles.BorderBottomLeftRadius
            );
        }
    }

    protected virtual void RenderBorder(LuaVector2 position, LuaVector2 size)
    {
        if (Styles.BorderColor is { } borderColor && borderColor != Color.Transparent)
        {
            G.SetColor(borderColor);
            G.DrawVariableBorderRect(
                position.X,
                position.Y,
                size.X,
                size.Y,
                Styles.BorderTop?.Value ?? 0,
                Styles.BorderRight?.Value ?? 0,
                Styles.BorderBottom?.Value ?? 0,
                Styles.BorderLeft?.Value ?? 0,
                Styles.BorderTopLeftRadius,
                Styles.BorderTopRightRadius,
                Styles.BorderBottomRightRadius,
                Styles.BorderBottomLeftRadius
            );
        }
    }

    protected virtual void RenderContent(LuaVector2 position, LuaVector2 size)
    {
    }

    protected virtual void RenderScrollbars(LuaVector2 position, LuaVector2 size)
    {
        if (Styles.Overflow != Overflow.Scroll)
            return;

        const float trackWidth = 6f;
        const float minThumb = 20f;

        if (ScrollableHeight > 0f && size.Y > 0f)
        {
            var contentHeight = size.Y + ScrollableHeight;
            var thumbHeight = Math.Max(size.Y * size.Y / contentHeight, minThumb);
            var travel = size.Y - thumbHeight;
            var thumbTop = position.Y + ScrollTop / ScrollableHeight * travel;

            G.SetColor(new Color(255, 255, 255, 32));
            G.FillRoundedRect((int)(position.X + size.X - trackWidth), (int)position.Y, (int)trackWidth, (int)size.Y, 3f, 3f, 3f, 3f);

            G.SetColor(new Color(255, 255, 255, 150));
            G.FillRoundedRect((int)(position.X + size.X - trackWidth), (int)thumbTop, (int)trackWidth, (int)thumbHeight, 3f, 3f, 3f, 3f);
        }

        if (ScrollableWidth > 0f && size.X > 0f)
        {
            var contentWidth = size.X + ScrollableWidth;
            var thumbWidth = Math.Max(size.X * size.X / contentWidth, minThumb);
            var travel = size.X - thumbWidth;
            var thumbLeft = position.X + ScrollLeft / ScrollableWidth * travel;

            G.SetColor(new Color(255, 255, 255, 32));
            G.FillRoundedRect((int)position.X, (int)(position.Y + size.Y - trackWidth), (int)size.X, (int)trackWidth, 3f, 3f, 3f, 3f);

            G.SetColor(new Color(255, 255, 255, 150));
            G.FillRoundedRect((int)thumbLeft, (int)(position.Y + size.Y - trackWidth), (int)thumbWidth, (int)trackWidth, 3f, 3f, 3f, 3f);
        }
    }

    /// <summary>
    /// Max z-index that escapes a node's subtree to its parent for sibling ordering.
    /// A stacking context (z != 0) presents its OWN z — its descendants are contained
    /// within it and do not bubble further. A non-SC node presents the max escaping z of
    /// its descendants. This is the CSS-like "nearest stacking context" rule; it avoids
    /// hoisting an entire ancestor subtree just because it contains one high-z descendant
    /// (e.g. a dropdown popup), which is what the old subtree-max approach did.
    /// </summary>
    internal static int EscapeZ(Component node, Dictionary<Component, int>? cache)
    {
        if (cache is not null && cache.TryGetValue(node, out var cached))
            return cached;

        int result;
        if (node.Styles.ZIndex != 0)
        {
            result = node.Styles.ZIndex;
        }
        else
        {
            result = 0;
            foreach (var visual in node.VisualChildren)
            {
                if (visual is Component c)
                {
                    int cz = EscapeZ(c, cache);
                    if (cz > result) result = cz;
                }
            }
        }

        if (cache is not null)
            cache[node] = result;
        return result;
    }

    private readonly record struct PaintState(
        bool Visible,
        bool Clipping,
        float OwnOpacity,
        LuaVector2 PaddingPos,
        LuaVector2 PaddingSize,
        LuaVector2 ChildOrigin,
        RectangleF? Clip);

    private readonly record struct StackEntry(Component Node, LuaVector2 Origin, RectangleF? Clip, int Z);

    /// <summary>
    /// Paints this node as a stacking context. Non-stacking children (and their escaped
    /// SC descendants) paint inline in tree order; then escaped stacking-context
    /// descendants paint last, in ascending z order. With no z-index anywhere this is
    /// exactly plain tree order, matching the previous behavior.
    /// </summary>
    public void Render(RenderContext context) => PaintStacking(context);

    private void PaintStacking(RenderContext context)
    {
        var s = BeginPaint(context);
        if (!s.Visible) return;

        var scs = new List<StackEntry>();
        PaintChildren(s, scs);
        scs.Sort((a, b) => a.Z.CompareTo(b.Z));

        foreach (var e in scs)
        {
            // Preserve the accumulated ancestor clip for the escaped stacking context.
            G.SaveState();
            if (e.Clip is { } c)
                G.IntersectScissor(c.X, c.Y, c.Width, c.Height);
            e.Node.PaintStacking(new RenderContext(e.Origin, s.OwnOpacity, e.Clip));
            G.RestoreState();
        }

        EndPaint(s);
    }

    private PaintState BeginPaint(RenderContext context)
    {
        OnAnimationFrameBegan();
        Root = context.TopLeft;

        if (this is Text && !_textRenderReachedLogged)
        {
            _textRenderReachedLogged = true;
            Logging.Info($"[Render] TextRun reached. Display={Styles.Display} Vis={Styles.Visibility} Opacity={Styles.Opacity}");
        }

        if (Styles.Display == Display.None || Styles.Visibility != Visibility.Visible || Styles.Opacity <= 0f)
            return default;

        var ownOpacity = context.InheritedOpacity * Styles.Opacity;

        // Recompute scroll extent from freshly laid-out children.
        UpdateScrollExtent();

        // Compute this node's effective clip (padding box when clipping),
        // intersected with any clip inherited from clipping ancestors.
        var paddingPos = LayoutPaddingPosition;
        var paddingSize = LayoutPaddingSize;
        RectangleF? effectiveClip = context.Clip;
        var clipping = IsClipping;
        if (clipping)
        {
            var ownClip = new RectangleF(paddingPos.X, paddingPos.Y, paddingSize.X, paddingSize.Y);
            effectiveClip = effectiveClip is { } inherited ? RectangleF.Intersect(inherited, ownClip) : ownClip;
        }
        ClipRect = effectiveClip;

        G.Alpha = ownOpacity;
        RenderBackground(paddingPos, paddingSize);
        RenderBorder(LayoutBorderPosition, LayoutBorderSize);
        RenderContent(LayoutContentPosition, LayoutContentSize);

        if (clipping)
        {
            G.SaveState();
            G.IntersectScissor(paddingPos.X, paddingPos.Y, paddingSize.X, paddingSize.Y);
        }

        var childOrigin = LayoutBorderPosition - new LuaVector2(ScrollLeft, ScrollTop);
        return new PaintState(true, clipping, ownOpacity, paddingPos, paddingSize, childOrigin, effectiveClip);
    }

    private void EndPaint(PaintState s)
    {
        if (s.Clipping)
            G.RestoreState();

        G.Alpha = s.OwnOpacity;
        RenderScrollbars(s.PaddingPos, s.PaddingSize);
        G.Alpha = 1f;
    }

    private void PaintChildren(PaintState s, List<StackEntry> scs)
    {
        foreach (var visual in VisualChildren)
        {
            if (visual is not Component c || !c.IsDisplayed) continue;
            if (c.Styles.ZIndex != 0)
                scs.Add(new StackEntry(c, s.ChildOrigin, s.Clip, c.Styles.ZIndex));
            else
                PaintNonStacking(c, s.ChildOrigin, s.OwnOpacity, s.Clip, scs);
        }
    }

    private static void PaintNonStacking(Component node, LuaVector2 origin, float opacity, RectangleF? clip, List<StackEntry> scs)
    {
        var s = node.BeginPaint(new RenderContext(origin, opacity, clip));
        if (!s.Visible) return;
        node.PaintChildren(s, scs);
        node.EndPaint(s);
    }

    private protected void OnAnimationFrameBegan()
    {
        AnimationFrameBegan?.Invoke();
    }

    protected virtual void GameTick()
    {
    }

    public void LayoutAndRender(LuaVector2 availableSize, LuaVector2? origin = null)
    {
        NotifyUiScaleChanged();
        NodeInternal.CalculateLayout(availableSize.X, availableSize.Y);
        if (PostLayout())
        {
            // if the layout has changed by PostLayout, relayout
            NodeInternal.CalculateLayout(availableSize.X, availableSize.Y);
        }

        Render(new RenderContext(origin ?? default));
    }

    public void Update()
    {
        GameTick();
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
                cmp.Update();
        }
    }

    protected virtual void OnMousePressed(MouseEvent @event)
    {
    }

    protected virtual void OnMouseReleased(MouseEvent @event)
    {
    }

    protected virtual void OnMouseDragged(MouseDragEvent @event)
    {
    }

    protected virtual void OnMouseScrolled(MouseWheelEvent @event)
    {
    }

    protected virtual void OnMouseMoved(MouseMoveEvent @event)
    {
    }

    protected virtual void OnMouseEntered(MouseMoveEvent @event)
    {
    }

    protected virtual void OnMouseLeft(MouseMoveEvent @event)
    {
    }

    protected virtual void OnKeyTyped(KeyboardTypingEvent @event)
    {
    }

    public void DispatchMouseMoved(BaseMouseMoveEvent @event)
    {
        if (ClipRect is { } clip && !clip.Contains(@event.Position.X, @event.Position.Y))
            return;

        if (@event.Position.X > LayoutPaddingPosition.X && @event.Position.Y > LayoutPaddingPosition.Y && @event.Position.X < LayoutPaddingPosition.X + LayoutPaddingSize.X && @event.Position.Y < LayoutPaddingPosition.Y + LayoutPaddingSize.Y)
        {
            var relativeEvent = new MouseMoveEvent(
                Position: @event.Position,
                Buttons: @event.Buttons,
                CtrlKey: @event.CtrlKey,
                MetaKey: @event.AltKey,
                ShiftKey: @event.ShiftKey,
                RelativePosition: @event.Position - LayoutPaddingPosition
            );
            MouseMoved?.Invoke(relativeEvent);
            OnMouseMoved(relativeEvent);
        }
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
                cmp.DispatchMouseMoved(@event);
        }
    }

    /// <summary>
    /// Marks this node as hovered and fires its MouseEntered event / callback.
    /// Self-only: it does NOT propagate to children. Hover propagation is
    /// driven by <see cref="FocusManager.DispatchMouseMove"/>, which hit-tests
    /// the exact ancestor chain under the cursor, so recursing here would
    /// spuriously hover every sibling/descendant (e.g. all menu buttons).
    /// </summary>
    public void DispatchMouseEntered(BaseMouseMoveEvent @event)
    {
        IsHovered = true;
        var relativeEvent = new MouseMoveEvent(
            Position: @event.Position,
            Buttons: @event.Buttons,
            CtrlKey: @event.CtrlKey,
            MetaKey: @event.AltKey,
            ShiftKey: @event.ShiftKey,
            RelativePosition: @event.Position - LayoutPaddingPosition
        );
        MouseEntered?.Invoke(relativeEvent);
        OnMouseEntered(relativeEvent);
    }

    /// <summary>
    /// Marks this node as no longer hovered and fires its MouseLeft event /
    /// callback. Self-only — see <see cref="DispatchMouseEntered"/>.
    /// </summary>
    public void DispatchMouseLeft(BaseMouseMoveEvent @event)
    {
        IsHovered = false;
        var relativeEvent = new MouseMoveEvent(
            Position: @event.Position,
            Buttons: @event.Buttons,
            CtrlKey: @event.CtrlKey,
            MetaKey: @event.AltKey,
            ShiftKey: @event.ShiftKey,
            RelativePosition: @event.Position - LayoutPaddingPosition
        );
        MouseLeft?.Invoke(relativeEvent);
        OnMouseLeft(relativeEvent);
    }

    public void DispatchMousePressed(BaseMouseEvent @event)
    {
        // Occlusion-aware: only the topmost hit-test chain under the cursor (the deepest
        // element + its ancestors) receives the press. The previous implementation walked
        // the whole tree and fired EVERY element whose bounds contained the point, so
        // clicking a z-indexed popup (e.g. a dropdown option) ALSO triggered an occluded
        // element drawn beneath it (e.g. a dropdown whose trigger sat under the popup).
        // Browsers only target the topmost element — matching focus/hover, which already
        // dispatch through FocusManager.HitTestChain.
        var chain = FocusManager.HitTestChain(this, @event.Position);
        foreach (var node in chain)
        {
            if (!IsPointWithinPadding(node, @event.Position))
                continue;
            var relativeEvent = new MouseEvent(
                Position: @event.Position,
                Button: @event.Button,
                Buttons: @event.Buttons,
                CtrlKey: @event.CtrlKey,
                MetaKey: @event.AltKey,
                ShiftKey: @event.ShiftKey,
                RelativePosition: @event.Position - node.LayoutPaddingPosition
            );
            node.MousePressed?.Invoke(relativeEvent);
            node.OnMousePressed(relativeEvent);
        }
    }

    public void DispatchMouseReleased(BaseMouseEvent @event)
    {
        // Occlusion-aware, like DispatchMousePressed (see above): only the topmost
        // hit-test chain under the cursor receives the release.
        var chain = FocusManager.HitTestChain(this, @event.Position);
        foreach (var node in chain)
        {
            if (!IsPointWithinPadding(node, @event.Position))
                continue;
            var relativeEvent = new MouseEvent(
                Position: @event.Position,
                Button: @event.Button,
                Buttons: @event.Buttons,
                CtrlKey: @event.CtrlKey,
                MetaKey: @event.AltKey,
                ShiftKey: @event.ShiftKey,
                RelativePosition: @event.Position - node.LayoutPaddingPosition
            );
            node.MouseReleased?.Invoke(relativeEvent);
            node.OnMouseReleased(relativeEvent);
        }
    }

    private static bool IsPointWithinPadding(Component node, LuaVector2 pos)
    {
        var p = node.LayoutPaddingPosition;
        var s = node.LayoutPaddingSize;
        return pos.X > p.X && pos.Y > p.Y && pos.X < p.X + s.X && pos.Y < p.Y + s.Y;
    }

    public void DispatchMouseDragged(BaseMouseDragEvent @event)
    {
        if (ClipRect is { } clip && !clip.Contains(@event.DragStart.X, @event.DragStart.Y))
            return;

        if (@event.DragStart.X > LayoutPaddingPosition.X && @event.DragStart.Y > LayoutPaddingPosition.Y && @event.DragStart.X < LayoutPaddingPosition.X + LayoutPaddingSize.X && @event.DragStart.Y < LayoutPaddingPosition.Y + LayoutPaddingSize.Y)
        {
            var relativeEvent = new MouseDragEvent(
                DragStart: @event.DragStart,
                RelativeDragStart: @event.DragStart - LayoutPaddingPosition,
                Position: @event.Position,
                Button: @event.Button,
                Buttons: @event.Buttons,
                CtrlKey: @event.CtrlKey,
                MetaKey: @event.MetaKey,
                ShiftKey: @event.ShiftKey,
                RelativePosition: @event.Position - LayoutPaddingPosition
            );
            MouseDragged?.Invoke(relativeEvent);
            OnMouseDragged(relativeEvent);
        }
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
                cmp.DispatchMouseDragged(@event);
        }
    }

    public bool DispatchMouseScrolled(BaseMouseWheelEvent @event)
    {
        if (ClipRect is { } clip && !clip.Contains(@event.Position.X, @event.Position.Y))
            return false;

        // Deepest scrollable first: a descendant that scrolls consumes the event.
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp && cmp.DispatchMouseScrolled(@event))
                return true;
        }

        if (@event.Position.X > LayoutPaddingPosition.X && @event.Position.Y > LayoutPaddingPosition.Y && @event.Position.X < LayoutPaddingPosition.X + LayoutPaddingSize.X && @event.Position.Y < LayoutPaddingPosition.Y + LayoutPaddingSize.Y)
        {
            if (Styles.Overflow == Overflow.Scroll && TryScroll(@event.Delta.X, @event.Delta.Y))
                return true;

            var relativeEvent = new MouseWheelEvent(
                Delta: @event.Delta,
                Position: @event.Position,
                Buttons: @event.Buttons,
                CtrlKey: @event.CtrlKey,
                MetaKey: @event.MetaKey,
                ShiftKey: @event.ShiftKey,
                RelativePosition: @event.Position - LayoutPaddingPosition
            );
            MouseScrolled?.Invoke(relativeEvent);
            OnMouseScrolled(relativeEvent);
        }

        return false;
    }

    public virtual void OnKeyPressed(KeyboardEvent @event)
    {
    }

    public virtual void OnKeyReleased(KeyboardEvent @event)
    {
    }

    public void DispatchKeyPressed(KeyboardEvent @event)
    {
        if (IsFocusable && IsFocused)
        {
            KeyPressed?.Invoke(@event);
            OnKeyPressed(@event);
        }
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
                cmp.DispatchKeyPressed(@event);
        }
    }

    public void DispatchKeyReleased(KeyboardEvent @event)
    {
        if (IsFocusable && IsFocused)
        {
            KeyReleased?.Invoke(@event);
            OnKeyReleased(@event);
        }
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
                cmp.DispatchKeyReleased(@event);
        }
    }

    public void DispatchKeyTyped(KeyboardTypingEvent @event)
    {
        if (IsFocusable && IsFocused)
        {
            KeyTyped?.Invoke(@event);
            OnKeyTyped(@event);
        }
        foreach (var child in GetChildSnapshot())
        {
            if (child is Component cmp)
                cmp.DispatchKeyTyped(@event);
        }
    }
}

public struct MeasurementMarginPosition : IEquatable<MeasurementMarginPosition>
{
    internal YogaValue InternalValue;
    internal YogaUnit Unit => InternalValue.Unit;
    public float Value => InternalValue.Value;
    public float? PointValue => InternalValue.Unit == YogaUnit.Point ? InternalValue.Value : null;
    public float? PercentValue => InternalValue.Unit == YogaUnit.Percent ? InternalValue.Value : null;

    public bool Equals(MeasurementMarginPosition other) => InternalValue.Unit == other.InternalValue.Unit && InternalValue.Value == other.InternalValue.Value;
    public override bool Equals(object? obj) => obj is MeasurementMarginPosition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InternalValue.Unit, InternalValue.Value);

    public static implicit operator MeasurementMarginPosition(float Value)
    {
        return new MeasurementMarginPosition
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }
    public static implicit operator MeasurementMarginPosition(YogaValue Value)
    {
        return new MeasurementMarginPosition
        {
            InternalValue = Value
        };
    }
    public static implicit operator YogaValue(MeasurementMarginPosition Value)
    {
        return Value.InternalValue;
    }

    public static MeasurementMarginPosition Auto =>
        new()
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Auto
            }
        };

    public static MeasurementMarginPosition Undefined => new()
    {
        InternalValue = new YogaValue
        {
            Unit = YogaUnit.Undefined
        }
    };

    public static MeasurementMarginPosition Percent(float Value)
    {
        return new MeasurementMarginPosition
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Percent,
                Value = Value
            }
        };
    }
    public static MeasurementMarginPosition Point(float Value)
    {
        return new MeasurementMarginPosition
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }

    public MeasurementMarginPosition Scale(float scale)
    {
        if (InternalValue.Unit == YogaUnit.Point)
        {
            return Point(InternalValue.Value * scale);
        }

        return this;
    }

    public static MeasurementMarginPosition FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementMarginPosition(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }
        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return Auto;
        }
        if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
            {
                return Percent(percentValue);
            }
        }
        else if (trimmed.EndsWith("px"))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }
        else
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementMarginPosition. Expected 'auto', '<number>px', '<number>%', or '<number>'.");
    }
}

public struct MeasurementMultiMargin : IEquatable<MeasurementMultiMargin>
{
    public InlineArray4<MeasurementMarginPosition> Sides;

    public bool Equals(MeasurementMultiMargin other) => Sides[0].Equals(other.Sides[0]) && Sides[1].Equals(other.Sides[1]) && Sides[2].Equals(other.Sides[2]) && Sides[3].Equals(other.Sides[3]);
    public override bool Equals(object? obj) => obj is MeasurementMultiMargin other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Sides[0], Sides[1], Sides[2], Sides[3]);
    public MeasurementMarginPosition Top
    {
        get => Sides[0];
        set => Sides[0] = value;
    }
    public MeasurementMarginPosition Bottom
    {
        get => Sides[1];
        set => Sides[1] = value;
    }
    public MeasurementMarginPosition Left
    {
        get => Sides[2];
        set => Sides[2] = value;
    }
    public MeasurementMarginPosition Right
    {
        get => Sides[3];
        set => Sides[3] = value;
    }

    public static MeasurementMultiMargin Auto => MeasurementMarginPosition.Auto;

    public static MeasurementMultiMargin Undefined => MeasurementMarginPosition.Undefined;

    public static MeasurementMultiMargin All(MeasurementMarginPosition Value)
    {
        return new MeasurementMultiMargin
        {
            Top = Value,
            Bottom = Value,
            Left = Value,
            Right = Value
        };
    }

    public static implicit operator MeasurementMultiMargin(MeasurementMarginPosition Value) => All(Value);
    public static implicit operator MeasurementMultiMargin(float Value) => All(Value);

    public static MeasurementMultiMargin? XY(float x, float y)
    {
        return new MeasurementMultiMargin
        {
            Left = x,
            Right = x,
            Top = y,
            Bottom = y
        };
    }

    public static MeasurementMultiMargin FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementMultiMargin(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return All(MeasurementMarginPosition.Undefined);
        }
        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return All(MeasurementMarginPosition.Auto);
        }

        var idx = 0;
        var sides = new InlineArray4<MeasurementMarginPosition>();
        foreach (var elementRange in trimmed.SplitAny(',', ' '))
        {
            var element = trimmed[elementRange];

            if (element.EndsWith("%", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                {
                    sides[idx] = MeasurementMarginPosition.Percent(percentValue);
                }
            }
            else if (element.EndsWith("px"))
            {
                if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                {
                    sides[idx] = MeasurementMarginPosition.Point(pointValue);
                }
            }
            else
            {
                if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                {
                    sides[idx] = MeasurementMarginPosition.Point(pointValue);
                }
            }

            idx++;
        }

        if (idx == 1)
        {
            return All(sides[0]);
        }

        if (idx == 2)
        {
            return new MeasurementMultiMargin
            {
                Top = sides[0],
                Bottom = sides[0],
                Left = sides[1],
                Right = sides[1]
            };
        }

        if (idx == 4)
        {
            return new MeasurementMultiMargin
            {
                Top = sides[0],
                Right = sides[1],
                Bottom = sides[2],
                Left = sides[3]
            };
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementMultiMargin. Expected 'auto', '<number>px', '<number>%', or '<number>', as 1, 2 or 4 elements, in order top-right-bottom-left, separated by comma or space.");
    }
}

public struct MeasurementPadding : IEquatable<MeasurementPadding>
{
    internal YogaValue InternalValue;
    public YogaUnit Unit => InternalValue.Unit;
    public float Value => InternalValue.Value;
    public float? PointValue => InternalValue.Unit == YogaUnit.Point ? InternalValue.Value : null;
    public float? PercentValue => InternalValue.Unit == YogaUnit.Percent ? InternalValue.Value : null;

    public bool Equals(MeasurementPadding other) => InternalValue.Unit == other.InternalValue.Unit && InternalValue.Value == other.InternalValue.Value;
    public override bool Equals(object? obj) => obj is MeasurementPadding other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InternalValue.Unit, InternalValue.Value);

    public static implicit operator MeasurementPadding(float Value)
    {
        return new MeasurementPadding
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }
    public static implicit operator MeasurementPadding(YogaValue Value)
    {
        return new MeasurementPadding
        {
            InternalValue = Value
        };
    }
    public static implicit operator YogaValue(MeasurementPadding Value)
    {
        return Value.InternalValue;
    }

    public static MeasurementPadding Undefined => new()
    {
        InternalValue = new YogaValue
        {
            Unit = YogaUnit.Undefined
        }
    };

    public static MeasurementPadding Percent(float Value)
    {
        return new MeasurementPadding
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Percent,
                Value = Value
            }
        };
    }
    public static MeasurementPadding Point(float Value)
    {
        return new MeasurementPadding
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }

    public MeasurementPadding Scale(float scale)
    {
        if (InternalValue.Unit == YogaUnit.Point)
        {
            return Point(InternalValue.Value * scale);
        }

        return this;
    }

    public static MeasurementPadding FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementPadding(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }
        if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
            {
                return Percent(percentValue);
            }
        }
        else if (trimmed.EndsWith("px"))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }
        else
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementPadding. Expected '<number>px', '<number>%', or '<number>'.");
    }
}

public struct MeasurementMultiPadding : IEquatable<MeasurementMultiPadding>
{
    public InlineArray4<MeasurementPadding> Sides;

    public bool Equals(MeasurementMultiPadding other) => Sides[0].Equals(other.Sides[0]) && Sides[1].Equals(other.Sides[1]) && Sides[2].Equals(other.Sides[2]) && Sides[3].Equals(other.Sides[3]);
    public override bool Equals(object? obj) => obj is MeasurementMultiPadding other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Sides[0], Sides[1], Sides[2], Sides[3]);
    public MeasurementPadding Top
    {
        get => Sides[0];
        set => Sides[0] = value;
    }
    public MeasurementPadding Bottom
    {
        get => Sides[1];
        set => Sides[1] = value;
    }
    public MeasurementPadding Left
    {
        get => Sides[2];
        set => Sides[2] = value;
    }
    public MeasurementPadding Right
    {
        get => Sides[3];
        set => Sides[3] = value;
    }

    public static MeasurementMultiPadding Undefined => MeasurementPadding.Undefined;

    public static MeasurementMultiPadding All(MeasurementPadding Value)
    {
        return new MeasurementMultiPadding
        {
            Top = Value,
            Bottom = Value,
            Left = Value,
            Right = Value
        };
    }

    public static implicit operator MeasurementMultiPadding(MeasurementPadding Value) => All(Value);
    public static implicit operator MeasurementMultiPadding(float Value) => All(Value);

    public static MeasurementMultiPadding? XY(float x, float y)
    {
        return new MeasurementMultiPadding
        {
            Left = MeasurementPadding.Point(x),
            Right = MeasurementPadding.Point(x),
            Top = MeasurementPadding.Point(y),
            Bottom = MeasurementPadding.Point(y)
        };
    }

    public static MeasurementMultiPadding FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementMultiPadding(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return All(MeasurementPadding.Undefined);
        }

        var idx = 0;
        var sides = new InlineArray4<MeasurementPadding>();
        foreach (var elementRange in trimmed.SplitAny(',', ' '))
        {
            var element = trimmed[elementRange];

            if (element.EndsWith("%", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
                {
                    sides[idx] = MeasurementPadding.Percent(percentValue);
                }
            }
            else if (element.EndsWith("px"))
            {
                if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                {
                    sides[idx] = MeasurementPadding.Point(pointValue);
                }
            }
            else
            {
                if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                {
                    sides[idx] = MeasurementPadding.Point(pointValue);
                }
            }

            idx++;
        }

        if (idx == 1)
        {
            return All(sides[0]);
        }

        if (idx == 2)
        {
            return new MeasurementMultiPadding
            {
                Top = sides[0],
                Bottom = sides[0],
                Left = sides[1],
                Right = sides[1]
            };
        }

        if (idx == 4)
        {
            return new MeasurementMultiPadding
            {
                Top = sides[0],
                Right = sides[1],
                Bottom = sides[2],
                Left = sides[3]
            };
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementMultiMargin. Expected '<number>px', '<number>%', or '<number>', as 1, 2 or 4 elements, in order top-right-bottom-left, separated by comma or space.");

    }
}

public struct MeasurementMultiBorder : IEquatable<MeasurementMultiBorder>
{
    public InlineArray4<float?> Sides;

    public bool Equals(MeasurementMultiBorder other) => Nullable.Equals(Sides[0], other.Sides[0]) && Nullable.Equals(Sides[1], other.Sides[1]) && Nullable.Equals(Sides[2], other.Sides[2]) && Nullable.Equals(Sides[3], other.Sides[3]);
    public override bool Equals(object? obj) => obj is MeasurementMultiBorder other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Sides[0], Sides[1], Sides[2], Sides[3]);
    public float? Top
    {
        get => Sides[0];
        set => Sides[0] = value;
    }
    public float? Bottom
    {
        get => Sides[1];
        set => Sides[1] = value;
    }
    public float? Left
    {
        get => Sides[2];
        set => Sides[2] = value;
    }
    public float? Right
    {
        get => Sides[3];
        set => Sides[3] = value;
    }

    public static MeasurementMultiBorder Undefined => All(null);

    public static MeasurementMultiBorder All(float? Value)
    {
        return new MeasurementMultiBorder
        {
            Top = Value,
            Bottom = Value,
            Left = Value,
            Right = Value
        };
    }

    public static implicit operator MeasurementMultiBorder(float? Value) => All(Value);
    public static MeasurementMultiBorder XY(float? x, float? y)
    {
        return new MeasurementMultiBorder
        {
            Left = x,
            Right = x,
            Top = y,
            Bottom = y
        };
    }

    public static MeasurementMultiBorder FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementMultiBorder(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }

        var idx = 0;
        var sides = new InlineArray4<float>();
        foreach (var elementRange in trimmed.SplitAny(',', ' '))
        {
            var element = trimmed[elementRange];

            if (element.EndsWith("px"))
            {
                if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                {
                    sides[idx] = pointValue;
                }
            }
            else
            {
                if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
                {
                    sides[idx] = pointValue;
                }
            }

            idx++;
        }

        if (idx == 1)
        {
            return All(sides[0]);
        }

        if (idx == 2)
        {
            return new MeasurementMultiBorder
            {
                Top = sides[0],
                Bottom = sides[0],
                Left = sides[1],
                Right = sides[1]
            };
        }

        if (idx == 4)
        {
            return new MeasurementMultiBorder
            {
                Top = sides[0],
                Right = sides[1],
                Bottom = sides[2],
                Left = sides[3]
            };
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementMultiMargin. Expected '<number>px' or '<number>', as 1, 2 or 4 elements, in order top-right-bottom-left, separated by comma or space.");
    }
}

public struct MeasurementGap : IEquatable<MeasurementGap>
{
    internal YogaValue InternalValue;
    public YogaUnit Unit => InternalValue.Unit;
    public float Value => InternalValue.Value;
    public float? PointValue => InternalValue.Unit == YogaUnit.Point ? InternalValue.Value : null;
    public float? PercentValue => InternalValue.Unit == YogaUnit.Percent ? InternalValue.Value : null;

    public bool Equals(MeasurementGap other) => InternalValue.Unit == other.InternalValue.Unit && InternalValue.Value == other.InternalValue.Value;
    public override bool Equals(object? obj) => obj is MeasurementGap other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InternalValue.Unit, InternalValue.Value);

    public static implicit operator MeasurementGap(float Value)
    {
        return new MeasurementGap
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }
    public static implicit operator MeasurementGap(YogaValue Value)
    {
        return new MeasurementGap
        {
            InternalValue = Value
        };
    }
    public static implicit operator YogaValue(MeasurementGap Value)
    {
        return Value.InternalValue;
    }

    public static MeasurementGap Undefined => new()
    {
        InternalValue = new YogaValue
        {
            Unit = YogaUnit.Undefined
        }
    };

    public static MeasurementGap Percent(float Value)
    {
        return new MeasurementGap
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Percent,
                Value = Value
            }
        };
    }
    public static MeasurementGap Point(float Value)
    {
        return new MeasurementGap
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }

    public MeasurementGap Scale(float scale)
    {
        if (InternalValue.Unit == YogaUnit.Point)
        {
            return Point(InternalValue.Value * scale);
        }

        return this;
    }

    public static MeasurementGap FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementGap(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }
        if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
            {
                return Percent(percentValue);
            }
        }
        else if (trimmed.EndsWith("px"))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }
        else
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementGap. Expected '<number>px', '<number>%', or '<number>'.");
    }

    // ReSharper disable once CompareOfFloatsByEqualityOperator
    public static bool operator ==(MeasurementGap left, MeasurementGap right) => left.Unit == right.Unit && left.Value == right.Value;
    public static bool operator !=(MeasurementGap left, MeasurementGap right) => !(left == right);
}

public readonly struct Pixels(float Value) : IEquatable<Pixels>
{
    public readonly float Value = Value;

    public bool Equals(Pixels other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Pixels other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator float(Pixels Value) => Value.Value;
    public static implicit operator Pixels(float Value) => new(Value);

    public static Pixels FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator Pixels(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return new Pixels(float.NaN);
        }
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        if (trimmed.EndsWith("px"))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return pointValue;
            }
        }
        else
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return pointValue;
            }
        }

        throw new FormatException($"Cannot convert '{str}' to pixels or undefined.");
    }
}

public struct MeasurementFlexBasis : IEquatable<MeasurementFlexBasis>
{
    internal YogaValue InternalValue;
    public YogaUnit Unit => InternalValue.Unit;
    public float Value => InternalValue.Value;
    public float? PointValue => InternalValue.Unit == YogaUnit.Point ? InternalValue.Value : null;
    public float? PercentValue => InternalValue.Unit == YogaUnit.Percent ? InternalValue.Value : null;

    public bool Equals(MeasurementFlexBasis other) => InternalValue.Unit == other.InternalValue.Unit && InternalValue.Value == other.InternalValue.Value;
    public override bool Equals(object? obj) => obj is MeasurementFlexBasis other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InternalValue.Unit, InternalValue.Value);

    public static implicit operator MeasurementFlexBasis(float Value)
    {
        return new MeasurementFlexBasis
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }
    public static implicit operator MeasurementFlexBasis(YogaValue Value)
    {
        return new MeasurementFlexBasis
        {
            InternalValue = Value
        };
    }
    public static implicit operator YogaValue(MeasurementFlexBasis Value)
    {
        return Value.InternalValue;
    }

    public static MeasurementFlexBasis Undefined = new()
    {
        InternalValue = new YogaValue
        {
            Unit = YogaUnit.Undefined
        }
    };

    public static MeasurementFlexBasis Auto =>
        new()
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.FitContent
            }
        };

    public static MeasurementFlexBasis MaxContent =>
        new()
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.MaxContent
            }
        };

    public static MeasurementFlexBasis Stretch =>
        new()
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Stretch
            }
        };

    public static MeasurementFlexBasis Percent(float Value)
    {
        return new MeasurementFlexBasis
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Percent,
                Value = Value
            }
        };
    }

    public static MeasurementFlexBasis Point(float Value)
    {
        return new MeasurementFlexBasis
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }

    public static MeasurementFlexBasis FitContent =>
        new()
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.FitContent
            }
        };

    public MeasurementFlexBasis Scale(float scale)
    {
        if (InternalValue.Unit == YogaUnit.Point)
        {
            return Point(InternalValue.Value * scale);
        }

        return this;
    }

    public static MeasurementFlexBasis FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementFlexBasis(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();
        if (trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return Undefined;
        }
        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return Auto;
        }
        if (trimmed.Equals("max-content", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("maxcontent", StringComparison.OrdinalIgnoreCase))
        {
            return MaxContent;
        }
        if (trimmed.Equals("stretch", StringComparison.OrdinalIgnoreCase))
        {
            return Stretch;
        }
        if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
            {
                return Percent(percentValue);
            }
        }
        else if (trimmed.EndsWith("px"))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }
        else
        {
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pointValue))
            {
                return Point(pointValue);
            }
        }

        throw new FormatException($"Cannot convert '{str}' to MeasurementFlexBasis. Expected 'auto', 'max-content', 'stretch', '<number>px', '<number>%', or '<number>'.");
    }
}

public struct MeasurementWidthHeight : IEquatable<MeasurementWidthHeight>
{
    internal YogaValue InternalValue;
    public YogaUnit Unit => InternalValue.Unit;
    public float Value => InternalValue.Value;
    public float? PointValue => InternalValue.Unit == YogaUnit.Point ? InternalValue.Value : null;
    public float? PercentValue => InternalValue.Unit == YogaUnit.Percent ? InternalValue.Value : null;

    public bool Equals(MeasurementWidthHeight other) => InternalValue.Unit == other.InternalValue.Unit && InternalValue.Value == other.InternalValue.Value;
    public override bool Equals(object? obj) => obj is MeasurementWidthHeight other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InternalValue.Unit, InternalValue.Value);

    public static implicit operator MeasurementWidthHeight(float Value)
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }
    public static implicit operator MeasurementWidthHeight(YogaValue Value)
    {
        return new MeasurementWidthHeight
        {
            InternalValue = Value
        };
    }
    public static implicit operator YogaValue(MeasurementWidthHeight Value)
    {
        return Value.InternalValue;
    }

    public static MeasurementWidthHeight Undefined => new()
    {
        InternalValue = new YogaValue
        {
            Unit = YogaUnit.Undefined
        }
    };

    public static MeasurementWidthHeight Auto()
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Auto
            }
        };
    }
    public static MeasurementWidthHeight Percent(float Value)
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Percent,
                Value = Value
            }
        };
    }
    public static MeasurementWidthHeight Point(float Value)
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Point,
                Value = Value
            }
        };
    }

    public static MeasurementWidthHeight FitContent()
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.FitContent
            }
        };
    }
    public static MeasurementWidthHeight MaxContent()
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.MaxContent
            }
        };
    }

    public static MeasurementWidthHeight Stretch()
    {
        return new MeasurementWidthHeight
        {
            InternalValue = new YogaValue
            {
                Unit = YogaUnit.Stretch
            }
        };
    }

    public MeasurementWidthHeight Scale(float scale)
    {
        if (InternalValue.Unit == YogaUnit.Point)
        {
            return Point(InternalValue.Value * scale);
        }

        return this;
    }

    public static MeasurementWidthHeight FromString(ReadOnlySpan<char> str) => str;

    public static implicit operator MeasurementWidthHeight(ReadOnlySpan<char> str)
    {
        var trimmed = str.Trim();

        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return Auto();
        }
        if (trimmed.Equals("stretch", StringComparison.OrdinalIgnoreCase))
        {
            return Stretch();
        }
        if (trimmed.Equals("fit-content", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("fitcontent", StringComparison.OrdinalIgnoreCase))
        {
            return FitContent();
        }
        if (trimmed.Equals("max-content", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("maxcontent", StringComparison.OrdinalIgnoreCase))
        {
            return MaxContent();
        }
        if (trimmed.EndsWith('%'))
        {
            if (float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percentValue))
            {
                return Percent(percentValue);
            }
        }
        else if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                return Point(floatValue);
            }
        }
        else if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            return Point(floatValue);
        }

        throw new FormatException($"Cannot convert {str} to MeasurementWidthHeight. Expected a number, percentage, 'auto', 'stretch', 'fit-content', or 'max-content'.");
    }
}