namespace NFMWorldLibrary.Util;

public class Colors
{
    public static void RGBtoHSB(int r, int g, int b, out float hue, out float saturation, out float brightness)
    {
        var cmax = (r > g) ? r : g;
        if (b > cmax)
        {
            cmax = b;
        }

        var cmin = (r < g) ? r : g;
        if (b < cmin)
        {
            cmin = b;
        }

        brightness = cmax / 255.0f;
        if (cmax != 0)
        {
            saturation = (cmax - cmin) / ((float) cmax);
        }
        else
        {
            saturation = 0;
        }

        if (saturation == 0)
        {
            hue = 0;
        }
        else
        {
            var redc = (cmax - r) / ((float) (cmax - cmin));
            var greenc = (cmax - g) / ((float) (cmax - cmin));
            var bluec = (cmax - b) / ((float) (cmax - cmin));
            if (r == cmax)
            {
                hue = bluec - greenc;
            }
            else if (g == cmax)
            {
                hue = 2.0f + redc - bluec;
            }
            else
            {
                hue = 4.0f + greenc - redc;
            }

            hue = hue / 6.0f;
            if (hue < 0)
            {
                hue = hue + 1.0f;
            }
        }
    }

    public static Color GetHSBColor(float hue, float saturation, float brightness)
    {
        var v = HSBtoRGB(hue, saturation, brightness);
        return new Color(v.r, v.g, v.b);
    }

    public static (byte r, byte g, byte b) HSBtoRGB(float hue, float saturation, float brightness)
    {
        byte r = 0, g = 0, b = 0;
        if (saturation == 0)
        {
            r = g = b = (byte) (brightness * 255.0f + 0.5f);
        }
        else
        {
            var h = (hue - (float) Math.Floor(hue)) * 6.0f;
            var f = h - (float) Math.Floor(h);
            var p = brightness * (1.0f - saturation);
            var q = brightness * (1.0f - saturation * f);
            var t = brightness * (1.0f - (saturation * (1.0f - f)));
            switch ((int) h)
            {
                case 0:
                    r = (byte) (brightness * 255.0f + 0.5f);
                    g = (byte) (t * 255.0f + 0.5f);
                    b = (byte) (p * 255.0f + 0.5f);
                    break;
                case 1:
                    r = (byte) (q * 255.0f + 0.5f);
                    g = (byte) (brightness * 255.0f + 0.5f);
                    b = (byte) (p * 255.0f + 0.5f);
                    break;
                case 2:
                    r = (byte) (p * 255.0f + 0.5f);
                    g = (byte) (brightness * 255.0f + 0.5f);
                    b = (byte) (t * 255.0f + 0.5f);
                    break;
                case 3:
                    r = (byte) (p * 255.0f + 0.5f);
                    g = (byte) (q * 255.0f + 0.5f);
                    b = (byte) (brightness * 255.0f + 0.5f);
                    break;
                case 4:
                    r = (byte) (t * 255.0f + 0.5f);
                    g = (byte) (p * 255.0f + 0.5f);
                    b = (byte) (brightness * 255.0f + 0.5f);
                    break;
                case 5:
                    r = (byte) (brightness * 255.0f + 0.5f);
                    g = (byte) (p * 255.0f + 0.5f);
                    b = (byte) (q * 255.0f + 0.5f);
                    break;
            }
        }
        return (r, g, b);
    }
}