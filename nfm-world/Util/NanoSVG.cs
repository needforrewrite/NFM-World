using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using NanoSvgSharp;
using NFMWorld;
using NFMWorld.DriverInterface.DriverInterface;
using NvgSharp;

public sealed partial class NanoSVGImage : IImage
{
	// Matches <style>…</style> blocks (dot matches newlines).
	[GeneratedRegex(@"<style[^>]*>(.*?)</style>", RegexOptions.Singleline)]
	private static partial Regex StyleBlockRegex { get; }

	// Matches .className{…} rules inside a style block.
	[GeneratedRegex(@"\.([a-zA-Z0-9_-]+)\s*\{([^}]+)\}")]
	private static partial Regex CssRuleRegex { get; }

	// Matches property:value; pairs inside a rule body.
	[GeneratedRegex(@"([a-zA-Z-]+)\s*:\s*([^;]+);?")]
	private static partial Regex CssPropertyRegex { get; }

	// Matches class="…" attributes on elements.
	[GeneratedRegex(@"\bclass\s*=\s*""([^""]*)""")]
	private static partial Regex ClassAttrRegex { get; }

	private readonly unsafe NSVGimage _image;

	public unsafe int Height => (int)_image.height;
	public unsafe int Width => (int)_image.width;

	public unsafe NanoSVGImage(NSVGimage image)
	{
		_image = image;
	}
	
	private static Color NsvgColorToNvg(uint color, float opacity)
	{
		byte r = (byte)(color & 0xFF);
		byte g = (byte)((color >> 8) & 0xFF);
		byte b = (byte)((color >> 16) & 0xFF);
		byte a = (byte)((color >> 24) & 0xFF);
		return new Color(r, g, b, (byte)(a * opacity));
	}

	// grad->xform is the user→gradient inverse transform (set by nsvg__xformInverse in nsvg__scaleToViewbox).
	// We recover gradient endpoints by solving the 2x2 system at gy=0 and gy=1 (linear) or gd=0 (radial center).
	private static unsafe Paint GradientPaint(NvgContext ctx, NSVGpaint paint, float opacity)
	{
		NSVGgradient? grad = paint.gradient;

		if (grad == null || grad.Value.nstops == 0)
			return default;

		var icol = NsvgColorToNvg(grad.Value.stops[0].color, opacity).ToXna();
		var ocol = NsvgColorToNvg(grad.Value.stops[grad.Value.nstops - 1].color, opacity).ToXna();

		float t0 = grad.Value.xform[0], t1 = grad.Value.xform[1];
		float t2 = grad.Value.xform[2], t3 = grad.Value.xform[3];
		float t4 = grad.Value.xform[4], t5 = grad.Value.xform[5];

		// det of the 2x2 linear part (row-vector convention: x'=x*t0+y*t2, y'=x*t1+y*t3)
		float det = t0 * t3 - t1 * t2;
		if (MathF.Abs(det) < 1e-10f) det = 1e-10f;

		// user-space point at gradient param (gx=0, gy=0)
		float cx = (-t3 * t4 + t2 * t5) / det;
		float cy = (t1 * t4 - t0 * t5) / det;

		if ((NSVGpaintType)paint.type == NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT)
		{
			// user-space point at (gx=0, gy=1)
			float ex = cx - t2 / det;
			float ey = cy + t0 / det;
			return ctx.LinearGradient(cx, cy, ex, ey, icol, ocol);
		}
		else
		{
			float r = 1.0f / MathF.Sqrt(MathF.Abs(det));
			return ctx.RadialGradient(cx, cy, 0, r, icol, ocol);
		}
	}

