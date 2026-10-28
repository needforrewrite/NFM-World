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
        Console.WriteLine("== a prebaked cache round-trips, and only onto its own font ==");
        {
            // A fresh font has to produce the same fields the cache was written from, and applying
            // the cache has to leave the drawing data untouched - a cache that decoded into
            // plausible bytes in the wrong order would draw garbage, not throw.
            byte[] fontBytes = File.ReadAllBytes(FindFont());
            byte[] cache = font.SavePrewarmed();
            Check(cache.Length > 0, "a cache is written");
            int baked = font.PrewarmedFieldCount;

            using var fresh = new ShapeFont(fontBytes);
            fresh.LoadPrewarmed(cache);
            // A cache has to be readable by a font that has baked nothing, which is the case the
            // game is in. Loading it must not silently fall back to baking, or the only symptom of
            // a corrupt format is a slow launch.
            Check(fresh.PrewarmedFieldCount == baked, $"the whole cache loads ({fresh.PrewarmedFieldCount} of {baked})");
            MsdfGlyph loadedO = fresh.Font.Field(fresh.Font.Lookup('o').Glyph);
            MsdfGlyph bakedO = font.Font.Field(font.Font.Lookup('o').Glyph);
            Check(loadedO.Width == bakedO.Width && loadedO.Height == bakedO.Height,
                  $"a loaded field keeps its size ({loadedO.Width}x{loadedO.Height})");
            Check(loadedO.PaddedMin == bakedO.PaddedMin && loadedO.PaddedMax == bakedO.PaddedMax,
                  "a loaded field keeps its placement");
            Check(loadedO.Rgba.AsSpan().SequenceEqual(bakedO.Rgba), "a loaded field keeps its bytes");

            // A cache that was baked from other font bytes has to be ignored whole. One flipped
            // byte in the recorded hash stands in for a font that changed: the check is on the
            // hash alone, so this exercises exactly the decision a real mismatch makes.
            byte[] wrongFont = (byte[])cache.Clone();
            wrongFont[8] ^= 0xFF;
            using var stranger = new ShapeFont(fontBytes);
            stranger.LoadPrewarmed(wrongFont);
            Check(stranger.PrewarmedFieldCount == 0, "a cache for other font bytes is ignored");

            // A damaged file misses rather than throwing: it is an accelerator, so a corrupt one
            // has to leave the lazy bake - and the missing-glyph box - to cover everything.
            byte[] broken = (byte[])cache.Clone();
            broken[broken.Length / 2] ^= 0xFF;
            using var tolerant = new ShapeFont(fontBytes);
            tolerant.LoadPrewarmed(broken);
            Check(tolerant.PrewarmedFieldCount == 0, "a damaged cache is ignored, not thrown on");

            // Nothing at all is not a cache either; the caller passes whatever it found.
            using var empty = new ShapeFont(fontBytes);
            empty.LoadPrewarmed(Array.Empty<byte>());
            Check(empty.PrewarmedFieldCount == 0, "an empty cache is ignored");

            Console.WriteLine($"  ({baked} fields, {cache.Length / 1024} KiB)");
        }

        Console.WriteLine();
        Console.WriteLine("== an outline read off alpha stays where the letter is ==");
        {
            // The outline is the letter grown by its width, so it is drawn off alpha, the true
            // distance. The median is only exact near the edge: further out it follows each edge's
            // line past the end of the edge, and reads near a letter that is not there - at every
            // corner, and elsewhere. Neither shows in plain text, which only reads the zero crossing.
            //
            // The counter's 1px outline at 16px inks everything the field puts within the band plus
            // its fade of the edge. The bound below is a generous 1.75px, which is 5.25 texels at 48
            // texels per em; a texel at least a texel further out than that must read outside it.
            const float reach = 1.75f * 48f / 16f;
            int badMedian = 0, badAlpha = 0, measured = 0;
            foreach (char ch in "Fps:ElaedtimTckGCrm(PU)Lub,-zw0123456789.")
            {
                BakedGlyph bg = font.Font.Lookup(ch);
                if (!bg.HasOutline) continue;
                MsdfGlyph f = font.Font.Field(bg.Glyph);
                for (int ty = 0; ty < f.Height; ty++)
                {
                    for (int tx = 0; tx < f.Width; tx++)
                    {
                        var p = new Vector2(f.PaddedMin.X + (tx + 0.5f) / f.EmPx, f.PaddedMin.Y + (ty + 0.5f) / f.EmPx);
                        float truth = TrueDistance(bg, p, out bool inside) * f.EmPx;
                        if (inside || truth < reach + 1f) continue;
                        measured++;
                        int at = (ty * f.Width + tx) * 4;
                        if ((0.5f - Median(f.Rgba[at] / 255f, f.Rgba[at + 1] / 255f, f.Rgba[at + 2] / 255f)) * f.RangeTexels < reach) badMedian++;
                        if ((0.5f - f.Rgba[at + 3] / 255f) * f.RangeTexels < reach) badAlpha++;
                    }
                }
            }
            Check(measured > 0 && badAlpha == 0,
                  $"alpha reads no texel beyond the outline as inside it ({badAlpha} of {measured}; the median reads {badMedian})");
        }

        Console.WriteLine();
        Console.WriteLine("== the quad's texel rect maps onto its em box one texel to one texel ==");
        {
            // The two silent geometry faults the drawn checks cannot see. A glyph baked upright
            // but drawn with the rect's rows paired the other way is a vertically mirrored letter
            // that still reads as a letter, and a quad whose em box is bigger than its texel rect
            // is the same glyph stretched. Neither throws and neither changes what is lit, so
            // both are pinned here against the field's own numbers.
            MsdfGlyph o = font.Font.Field(font.Font.Lookup('o').Glyph);
            float halfTexel = 0.5f / o.EmPx;
            float emWidth = (o.PaddedMax.X - o.PaddedMin.X) - 2f * halfTexel;
            float emHeight = (o.PaddedMax.Y - o.PaddedMin.Y) - 2f * halfTexel;
            // An 'L' is a stem plus a foot, so the rows near its bottom carry the full bar and the
            // ones near its top carry only the stem. Row 0 is the bottom, so that is the heavy
            // end; read the other way round the foot would land at the top. Both rows are taken
            // in from the padding, which sits outside the glyph and carries no ink either way.
            MsdfGlyph L = font.Font.Field(font.Font.Lookup('L').Glyph);
            int inFromEdge = (int)MathF.Ceiling(0.5f * L.RangeTexels + 1f) + 2;
            float nearRow0 = Ink(L, inFromEdge);
            float nearLast = Ink(L, L.Height - 1 - inFromEdge);
            Check(nearRow0 > nearLast,
                  $"row 0 is the glyph's bottom, so the foot is the heavy end ({nearRow0:F0} ink at row 0's end, {nearLast:F0} at the other)");

            // One texel of the field has to be one texel of the quad: the em box the quad covers,
            // over the bake resolution, is the rect's own texel count. A quad padded beyond the
            // field stretches the glyph by the ratio of the two.
            float texelsAcross = emWidth * o.EmPx;
            float texelsDown = emHeight * o.EmPx;
            Check(MathF.Abs(texelsAcross - (o.Width - 1)) < 0.05f,
                  $"the em box spans the rect's texels across ({texelsAcross:F2} vs {o.Width - 1})");
            Check(MathF.Abs(texelsDown - (o.Height - 1)) < 0.05f,
                  $"the em box spans the rect's texels down ({texelsDown:F2} vs {o.Height - 1})");
        }

        Console.WriteLine();
        Console.WriteLine("== a glyph's AA band is always centred, never the batch's outward style ==");
        {
            // Text used to be solved as coverage, which straddles the edge by construction. MSDF
            // hands the tail a signed distance instead, and the shape default puts the whole fade
            // OUTSIDE the edge - on a 16px stem that is most of the stem's own width on each side,
            // so the fill covers about twice the ink and every straight side grows a halo. The
            // width therefore has to reach the shader negated (the sign IS the style, Meta1.y -
            // see SpritePixelShader) whatever the batch is set to.
            //
            // Checked here rather than on drawn pixels because the headless ANGLE context these
            // suites run on hands ddx/ddy back as zero, which collapses the band to a hard step
            // for glyphs and shapes alike. A rendered check cannot see this at all on that context.
            Check(Apos.Shapes.ShapeBatch.GlyphAa(1.5f) == -1.5f,
                  $"a positive width comes back negated ({Apos.Shapes.ShapeBatch.GlyphAa(1.5f)})");
            Check(Apos.Shapes.ShapeBatch.GlyphAa(-1.5f) == -1.5f,
                  $"a negative width stays negative ({Apos.Shapes.ShapeBatch.GlyphAa(-1.5f)})");
            Check(Apos.Shapes.ShapeBatch.GlyphAa(0f) == 0f,
                  $"a zero width stays zero ({Apos.Shapes.ShapeBatch.GlyphAa(0f)})");
            // The magnitude is the caller's and only the sign is overridden, so the band is as
            // wide as the draw asked for rather than pinned to some default.
            Check(Apos.Shapes.ShapeBatch.GlyphAa(6f) == -6f,
                  $"the width itself is untouched ({Apos.Shapes.ShapeBatch.GlyphAa(6f)})");
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

    // How much of one row of a field is ink: how far each texel sits inside the glyph, summed, so
    // a row across the middle of a stem reads heavier than one that only grazes an edge. msdfgen
    // encodes the field inside-positive, so ink is where the median runs above its 0.5 midpoint.
    private static float Ink(MsdfGlyph g, int row)
    {
        float sum = 0;
        for (int tx = 0; tx < g.Width; tx++)
        {
            int at = (row * g.Width + tx) * 4;
            float median = Median(g.Rgba[at] / 255f, g.Rgba[at + 1] / 255f, g.Rgba[at + 2] / 255f);
            sum += MathF.Max(0f, median - 0.5f);
        }
        return sum * g.RangeTexels;
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
