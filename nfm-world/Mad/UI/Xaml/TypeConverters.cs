using System.ComponentModel;
using System.Globalization;
using Font = NFMWorld.Util.Font;
using FontFamily = NFMWorld.Util.FontFamily;
using FontStyle = NFMWorld.Util.FontStyle;

namespace WorldXaml.UI.Yoga.Xaml;

/// <summary>
/// Type converter for Font - parses "FontFamily,Flags,Size" format.
/// </summary>
public class FontTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            var parts = str.Split(',');
            if (parts.Length == 3)
            {
                var fontFamily = Enum.Parse<FontFamily>(parts[0].AsSpan().Trim(), ignoreCase: true);
                var flags = Enum.Parse<FontStyle>(parts[1].AsSpan().Trim(), ignoreCase: true);
                var sizeStr = parts[2].AsSpan().Trim();
                if (sizeStr.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                {
                    sizeStr = sizeStr[..^2].Trim();
                }
                var size = float.Parse(sizeStr, CultureInfo.InvariantCulture);
                return new Font(fontFamily, flags, size);
            }
            
            parts = str.Split(' ');
            if (parts.Length >= 2)
            {
                var flags = FontStyle.Plain;
                for (var i = 0; i < parts.Length - 2; i++)
                {
                    var part = parts[i].AsSpan().Trim();
                    if (part.Equals("bold", StringComparison.OrdinalIgnoreCase))
                        flags |= FontStyle.Bold;
                    else if (part.Equals("italic", StringComparison.OrdinalIgnoreCase))
                        flags |= FontStyle.Italic;
                    else
                        throw new FormatException($"Invalid font style: {part} in '{str}'. Expected 'bold' or 'italic'.");
                }
                var sizeStr = parts[^2].AsSpan().Trim();
                if (sizeStr.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                {
                    sizeStr = sizeStr[..^2].Trim();
                }
                var size = float.Parse(sizeStr, CultureInfo.InvariantCulture);
                var fontFamily = Enum.Parse<FontFamily>(parts[^1].AsSpan().Trim(), ignoreCase: true);
                return new Font(fontFamily, flags, size);
            }
            throw new FormatException($"Invalid font format: {str}. Expected '[style*] <size> <family>' or 'FontFamily,Style,Size'.");
        }
        return base.ConvertFrom(context, culture, value);
    }
}