	/// <summary>
	/// Resolves CSS class selectors from &lt;style&gt; blocks into inline presentation
	/// attributes so that nanosvg (which does not parse CSS) can see fill/stroke/etc.
	/// </summary>
	private static string PreprocessSvgClasses(string svg)
	{
		// 1. Extract class→properties map from <style> blocks, then strip them.
		var classMap = new Dictionary<string, Dictionary<string, string>>();
		
		// Console.WriteLine($"classMap has {classMap.Count} classes");
		// foreach (var (cls, props) in classMap)
		// 	Console.WriteLine($"  .{cls} -> {string.Join(", ", props.Select(kv => $"{kv.Key}={kv.Value}"))}");
		
		svg = StyleBlockRegex.Replace(svg, match =>
		{
			var css = match.Groups[1].Value;
			foreach (Match rule in CssRuleRegex.Matches(css))
			{
				var cls = rule.Groups[1].Value;
				if (!classMap.TryGetValue(cls, out var props))
				{
					props = new Dictionary<string, string>();
					classMap[cls] = props;
				}

				foreach (Match p in CssPropertyRegex.Matches(rule.Groups[2].Value))
					props[p.Groups[1].Value.Trim()] = p.Groups[2].Value.Trim();
			}

			return ""; // remove <style> block — nanosvg ignores it
		});

		if (classMap.Count == 0)
			return svg;
		
		// var matches = ClassAttrRegex.Matches(svg);
		// Console.WriteLine($"Found {matches.Count} class= attributes");
		// foreach (Match m in matches)
		// 	Console.WriteLine($"  class=\"{m.Groups[1].Value}\" matched in: {m.Value}");
		
		// 2. Inline: class="cls1 cls2" → class="cls1 cls2" fill="…" stroke="…" …
		//    Later classes override earlier ones; existing inline attrs are untouched.
		return ClassAttrRegex.Replace(svg, match =>
		{
			var classes = match.Groups[1].Value.Split(' ',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			var added = new StringBuilder();
			var seen = new HashSet<string>();

			foreach (var cls in classes)
			{
				if (!classMap.TryGetValue(cls, out var props))
					continue;

				foreach (var (prop, val) in props)
				{
					if (seen.Add(prop))
						added.Append($" {prop}=\"{val}\"");
				}
			}

			return match.Value + added.ToString();
		});
	}
	
	public static unsafe NanoSVGImage FromString(string svg, string units = "px", float dpi = 96.0f)
	{
		svg = PreprocessSvgClasses(svg);
		return new NanoSVGImage(NSVG.Parse(svg, units, dpi));
	}

	public static unsafe NanoSVGImage FromStream(Stream stream, string units = "px", float dpi = 96.0f)
	{
		using var arr = new ArrayPoolBufferWriter<byte>();
		stream.CopyTo(arr.AsStream());

		// Decode → preprocess CSS classes → re-encode to UTF-8
		var svg = Encoding.UTF8.GetString(arr.WrittenSpan);
		svg = PreprocessSvgClasses(svg);
		return new NanoSVGImage(NSVG.Parse(svg, units, dpi));
	}

	// Shoelace signed area of all control points in a nanosvg path.
	// In screen space (Y-down): area < 0 → CCW (solid), area > 0 → CW (hole).
	private static unsafe float PathSignedArea(ReadOnlySpan<float> pts, int npts)
	{
		float area = 0;
		for (int i = 0; i < npts; i++)
		{
			int j = (i + 1) % npts;
			area += pts[i * 2] * pts[j * 2 + 1] - pts[j * 2] * pts[i * 2 + 1];
		}
		return area;
	}
	
    private unsafe void DrawVectorSVG(NvgContext ctx, float width, float height)
    {
	    float svgW = Width;
        float svgH = Height;

        float scale = Math.Min(width / svgW, height / svgH);
        float tx = (width - svgW * scale) * 0.5f;
        float ty = (height - svgH * scale) * 0.5f;

        ctx.SaveState();
        ctx.Translate(tx, ty);
        ctx.Scale(scale, scale);

        foreach (var shape in _image.shapes)
        {
            if ((shape.flags & (byte)NSVGflags.NSVG_FLAGS_VISIBLE) == 0)
                continue;

            var fillType   = (NSVGpaintType)shape.fill.type;
			var strokeType = (NSVGpaintType)shape.stroke.type;
			// Console.WriteLine($"fillType={fillType} strokeType={strokeType} opacity={shape.opacity}");
			bool hasFill = fillType is NSVGpaintType.NSVG_PAINT_COLOR or NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT or NSVGpaintType.NSVG_PAINT_RADIAL_GRADIENT;
            bool hasStroke = strokeType is NSVGpaintType.NSVG_PAINT_COLOR or NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT or NSVGpaintType.NSVG_PAINT_RADIAL_GRADIENT
                             && shape.strokeWidth > 0;

			if (!hasFill && !hasStroke)
				continue;

			// Build the full compound path once, then fill and/or stroke
			// int pathCount = 0;
			// for (NSVGpath* p = shape.paths; p != null; p = p.next) pathCount++;
			// Console.WriteLine($"shape has {pathCount} paths, fillType={fillType}");
			ctx.BeginPath();
            foreach (var path in shape.paths)
            {
                float[] pts = path.pts;
                ctx.MoveTo(pts[0], pts[1]);
                for (int i = 0; i < path.npts - 1; i += 3)
                {
                    var p = pts.AsSpan(i * 2);
                    ctx.BezierTo(p[2], p[3], p[4], p[5], p[6], p[7]);
                }
                if (path.closed != 0)
                    ctx.ClosePath();
                // Mark holes: CW on screen (area>0) → NVG_CW=2=NVG_HOLE
                ctx.PathWinding(PathSignedArea(pts, path.npts) < 0 ? Solidity.Solid : Solidity.Hole);
            }

            if (hasFill)
            {
                if (fillType == NSVGpaintType.NSVG_PAINT_COLOR)
                    ctx.FillColor(NsvgColorToNvg(shape.fill.color, shape.opacity).ToXna());
                else
                    ctx.FillPaint(GradientPaint(ctx, shape.fill, shape.opacity));
                ctx.Fill();
            }
            if (hasStroke)
            {
                if (strokeType == NSVGpaintType.NSVG_PAINT_COLOR)
                    ctx.StrokeColor(NsvgColorToNvg(shape.stroke.color, shape.opacity).ToXna());
                else
                    ctx.StrokePaint(GradientPaint(ctx, shape.stroke, shape.opacity));
                ctx.StrokeWidth(shape.strokeWidth);
                ctx.Stroke();
            }
        }

        ctx.RestoreState();
    }

	public void Draw(NvgContext vg, float x, float y, float width, float height)
	{
		vg.SaveState();
		vg.Translate(x, y);
		DrawVectorSVG(vg, width, height);
		vg.RestoreState();
	}
}