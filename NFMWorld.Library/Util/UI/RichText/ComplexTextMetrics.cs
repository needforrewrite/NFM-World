using System.Numerics;
using System.Text;
using Maxine.Extensions;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Reactor;

public static class ComplexTextMetrics
{
    public static IEnumerable<FlattenedRichText> FlattenText<T>(IEnumerable<T> elements)
        where T : IRichTextElement
    {
        return CompactImpl(FlattenImpl(elements));

        IEnumerable<FlattenedRichText> CompactImpl(IEnumerable<FlattenedRichText> flattened)
        {
            // for every element, if the previous element contains the same style, combine their strings
            FlattenedRichText? previous = null;
            foreach (var element in flattened)
            {
                if (previous.HasValue &&
                    previous.Value.Background == element.Background &&
                    previous.Value.Foreground == element.Foreground &&
                    previous.Value.Stroke == element.Stroke &&
                    previous.Value.FontFamily == element.FontFamily &&
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    previous.Value.FontSize == element.FontSize &&
                    previous.Value.FontStyle == element.FontStyle)
                {
                    previous = previous.Value with { Text = previous.Value.Text + element.Text };
                }
                else
                {
                    yield return element;
                }
            }
        }

        static IEnumerable<FlattenedRichText> FlattenImpl(IEnumerable<T> elements)
        {
            foreach (var element in elements)
            {
                foreach (var flattened in FlattenInner(element))
                {
                    yield return flattened;
                }
            }
        }

        static IEnumerable<FlattenedRichText> FlattenInner<T>(
            T element,
            Color? parentBackground = null,
            Color? parentForeground = null,
            Color? parentStroke = null,
            FontFamily? parentFontFamily = null,
            float? parentFontSize = null,
            FontStyle? parentFontStyle = null)
            where T : IRichTextElement
        {
            if (element is IRichTextLeaf leaf)
            {
                yield return new FlattenedRichText(
                    Background: element.Background ?? parentBackground,
                    Foreground: element.Foreground ?? parentForeground,
                    Stroke: element.Stroke ?? parentStroke,
                    FontFamily: element.FontFamily ?? parentFontFamily,
                    FontSize: element.FontSize ?? parentFontSize,
                    FontStyle: element.FontStyle ?? parentFontStyle,
                    Text: leaf.Text
                );
            }
            else if (element is IRichTextContainer container)
            {
                foreach (var child in container.Children)
                {
                    foreach (var flattenedChild in FlattenInner(
                                 child,
                                 element.Background ?? parentBackground,
                                 element.Foreground ?? parentForeground,
                                 element.Stroke ?? parentStroke,
                                 element.FontFamily ?? parentFontFamily,
                                 element.FontSize ?? parentFontSize,
                                 element.FontStyle ?? parentFontStyle
                    ))
                    {
                        yield return flattenedChild;
                    }
                }
            }
        }
    }
    
    public static IEnumerable<FlattenedRichText> LayoutText(Font defaultFont, IEnumerable<FlattenedRichText> elements, Vector2 bounds, BreakType breakType = BreakType.Word, OverflowBehavior overflowBehavior = OverflowBehavior.ContinueHorizontally)
    {
        if (breakType == BreakType.None)
        {
            return elements;
        }

        return LayoutImpl(elements);

        IEnumerable<FlattenedRichText> LayoutImpl(IEnumerable<FlattenedRichText> flattened)
        {
            var sb = new StringBuilder();
            var lineWidth = 0.0f;
            var lineHeight = 0.0f;
        
            var textHeight = 0f;

            foreach (var element in flattened)
            {
                var ftm = G.GetFontMetrics(new Font(element.FontFamily ?? defaultFont.FontFamily, element.FontStyle ?? defaultFont.Style, element.FontSize ?? defaultFont.Size));

                var spaceWidth = ftm.MeasureText(" ").X;

                foreach (var wordRange in element.Text.AsSpan().Split(' '))
                {
                    var word = element.Text.AsSpan(wordRange);
            
                    var wordSize = ftm.MeasureText(word);
                    
                    lineHeight = Math.Max(lineHeight, wordSize.Y);

                    if (lineWidth + wordSize.X > bounds.X &&
                        (textHeight + (wordSize.Y * 2) < bounds.Y || overflowBehavior == OverflowBehavior.ContinueVertically))
                    {
                        if (breakType == BreakType.Word)
                        {
                            sb.Append('\n');
                            textHeight += lineHeight;
                            lineWidth = 0.0f;
                            lineHeight = 0.0f;
                        }
                        else if (breakType == BreakType.Character)
                        {
                            foreach (var ch in word)
                            {
                                var charWidth = ftm.MeasureText([ch]).X;
                                if (lineWidth + charWidth > bounds.X)
                                {
                                    sb.Append('\n');
                                    textHeight += lineHeight;
                                    lineWidth = 0.0f;
                                    lineHeight = 0.0f;
                                }
                                sb.Append(ch);
                                lineWidth += charWidth;
                            }
                            sb.Append(' ');
                            lineWidth += spaceWidth;
                            continue;
                        }
                    }

                    sb.Append(word).Append(' ');
                    lineWidth += wordSize.X + spaceWidth;
                }

                yield return element with { Text = sb.TrimEnd().ToString() };
                sb.Clear();
            }
        }
    }

