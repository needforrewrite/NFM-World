// Verifies the glyph data the text path depends on, without a GPU: the contour a glyph retains is
// in em units, in contour order and split at the right places, and the distance field baked from
// it is a real distance - right sign, right scale, unsaturated where it should be, and stable.
//
// None of this is a build error if it goes wrong. A contour in the wrong units, or one ordered by
// the band sort's shuffled list, still compiles and still draws - just through a mangled letter -
// and a field with the sign flipped, or read a texel off, draws a plausible-looking blur. So the
// numbers are checked here instead.
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
        Console.WriteLine("== the distance field is a real distance ==");
        {
            MsdfGlyph o = font.Font.Field(font.Font.Lookup('o').Glyph);
            Check(o.HasField, "o has a field");
            Check(o.Width > 0 && o.Height > 0, $"o baked a {o.Width}x{o.Height} atlas");
            Check(o.Rgba.Length == o.TexelCount * 4, "the field is RGBA8 and the right length");
            Check(o.PaddedMin.X <= o.Min.X && o.PaddedMin.Y <= o.Min.Y, "the field covers the glyph box");
            Check(o.PaddedMax.X >= o.Max.X && o.PaddedMax.Y >= o.Max.Y, "the field covers the glyph box");

            // The distance the field reports at each texel against the true distance to the
            // contour, sampled densely. The median is what the fill reads and alpha is what a
            // blur reads; both have to be the same number.
            double sumMedian = 0, sumAlpha = 0;
            int n = 0, wrongSign = 0, saturated = 0;
            float range = o.RangeTexels;
            for (int ty = 0; ty < o.Height; ty++) {
                for (int tx = 0; tx < o.Width; tx++) {
                    // The texel's own em coordinate, from the padded box. A glyph is sampled at
                    // texel centres, which is what the half texel msdfgen adds is for.
                    var p = new Vector2(
                        o.PaddedMin.X + (tx + 0.5f) / o.EmPx,
                        o.PaddedMin.Y + (ty + 0.5f) / o.EmPx);
                    float truth = TrueDistance(font.Font.Lookup('o'), p, out bool inside);
                    if (MathF.Abs(truth) * o.EmPx > range * 0.5f) { saturated++; continue; }

                    int at = (ty * o.Width + tx) * 4;
                    float median = Median(o.Rgba[at] / 255f, o.Rgba[at + 1] / 255f, o.Rgba[at + 2] / 255f);
                    float alpha = o.Rgba[at + 3] / 255f;
                    // Encoded as sd / range + 0.5 with sd inside-positive, so this comes back
                    // as a signed texel distance with positive outside.
                    float wm = (0.5f - median) * range;
                    float wa = (0.5f - alpha) * range;
                    float want = (inside ? -1f : 1f) * truth * o.EmPx;
                    if (MathF.Sign(wm) != MathF.Sign(want) && MathF.Abs(want) > 0.5f) wrongSign++;
                    sumMedian += MathF.Abs(wm - want);
                    sumAlpha += MathF.Abs(wa - want);
                    n++;
                }
            }
            Check(n > 0, $"measured {n} texels");
            // The median is exact to the byte; a whole texel of error would mean the
            // reconstruction is reading the wrong texel, and the sign is what a winding bug
            // shows up as.
            Check(sumMedian / n < 0.15, $"the median matches the true distance (mean |err| {sumMedian / n:F3} texels)");
            Check(sumAlpha / n < 0.15, $"alpha matches the true distance (mean |err| {sumAlpha / n:F3} texels)");
            Check(wrongSign == 0, $"the sign is right inside and outside ({wrongSign} wrong)");
            Check(saturated < o.TexelCount, $"some of the field is unsaturated ({saturated}/{o.TexelCount})");

            // A second ask hands back the same field rather than baking it again.
            Check(ReferenceEquals(o, font.Font.Field(font.Font.Lookup('o').Glyph)), "the field is cached");

            // The same outline must bake to the same bytes every time. The baker walks its
            // contours, counts crossings to orient them, and refines edges; any of those reading
            // a hash set or an uninitialised value would hand back a field that differs between
            // runs, which no single-image check could see.
            MsdfGlyph again = MsdfBake.Bake(
                o.Glyph, o.Advance, o.Bearing, o.Min, o.Max,
                font.Font.Lookup('o').Contour, font.Font.Lookup('o').ContourStarts);
            bool same = again.Width == o.Width && again.Height == o.Height;
            if (same) {
                for (int i = 0; i < o.Rgba.Length; i++) {
                    if (o.Rgba[i] != again.Rgba[i]) { same = false; break; }
                }
            }
            Check(same, "the bake is deterministic");
        }

        Console.WriteLine();
        Console.WriteLine("== the shelf packer uses a shelf's whole width, not just its first row ==");
        {
            // A font is a run of similar-but-not-equal heights, and a rectangle belongs on the
            // first shelf it fits rather than only on the newest. A packer that opens a shelf
            // whenever the newest one is too short fills a few rows and then reports the atlas
            // full while every earlier shelf is still empty to the right of its first glyph.
            // Nothing about that is visible in a drawn frame, so it is checked here.
            var shelf = new MsdfShelf(256, 256);
            var placed = new List<(int X, int Y, int W, int H)>();
            // Thirteen heights a row or two apart, of the kind a font is full of, and narrow
            // enough that 256 columns holds six of them. Sharing shelves, that is three shelves
            // and about 190 rows; one shelf per height, which is what a packer that only ever
            // looks at the newest shelf does, runs past the 256 rows and starts refusing.
            var heights = new List<int>();
            for (int h = 40; h <= 52; h++) heights.Add(h);
            foreach (int h in heights)
            {
                (int X, int Y) at = shelf.Allocate(40, h);
                Check(at.X >= 0, $"a 40x{h} rectangle found room");
                if (at.X >= 0) placed.Add((at.X, at.Y, 40, h));
            }
            Check(placed.Count == heights.Count, $"every rectangle was placed ({placed.Count} of {heights.Count})");
            int rowsUsed = placed.Count == 0 ? 0 : placed.Max(p => p.Y + p.H);
            Check(rowsUsed <= 192, $"the shelves were shared rather than stacked ({rowsUsed} rows used)");

            // Two rectangles on one shelf must not overlap, which is the failure the width scan
            // would introduce if it handed out a column already taken.
            bool overlaps = false;
            for (int i = 0; i < placed.Count; i++)
                for (int j = i + 1; j < placed.Count; j++)
                {
                    (int X, int Y, int W, int H) a = placed[i], b = placed[j];
                    if (a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H) overlaps = true;
                }
            Check(!overlaps, "no two rectangles overlap");
        }

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "SLUG SMOKE: pass" : $"SLUG SMOKE: FAILED ({_failures})");
        return _failures == 0 ? 0 : 1;
    }

    // The middle of the three field channels, which is the distance the fill reads. Matches
    // median3 in the shader.
    private static float Median(float a, float b, float c)
    {
        return MathF.Max(MathF.Min(a, b), MathF.Min(MathF.Max(a, b), c));
    }

    // The true distance from a point to a baked glyph's outline, and which side of it the point
    // is on. The contour is sampled densely and the closest sample is taken: coarse, but a
    // distance field is only asked to be right to a fraction of a texel, and 512 samples per
    // curve is far finer than that at 48 texels per em.
    private static float TrueDistance(BakedGlyph g, Vector2 p, out bool inside)
    {
        float best = float.MaxValue;
        const int steps = 512;
        for (int c = 0; c < g.ContourStarts.Length; c++)
        {
            int start = g.ContourStarts[c];
            int end = c + 1 < g.ContourStarts.Length ? g.ContourStarts[c + 1] : g.Contour.Length;
            for (int i = start; i < end; i++)
            {
                GlyphCurve q = g.Contour[i];
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps, u = 1f - t;
                    var pt = new Vector2(
                        u * u * q.P1.X + 2f * u * t * q.P2.X + t * t * q.P3.X,
                        u * u * q.P1.Y + 2f * u * t * q.P2.Y + t * t * q.P3.Y);
                    best = MathF.Min(best, Vector2.Distance(pt, p));
                }
            }
        }

        // An even-odd ray cast to the right, so the sign owes nothing to the winding the baker
        // chose. A contour closes back to its first point, so each loop's last curve ends on it.
        int crossings = 0;
        for (int c = 0; c < g.ContourStarts.Length; c++)
        {
            int start = g.ContourStarts[c];
            int end = c + 1 < g.ContourStarts.Length ? g.ContourStarts[c + 1] : g.Contour.Length;
            for (int i = start; i < end; i++)
            {
                GlyphCurve q = g.Contour[i];
                Vector2 prev = q.P1;
                for (int s = 1; s <= 32; s++)
                {
                    float t = s / 32f, u = 1f - t;
                    var cur = new Vector2(
                        u * u * q.P1.X + 2f * u * t * q.P2.X + t * t * q.P3.X,
                        u * u * q.P1.Y + 2f * u * t * q.P2.Y + t * t * q.P3.Y);
                    if ((prev.Y <= p.Y) != (cur.Y <= p.Y))
                    {
                        float xc = prev.X + (p.Y - prev.Y) / (cur.Y - prev.Y) * (cur.X - prev.X);
                        if (xc > p.X) crossings++;
                    }
                    prev = cur;
                }
            }
        }
        inside = (crossings & 1) == 1;
        return best;
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
