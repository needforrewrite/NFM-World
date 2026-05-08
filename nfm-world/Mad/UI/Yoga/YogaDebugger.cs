using Maxine.Extensions;
using NFMWorld.DriverInterface;
using NFMWorld.Util;

namespace NFMWorld.UI.Yoga;

public static class YogaDebugger
{
    private static Vector2 _mousePosition;
    private static string slnDir = ProjectUtils.TryGetSolutionDirectory() ?? Directory.GetCurrentDirectory();

    public const int MaxPages = 2;

    public static void Render(int page = 0)
    {
        if (page == 0)
            RenderPage1();
        else if (page == 1)
            RenderPage2();
        else if (page == 2)
            RenderPage3();
        
        // draw two lines intersecting the mouse position
        G.SetColor(Color.Magenta);
        G.DrawLine(0, (int)_mousePosition.Y, (int)G.Viewport.X, (int)_mousePosition.Y);
        G.DrawLine((int)_mousePosition.X, 0, (int)_mousePosition.X, (int)G.Viewport.Y);
        // draw mouse position text
        G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 16));
        var mousePosText = $"Mouse: ({(int)_mousePosition.X}, {(int)_mousePosition.Y})";
        G.SetColor(Color.White);
        G.DrawStringStroke(mousePosText, (int)_mousePosition.X + 12, (int)_mousePosition.Y + 12);
        G.SetColor(Color.Magenta);
        G.DrawString(mousePosText, (int)_mousePosition.X + 12, (int)_mousePosition.Y + 12);
    }

    private static void RenderPage3()
    {
        // draw a tree of all elements and their layout info in the top-left corner,
        // with a gradient from red to yellow based on depth
        var maxDepth = 0;
        foreach (var root in Node.__INTERNAL_YogaRootsThisFrame)
        {
            maxDepth = Math.Max(maxDepth, GetMaxDepth(root, 0));
            continue;

            static int GetMaxDepth(Node node, int depth)
            {
                var childMax = depth;
                if (node is Box box)
                {
                    foreach (var child in box.Children)
                    {
                        childMax = Math.Max(childMax, GetMaxDepth(child, depth + 1));
                    }
                }

                return childMax;
            }
        }

        var y = 24;
        foreach (var root in Node.__INTERNAL_YogaRootsThisFrame)
        {
            DrawElementAndChildren(root, y, 0);
            y += 24 * (1 + (root is Box box ? GetChildCount(box) : 0));
        }

        return;

        void DrawElementAndChildren(Node node, int y = 15, int depth = 0)
        {
            var color = Color.Lerp(Color.Red, Color.Yellow, depth / (float)maxDepth);
            G.SetColor(color);
            G.DrawRect(
                (int)node.LayoutBorderPosition.X,
                (int)node.LayoutBorderPosition.Y,
                (int)node.LayoutBorderSize.X,
                (int)node.LayoutBorderSize.Y
            );

            var layoutInfo = $"""
                              {node.Name ?? ""}[{node.GetType().Name}] {node.LayoutBorderSize.X}px x {node.LayoutBorderSize.Y}px at ({node.LayoutBorderPosition.X}, {node.LayoutBorderPosition.Y})
                              """;
            G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 24));
            var indent = new string(' ', depth * 2);
            G.SetColor(Color.White);
            G.DrawStringStroke(indent + layoutInfo, 12, y);
            G.SetColor(color);
            G.DrawString(indent + layoutInfo, 12, y);
            y += 24;
            if (node is Box box)
            {
                foreach (var child in box.Children)
                {
                    DrawElementAndChildren(child, y, depth + 1);
                    y += 24 * (1 + (child is Box childBox ? GetChildCount(childBox) : 0));
                }
            }
        }
        
        static int GetChildCount(Box child)
        {
            var count = child.Children.Count;
            foreach (var grandChild in child.Children)
            {
                if (grandChild is Box box)
                    count += GetChildCount(box);
            }
            return count;
        }
    }

    private static void RenderPage2()
    {
        // draw an outline around every element with a gradient from red to yellow based on depth
        var maxDepth = 0;
        foreach (var root in Node.__INTERNAL_YogaRootsThisFrame)
        {
            maxDepth = Math.Max(maxDepth, GetMaxDepth(root, 0));
            continue;

            static int GetMaxDepth(Node node, int depth)
            {
                var childMax = depth;
                if (node is Box box)
                {
                    foreach (var child in box.Children)
                    {
                        childMax = Math.Max(childMax, GetMaxDepth(child, depth + 1));
                    }
                }

                return childMax;
            }
        }
        
        foreach (var root in Node.__INTERNAL_YogaRootsThisFrame)
        {
            DrawNodeAndChildren(root, 0);
            continue;

            void DrawNodeAndChildren(Node node, int depth)
            {
                var color = Color.Lerp(Color.Red, Color.Yellow, depth / (float)maxDepth);
                G.SetColor(color);
                G.DrawRect(
                    (int)node.LayoutBorderPosition.X,
                    (int)node.LayoutBorderPosition.Y,
                    (int)node.LayoutBorderSize.X,
                    (int)node.LayoutBorderSize.Y
                );
                
                // draw text + outline with element name
                G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 24));
                var info = $"{node.Name ?? ""}[{node.GetType().Name}]";
                G.SetColor(Color.White);
                G.DrawStringStroke(info, (int)node.LayoutBorderPosition.X, (int)node.LayoutBorderPosition.Y - 12);
                G.SetColor(color);
                G.DrawString(info, (int)node.LayoutBorderPosition.X, (int)node.LayoutBorderPosition.Y - 12);

                if (node is Box box)
                {
                    foreach (var child in box.Children)
                    {
                        DrawNodeAndChildren(child, depth + 1);
                    }
                }
            }
        }
    }

    private static void RenderPage1()
    {
        var mouseOverNodeTree = Node.__INTERNAL_YogaRootsThisFrame
            .Select(FindMouseOverNodeTree)
            .MaxBy(n => n.Length);
        if (mouseOverNodeTree?.Length > 0)
        {
            for (int i = 0; i < mouseOverNodeTree.Length; i++)
            {
                var node = mouseOverNodeTree[i];
                var color = Color.Lerp(Color.Red, Color.Yellow, i / (float)mouseOverNodeTree.Length);
                G.SetColor(color);
                G.DrawRect(
                    (int)node.LayoutBorderPosition.X,
                    (int)node.LayoutBorderPosition.Y,
                    (int)node.LayoutBorderSize.X,
                    (int)node.LayoutBorderSize.Y
                );
                
                G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 20));
                var info = $"Node: {node.Name ?? ""}[{node.GetType().Name}] from {(node.__INTERNAL_CtorCallerFilePath != "" ? Path.GetRelativePath(slnDir, node.__INTERNAL_CtorCallerFilePath) : "")}:{node.__INTERNAL_CtorCallerMemberName}:{node.__INTERNAL_CtorCallerLineNumber}";
                var prefix = new string(' ', i * 2);
                
                G.SetColor(Color.White);
                G.DrawStringStroke(prefix + info, 12, 24 + (i * 24));
                G.SetColor(color);
                G.DrawString(prefix + info, 12, 24 + (i * 24));
            }
            
            var lastEntry = mouseOverNodeTree[^1];
            G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 16));
            var layoutInfo = $"""
                              Layout:
                              Margin: {lastEntry.LayoutMarginSize.X}px x {lastEntry.LayoutMarginSize.Y}px at ({lastEntry.LayoutMarginPosition.X}, {lastEntry.LayoutMarginPosition.Y})
                              Border: {lastEntry.LayoutBorderSize.X}px x {lastEntry.LayoutBorderSize.Y}px at ({lastEntry.LayoutBorderPosition.X}, {lastEntry.LayoutBorderPosition.Y})
                              Padding: {lastEntry.LayoutPaddingSize.X}px x {lastEntry.LayoutPaddingSize.Y}px at ({lastEntry.LayoutPaddingPosition.X}, {lastEntry.LayoutPaddingPosition.Y})
                              Content: {lastEntry.LayoutContentSize.X}px x {lastEntry.LayoutContentSize.Y}px at ({lastEntry.LayoutContentPosition.X}, {lastEntry.LayoutContentPosition.Y})
                              """;
            G.SetColor(Color.White);
            G.DrawStringStroke(layoutInfo, 12, 24 + (mouseOverNodeTree.Length * 24));
            G.SetColor(Color.Cyan);
            G.DrawString(layoutInfo, 12, 24 + (mouseOverNodeTree.Length * 24));
            
            // draw margin, padding, border, content
            G.SetColor(Color.Gray with { A = 128 });
            FillRectExceptForRect(
                new RectangleF(
                    lastEntry.LayoutMarginPosition.X,
                    lastEntry.LayoutMarginPosition.Y,
                    lastEntry.LayoutMarginSize.X,
                    lastEntry.LayoutMarginSize.Y
                ),
                new RectangleF(
                    lastEntry.LayoutBorderPosition.X,
                    lastEntry.LayoutBorderPosition.Y,
                    lastEntry.LayoutBorderSize.X,
                    lastEntry.LayoutBorderSize.Y
                )
            );
            G.SetColor(Color.Yellow with { A = 128 });
            FillRectExceptForRect(
                new RectangleF(
                    lastEntry.LayoutBorderPosition.X,
                    lastEntry.LayoutBorderPosition.Y,
                    lastEntry.LayoutBorderSize.X,
                    lastEntry.LayoutBorderSize.Y
                ),
                new RectangleF(
                    lastEntry.LayoutPaddingPosition.X,
                    lastEntry.LayoutPaddingPosition.Y,
                    lastEntry.LayoutPaddingSize.X,
                    lastEntry.LayoutPaddingSize.Y
                )
            );
            G.SetColor(Color.Green with { A = 128 });
            FillRectExceptForRect(
                new RectangleF(
                    lastEntry.LayoutPaddingPosition.X,
                    lastEntry.LayoutPaddingPosition.Y,
                    lastEntry.LayoutPaddingSize.X,
                    lastEntry.LayoutPaddingSize.Y
                ),
                new RectangleF(
                    lastEntry.LayoutContentPosition.X,
                    lastEntry.LayoutContentPosition.Y,
                    lastEntry.LayoutContentSize.X,
                    lastEntry.LayoutContentSize.Y
                )
            );
            G.SetColor(Color.Blue with { A = 128 });
            G.FillRect(
                (int)lastEntry.LayoutContentPosition.X,
                (int)lastEntry.LayoutContentPosition.Y,
                (int)lastEntry.LayoutContentSize.X,
                (int)lastEntry.LayoutContentSize.Y
            );
            
            // draw layout box in the corner with labels
            const int scale = 48;
            var margin = new RectangleF(
                (int)G.Viewport.X - 420,
                12,
                416,
                416
            );
            var border = new RectangleF(
                margin.X + scale,
                margin.Y + scale,
                margin.Width - (scale * 2),
                margin.Height - (scale * 2)
            );
            var padding = new RectangleF(
                border.X + scale,
                border.Y + scale,
                border.Width - (scale * 2),
                border.Height - (scale * 2)
            );
            var content = new RectangleF(
                padding.X + scale,
                padding.Y + scale,
                padding.Width - (scale * 2),
                padding.Height - (scale * 2)
            );
            
            DrawBoxWithLabels(
                Color.Gray with { A = 128 },
                "margin",
                margin, border,
                lastEntry.LayoutMarginTop,
                lastEntry.LayoutMarginRight,
                lastEntry.LayoutMarginBottom,
                lastEntry.LayoutMarginLeft
            );
            DrawBoxWithLabels(
                Color.Yellow with { A = 128 },
                "border",
                border, padding,
                lastEntry.LayoutBorderTop,
                lastEntry.LayoutBorderRight,
                lastEntry.LayoutBorderBottom,
                lastEntry.LayoutBorderLeft
            );
            DrawBoxWithLabels(
                Color.Green with { A = 128 },
                "padding",
                padding, content,
                lastEntry.LayoutPaddingTop,
                lastEntry.LayoutPaddingRight,
                lastEntry.LayoutPaddingBottom,
                lastEntry.LayoutPaddingLeft
            );
            
            // draw centred widthxheight in middle box
            G.SetColor(Color.Blue with { A = 128 });
            G.FillRect((int)content.X, (int)content.Y, (int)content.Width, (int)content.Height);
            
            G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 16));
            G.SetColor(Color.Black);
            G.DrawStringStroke("content", (int)content.X, (int)content.Y + 16);
            G.SetColor(Color.White);
            G.DrawString("content", (int)content.X, (int)content.Y + 16);

            G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 16));
            var contentSizeText = $"{(int)lastEntry.LayoutContentSize.X}px x {(int)lastEntry.LayoutContentSize.Y}px";
            G.SetColor(Color.Black);
            G.DrawStringStrokeAligned(
                contentSizeText,
                (int)content.X,
                (int)content.Y,
                (int)content.Width,
                (int)content.Height,
                TextHorizontalAlignment.Center,
                TextVerticalAlignment.Center
            );
            G.SetColor(Color.White);
            G.DrawStringAligned(
                contentSizeText,
                (int)content.X,
                (int)content.Y,
                (int)content.Width,
                (int)content.Height,
                TextHorizontalAlignment.Center,
                TextVerticalAlignment.Center
            );
        }
    }

    private static void DrawBoxWithLabels(Color color, string label, RectangleF area, RectangleF? inner, float top, float right, float bottom, float left)
    {
        G.SetColor(color);
        if (inner is {} innerRect)
        {
            FillRectExceptForRect(area, innerRect);
        }
        else
        {
            G.FillRect((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        }
        
        G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 16));
        G.SetColor(Color.Black);
        G.DrawStringStroke(label, (int)area.X, (int)area.Y + 16);
        G.SetColor(Color.White);
        G.DrawString(label, (int)area.X, (int)area.Y + 16);

        // Top
        G.SetColor(Color.Black);
        G.DrawStringStrokeAligned($"{top:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Center);
        G.SetColor(Color.White);
        G.DrawStringAligned($"{top:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Center);

        // Right
        G.SetColor(Color.Black);
        G.DrawStringStrokeAligned($"{right:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Right, TextVerticalAlignment.Center);
        G.SetColor(Color.White);
        G.DrawStringAligned($"{right:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Right, TextVerticalAlignment.Center);

        // Bottom
        G.SetColor(Color.Black);
        G.DrawStringStrokeAligned($"{bottom:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Center, TextVerticalAlignment.Bottom);
        G.SetColor(Color.White);
        G.DrawStringAligned($"{bottom:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Center, TextVerticalAlignment.Bottom);

        // Left
        G.SetColor(Color.Black);
        G.DrawStringStrokeAligned($"{left:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Left, TextVerticalAlignment.Center);
        G.SetColor(Color.White);
        G.DrawStringAligned($"{left:0.00}px", (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height, TextHorizontalAlignment.Left, TextVerticalAlignment.Center);
    }

    private static void FillRectExceptForRect(RectangleF outer, RectangleF inner)
    {
        // Top
        G.FillRect(
            (int)outer.X,
            (int)outer.Y,
            (int)outer.Width,
            (int)(inner.Y - outer.Y)
        );
        // Bottom
        G.FillRect(
            (int)outer.X,
            (int)(inner.Y + inner.Height),
            (int)outer.Width,
            (int)(outer.Y + outer.Height - (inner.Y + inner.Height))
        );
        // Left
        G.FillRect(
            (int)outer.X,
            (int)inner.Y,
            (int)(inner.X - outer.X),
            (int)inner.Height
        );
        // Right
        G.FillRect(
            (int)(inner.X + inner.Width),
            (int)inner.Y,
            (int)(outer.X + outer.Width - (inner.X + inner.Width)),
            (int)inner.Height
        );
    }

    private static Node[] FindMouseOverNodeTree(Node node)
    {
        // Depth-first search for the node whose border box contains the mouse position, and its parents
        var rect = new RectangleF(
            node.LayoutMarginPosition.X,
            node.LayoutMarginPosition.Y,
            node.LayoutMarginSize.X,
            node.LayoutMarginSize.Y
        );
        if (rect.Contains(_mousePosition))
        {
            if (node is Box box)
            {
                foreach (var child in box.Children)
                {
                    var childResult = FindMouseOverNodeTree(child);
                    if (childResult.Length > 0)
                    {
                        return [node, ..childResult];
                    }
                }
            }

            return [node];
        }
        return [];
    }

    public static void MouseMove(int x, int y)
    {
        _mousePosition = new Vector2(x, y);
    }
}