    public static RichTextContainer MeasureRichText(IEnumerable<FlattenedRichText> elements, Font defaultFont)
    {
        var cursor = Vector2.Zero;
        float currentLineHeight = 0;
        float totalWidth = 0;
        List<PositionedRichText> positionedElements = [];
        
        foreach (var element in elements)
        {
            var font = new Font(element.FontFamily ?? defaultFont.FontFamily, element.FontStyle ?? defaultFont.Style, element.FontSize ?? defaultFont.Size);
            var foreground = element.Foreground;
            var background = element.Background;
            var stroke = element.Stroke;
            
            if (element is IRichTextLeaf leaf && !string.IsNullOrEmpty(leaf.Text))
            {
                var fontMetrics = G.GetFontMetrics(font);

                if (leaf.Text.Contains('\n'))
                {
                    var isFirst = true;
                    foreach (var range in leaf.Text.AsSpan().Split('\n'))
                    {
                        if (isFirst)
                        {
                            isFirst = false;
                        }
                        else
                        {
                            cursor.X = 0; // Move cursor to the beginning of the line
                            cursor.Y += currentLineHeight; // Move cursor down by the height of the line
                        }
                        
                        var lineSize = fontMetrics.MeasureText(leaf.Text.AsSpan(range));
                        positionedElements.Add(new PositionedRichText(cursor, lineSize, background, foreground, stroke, font, leaf.Text[range]));

                        cursor.X = lineSize.X; // Move cursor to the end of the line
                        currentLineHeight = fontMetrics.LineHeight; // Store the height of the current line
                        
                        totalWidth = Math.Max(totalWidth, cursor.X); // Update total width if the current line is wider
                    }
                }
                else
                {
                    var lineSize = fontMetrics.MeasureText(leaf.Text);
                    positionedElements.Add(new PositionedRichText(cursor, lineSize, background, foreground, stroke, font, leaf.Text));

                    cursor.X += lineSize.X; // Move cursor to the end of the line
                    currentLineHeight = Math.Max(currentLineHeight, fontMetrics.LineHeight); // Update the previous line height if the current line is taller
                    
                    totalWidth = Math.Max(totalWidth, cursor.X); // Update total width if the current line is wider
                }
            }
        }
        
        return new RichTextContainer(positionedElements, new Vector2(totalWidth, cursor.Y + currentLineHeight));
    }
    
    public static void AlignBounds(Vector2 sz, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign, ref float x, ref float y)
    {
        if (hAlign != TextHorizontalAlignment.Left)
        {
            if (hAlign == TextHorizontalAlignment.Center)
            {
                x += areaWidth / 2f;
                x -= sz.X / 2f;
            }
            else if (hAlign == TextHorizontalAlignment.Right)
            {
                x += areaWidth;
                x -= sz.X;
            }
        }
            
        if (vAlign == TextVerticalAlignment.Center)
        {
            y += areaHeight / 2f;
        }
        else if (vAlign == TextVerticalAlignment.Bottom)
        {
            y += areaHeight;
        }
    }

    public readonly struct RichTextContainer(IReadOnlyList<PositionedRichText> elements, Vector2 size)
    {
        public IReadOnlyList<PositionedRichText> Elements { get; } = elements;
        public Vector2 Size { get; } = size;
    }

    public readonly record struct FlattenedRichText(
        Color? Background,
        Color? Foreground,
        Color? Stroke,
        FontFamily? FontFamily,
        float? FontSize,
        FontStyle? FontStyle,
        string Text) : IRichTextLeaf
    {
        public static implicit operator FlattenedRichText(string plainText)
            => new(null, null, null, null, null, null, plainText);
    }

    public readonly record struct PositionedRichText(
        Vector2 Position,
        Vector2 Size,
        Color? Background,
        Color? Foreground,
        Color? Stroke,
        Font Font,
        string Text) : IRichTextElement
    {
        public FontFamily? FontFamily => Font.FontFamily;
        public float? FontSize => Font.Size;
        public FontStyle? FontStyle => Font.Style;
    }
}
