using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary;

namespace NFMWorld.UI;

/// <summary>
/// Builds the Yoga UI test scene: a scrollable list (overflow: scroll with a
/// scrollbar), a nested overflow:hidden clip, and interactive rows that
/// demonstrate hover/press hit-testing.
/// </summary>
public static class TestView
{
    public static View Render(ref int counter)
    {
        // Text bindings
        string title = "XAML Test View";
        string subtitle = "All systems operational";

        // Color bindings
        Color titleColor = new Color(255, 255, 255, 255);
        Color subtitleColor = new Color(180, 180, 180, 255);
        Color accentColor = new Color(100, 200, 255, 255);

        // Font bindings
        Font titleFont = new Font(FontFamily.Adventure, FontStyle.Bold, 32);
        Font bodyFont = new Font(FontFamily.Adventure, FontStyle.Plain, 18);

        // Visibility binding
        Visibility badgeVisibility = Visibility.Visible;

        var counterModulo = counter % 63;

        return new View()
        {
            Name = "YogaTest",
            Styles = Sx.Styles(
                flexDirection: FlexDirection.Column,
                alignItems: Align.FlexStart,
                justifyContent: Justify.FlexStart,
                gapRow: 16f,
                gapColumn: 16f,
                paddingTop: 20f,
                paddingBottom: 20f,
                paddingLeft: 20f,
                paddingRight: 20f
            ),
            Children =
            {
                Section(
                    $"Counter: {counter} — scroll the list and click rows",
                    font: new Font(FontFamily.Adventure, FontStyle.Bold, 18),
                    color: new Color(255, 255, 255, 255)
                ),

                ScrollList(),

                Section(
                    "Nested overflow: hidden (child is wider than the box)",
                    font: new Font(FontFamily.Adventure, FontStyle.Plain, 16),
                    color: new Color(180, 180, 180, 255)
                ),

                new View()
                {
                    Name = "ClippedBox",
                    Styles = Sx.Styles(
                        width: 200f,
                        height: 80f,
                        overflow: Overflow.Hidden,
                        backgroundColor: new Color(30, 40, 60, 255),
                        borderColor: new Color(100, 200, 255, 255),
                        borderTop: 2f,
                        borderBottom: 2f,
                        borderLeft: 2f,
                        borderRight: 2f,
                        borderRadius: 8f
                    ),
                    Children =
                    {
                        new View()
                        {
                            Styles = Sx.Styles(
                                width: 300f,
                                height: 40f,
                                backgroundColor: new Color(255, 100, 100, 255)
                            ),
                        }
                    }
                },

                // ── Left side ──────────────────────────────────────────
                new View
                {
                    Styles = Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        alignItems: Align.FlexStart,
                        justifyContent: Justify.FlexStart,
                        gapColumn: 16f,
                        gapRow: 16f,
                        paddingTop: 20f,
                        paddingBottom: 20f,
                        paddingLeft: 20f,
                        paddingRight: 20f
                    ),
                    Children =
                    {
                        // Section 1: Basic text bindings
                        Section(w: title, font: titleFont, color: titleColor, strokeColor: new Color(0, 0, 0, 255)),
                        Section(w: subtitle, font: bodyFont, color: subtitleColor),

                        // Section 2: Inline text with counter
                        new View()
                        {
                            Styles = Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                gapColumn: 4f,
                                gapRow: 4f
                            ),
                            Children =
                            {
                                Section(
                                    w: $"Counter: {counter}",
                                    font: new Font(FontFamily.Adventure, FontStyle.Bold, 18),
                                    color: new Color(255, 255, 255, 255)
                                )
                            }
                        },

                        // Section 3: Color swatches
                        new View()
                        {
                            Styles = Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                gapColumn: 8f,
                                gapRow: 8f,
                                alignItems: Align.Center
                            ),
                            Children =
                            {
                                new View
                                {
                                    Styles = Sx.Styles(backgroundColor: new Color(255, 0, 0, 255), width: 24f, height: 24f)
                                },
                                new View
                                {
                                    Styles = Sx.Styles(backgroundColor: new Color(0, 255, 0, 255), width: 24f, height: 24f)
                                },
                                new View
                                {
                                    Styles = Sx.Styles(backgroundColor: new Color(0, 0, 255, 255), width: 24f, height: 24f)
                                },
                                new View
                                {
                                    Styles = Sx.Styles(backgroundColor: new Color(255, 255, 0, 255), width: 24f, height: 24f)
                                },
                                Section(
                                    w: "Color swatches",
                                    font: new Font(FontFamily.Adventure, FontStyle.Plain, 18),
                                    color: new Color(180, 180, 180, 255),
                                    marginLeft: 8f
                                )
                            }
                        },

                        // Section 4: Visibility binding
                        new View()
                        {
                            Styles = Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                gapColumn: 8f,
                                gapRow: 8f,
                                alignItems: Align.Center
                            ),
                            Children =
                            {
                                new View()
                                {
                                    Styles = Sx.Styles(
                                        backgroundColor: new Color(0, 255, 255, 255),
                                        width: 16f,
                                        height: 16f,
                                        visibility: badgeVisibility
                                    ),
                                },
                                Section(
                                    w: "Badge (visibility-bound)",
                                    font: new Font(FontFamily.Adventure, FontStyle.Plain, 18),
                                    color: new Color(255, 255, 255, 255)
                                )
                            }
                        },

                        // // Section 6: Nested layout — Row of Columns
                        // Section(w: "Nested layout:", font: new Font(FontFamily.Adventure, FontStyle.Bold, 18),
                        //     color: accentColor),
                        // FlexPanel(flexDirection: FlexDirection.Row, gap: counterModulo, children:
                        // [
                        //     FlexPanel(flexDirection: FlexDirection.Column, gap: 4f, children:
                        //     [
                        //         Section(w: "Column A", font: new Font(FontFamily.Adventure, FontStyle.Bold, 14),
                        //             color: new Color(255, 200, 100, 255)),
                        //         PaintedBox(backgroundColor: new Color(170, 68, 68, 255), width: 60f, height: 20f),
                        //         PaintedBox(backgroundColor: new Color(170, 102, 102, 255), width: 60f, height: 20f),
                        //         PaintedBox(backgroundColor: new Color(170, 136, 136, 255), width: 60f, height: 20f)
                        //     ]),
                        //     FlexPanel(flexDirection: FlexDirection.Column, gap: 4f, children:
                        //     [
                        //         Section(w: "Column B", font: new Font(FontFamily.Adventure, FontStyle.Bold, 14),
                        //             color: new Color(100, 200, 255, 255)),
                        //         PaintedBox(backgroundColor: new Color(68, 68, 170, 255), width: 60f, height: 20f),
                        //         PaintedBox(backgroundColor: new Color(102, 102, 170, 255), width: 60f, height: 20f),
                        //         PaintedBox(backgroundColor: new Color(136, 136, 170, 255), width: 60f, height: 20f)
                        //     ])
                        // ]),
                        //
                        // // Section 7: Fade-in animation on mount
                        // FlexPanel(flexDirection: FlexDirection.Row, gap: 8f, alignItems: Align.Center, opacity: 1f,
                        //     children:
                        //     [
                        //         PaintedBox(backgroundColor: new Color(255, 255, 255, 255), width: 12f, height: 12f),
                        //         Section(w: "Fade-in on mount",
                        //             font: new Font(FontFamily.Adventure, FontStyle.Plain, 18),
                        //             color: new Color(255, 255, 255, 255))
                        //     ]),
                        //
                        // // Section 8: Contents pane
                        // Section(w: "Contents panel:", font: new Font(FontFamily.Adventure, FontStyle.Bold, 18),
                        //     color: accentColor),
                        // ContentsPanel([
                        //     Section(w: "Contents panel", font: new Font(FontFamily.Adventure, FontStyle.Bold, 18),
                        //         color: accentColor)
                        // ])
                    }
                },
                // ── Middle spacer ──────────────────────────────────────
                new View { Styles = Sx.Styles(flex: 1f) },
            },
        };
    }

    private static View ScrollList()
    {
        var list = new View()
        {
            Name = "ScrollList",
            Styles = Sx.Styles(
                flexDirection: FlexDirection.Column,
                alignItems: Align.Stretch,
                width: 360f,
                height: 260f,
                overflow: Overflow.Scroll,
                backgroundColor: new Color(20, 20, 30, 255),
                borderColor: new Color(230, 128, 26, 255),
                borderTop: 2f,
                borderBottom: 2f,
                borderLeft: 2f,
                borderRight: 2f,
                borderRadius: 8f,
                paddingTop: 4f,
                paddingBottom: 4f
            ),
        };

        for (int i = 0; i < 100; i++)
        {
            list.Children.Add(Row($"Row {i}"));
        }

        return list;
    }

    private static View Row(string text)
    {
        var row = new View()
        {
            Name = text,
            IsFocusable = true,
            Styles = Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                height: 36f,
                minHeight: 36f,
                maxHeight: 36f,
                paddingLeft: 12f,
                paddingRight: 12f,
                backgroundColor: new Color(45, 45, 60, 255)
            ),
            Children =
            {
                new Text()
                {
                    TextStyles = Sx.TextStyles(
                        fontFamily: FontFamily.Adventure,
                        fontSize: 16f,
                        foregroundColor: new Color(220, 220, 220, 255)
                    ),
                    TextContent = text,
                }
            }
        };

        row.MouseEntered = _ => row.Styles = row.Styles with { BackgroundColor = new Color(70, 70, 95, 255) };
        row.MouseLeft = _ => row.Styles = row.Styles with { BackgroundColor = new Color(45, 45, 60, 255) };
        row.MousePressed = e
            => Logging.Info($"[YogaTest] pressed '{text}' rel=({e.RelativePosition.X:F0},{e.RelativePosition.Y:F0})");

        return row;
    }

    private static View Section(string w, Font font, Color color, Color? strokeColor = null, float marginLeft = 0f)
    {
        return new View()
        {
            Styles = Sx.Styles(
                flexDirection: FlexDirection.Row,
                gapRow: 4f,
                gapColumn: 4f,
                marginLeft: marginLeft
            ),
            Children =
            {
                new Text()
                {
                    TextStyles = Sx.TextStyles(
                        fontFamily: font.FontFamily,
                        fontSize: font.Size,
                        fontStyle: font.Style,
                        foregroundColor: color,
                        strokeColor: strokeColor
                    ),
                    Children =
                    {
                        new TextNode()
                        {
                            Text = w
                        }
                    }
                }
            }
        };
    }
}
