using System.Globalization;
using System.Xml.Linq;

namespace NFMWorldLibrary.Util;

public static class SvgColorAdjustment
{
    public static string Apply(string markup, Vector3 multiplier, byte maximumChannel = 255)
    {
        if (!float.IsFinite(multiplier.X) || !float.IsFinite(multiplier.Y) || !float.IsFinite(multiplier.Z))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        var document = XDocument.Parse(markup);
        SvgStyles.Inline(document);
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.LocalName is "fill" or "stroke" or "stop-color" or "color")
                    attribute.Value = AdjustHex(attribute.Value, multiplier, maximumChannel);
                else if (attribute.Name.LocalName == "style")
                    attribute.Value = string.Join(';', attribute.Value.Split(';').Select(declaration =>
                    {
                        var colon = declaration.IndexOf(':');
                        if (colon < 0) return declaration;
                        var name = declaration[..colon].Trim();
                        return name is "fill" or "stroke" or "stop-color" or "color"
                            ? declaration[..(colon + 1)] + AdjustHex(declaration[(colon + 1)..].Trim(), multiplier, maximumChannel)
                            : declaration;
                    }));
            }
        }
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static string AdjustHex(string value, Vector3 multiplier, byte maximum)
    {
        if (value.Length is not (4 or 5 or 7 or 9) || value[0] != '#' ||
            value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0) return value;
        var shortForm = value.Length <= 5;
        byte Channel(int index) => shortForm
            ? (byte)(byte.Parse(value.AsSpan(index + 1, 1), NumberStyles.HexNumber) * 17)
            : byte.Parse(value.AsSpan(index * 2 + 1, 2), NumberStyles.HexNumber);
        var r = Math.Clamp((int)(Channel(0) * multiplier.X), 0, maximum);
        var g = Math.Clamp((int)(Channel(1) * multiplier.Y), 0, maximum);
        var b = Math.Clamp((int)(Channel(2) * multiplier.Z), 0, maximum);
        var alpha = value.Length is 5 or 9 ? Channel(3).ToString("x2", CultureInfo.InvariantCulture) : "";
        return $"#{r:x2}{g:x2}{b:x2}{alpha}";
    }
}
