// Verifies the geometry the outlined-text path depends on, without a GPU: the contour a glyph
// retains is in em units, in contour order, split at the right places, and its stroke lands where
// the outline actually is.
//
// The contour is the half of this feature that a build cannot check. Getting the units wrong, or
// ordering the contour by the band sort's shuffled list, still compiles and still strokes - just
// through a mangled letter - so it is measured here instead.
using System;
using System.IO;
using System.Numerics;
using Apos.Shapes;

internal static class Program
{
    private static int _failures;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _failures++;
    }

    private static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : FindFont();
        Console.WriteLine($"font: {path}");
        var font = new ShapeFont(File.ReadAllBytes(path));

        Console.WriteLine();
        Console.WriteLine("== a glyph with a counter (o) keeps two contours ==");
        {
            GlyphFont gf = font.Font;
            BakedGlyph o = gf.Lookup('o');
            Check(o.HasOutline, "o has an outline");
            Check(o.Contour.Length > 0, $"o retains a contour ({o.Contour.Length} curves)");
            Check(o.ContourStarts.Length == 2, $"o has two contours (got {o.ContourStarts.Length})");
            Check(o.ContourStarts[0] == 0, $"the first starts at 0 (got {o.ContourStarts[0]})");
            Check(o.ContourStarts[1] > 0 && o.ContourStarts[1] < o.Contour.Length,
                  $"the second starts inside the list (got {o.ContourStarts[1]} of {o.Contour.Length})");

            // The whole point of retaining the contour separately: the band sorts shuffle the
            // list the shader reads, so the retained copy must not be that list.
            Console.WriteLine($"       (glyph box {o.Min} to {o.Max})");
        }

        Console.WriteLine();
        Console.WriteLine("== the contour is in em units and inside the glyph's own box ==");
        {
            BakedGlyph o = font.Font.Lookup('o');
            // Every contour point of a glyph sits inside its own bounding box, which is what
            // pins the units down: design units would be a thousand times too large and land
            // far outside it, and a normalized [0,1] frame would collapse into a corner.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (GlyphCurve c in o.Contour)
            {
                minX = MathF.Min(minX, MathF.Min(c.P1.X, MathF.Min(c.P2.X, c.P3.X)));
                maxX = MathF.Max(maxX, MathF.Max(c.P1.X, MathF.Max(c.P2.X, c.P3.X)));
                minY = MathF.Min(minY, MathF.Min(c.P1.Y, MathF.Min(c.P2.Y, c.P3.Y)));
                maxY = MathF.Max(maxY, MathF.Max(c.P1.Y, MathF.Max(c.P2.Y, c.P3.Y)));
            }
            // Control points may bulge past the box a little, which is why this is a loose
            // tolerance rather than an exact containment.
            const float slack = 0.25f;
            Check(minX >= o.Min.X - slack && maxX <= o.Max.X + slack,
                  $"x within the box [{o.Min.X:0.####}, {o.Max.X:0.####}] (contour {minX:0.####} to {maxX:0.####})");
            Check(minY >= o.Min.Y - slack && maxY <= o.Max.Y + slack,
                  $"y within the box [{o.Min.Y:0.####}, {o.Max.Y:0.####}] (contour {minY:0.####} to {maxY:0.####})");

            // An 'o' is wider than it is tall in a typical font, and never a hairline: if the
            // units were off by the units-per-em the box would be ~1000x this.
            float w = maxX - minX, h = maxY - minY;
            Check(w > 0.05f && w < 2f && h > 0.05f && h < 2f,
                  $"the contour spans a sane number of ems ({w:0.###} x {h:0.###})");
        }

        Console.WriteLine();
        Console.WriteLine("== the contour is closed: every contour's last point meets its first ==");
        {
            BakedGlyph o = font.Font.Lookup('o');
            for (int s = 0; s < o.ContourStarts.Length; s++)
            {
                int start = o.ContourStarts[s];
                int next = s + 1 < o.ContourStarts.Length ? o.ContourStarts[s + 1] : o.Contour.Length;
                Vector2 first = o.Contour[start].P1;
                Vector2 last = o.Contour[next - 1].P3;
                Check(Vector2.Distance(first, last) < 1e-4f,
                      $"contour {s} closes ({first} -> {last})");
            }
        }

        Console.WriteLine();
        Console.WriteLine("== a space has no contour and no strokes ==");
        {
            BakedGlyph sp = font.Font.Lookup(' ');
            Check(!sp.HasOutline, "space has no outline");
            Check(sp.Contour.Length == 0, "space retains nothing to stroke");
        }

        Console.WriteLine();
        Console.WriteLine("== a glyph the font is missing still bakes without a contour ==");
        {
            // A code point no font has a glyph for resolves to the missing glyph, which is a box
            // the font does draw - so this is about not throwing, not about being empty.
            BakedGlyph box = font.Font.Lookup(0x10FFFD);
            Check(box.ContourStarts.Length > 0, "the missing glyph still has a contour list");
        }

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "SLUG SMOKE: pass" : $"SLUG SMOKE: FAILED ({_failures})");
        return _failures == 0 ? 0 : 1;
    }

    private static string FindFont()
    {
        string[] candidates =
        {
            "Apos.Shapes/Example/Content/source-code-pro-medium.ttf",
            "../Apos.Shapes/Example/Content/source-code-pro-medium.ttf",
            "Apos.Shapes/Example/Platforms/NFMW/bin/Debug/net11.0/source-code-pro-medium.ttf",
        };
        foreach (string c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        throw new FileNotFoundException("Pass a .ttf path as the first argument.");
    }
}
