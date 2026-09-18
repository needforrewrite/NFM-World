using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Reactor;

/// <summary>
/// TextElement is a base class for content in text based controls.
/// TextElements span other content, applying property values or providing structural information.
/// </summary>
public abstract class TextElement : IRichTextElement, IInline
{
    /// <summary>
    /// Gets or sets a brush used to paint the control's background.
    /// </summary>
    public Color? Background { get; set; }

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public FontFamily? FontFamily { get; set; }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float? FontSize { get; set; }

    /// <summary>
    /// Gets or sets the font style.
    /// </summary>
    public FontStyle? FontStyle { get; set; }
    
    /// <summary>
    /// Gets or sets a brush used to paint the text.
    /// </summary>
    public Color? Foreground { get; set; }
    
    public Color? Stroke { get; set; }
}