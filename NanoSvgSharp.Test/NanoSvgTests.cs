[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]

namespace NanoSvgSharp.Test;

[TestClass]
public class BasicShapeTests
{
	[TestMethod]
	public void ParseRect_Basic_ProducesClosedPathWithColorFill()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect x=\"10\" y=\"20\" width=\"30\" height=\"40\" fill=\"red\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		Assert.AreEqual(100f, img.width, 0.001f);
		Assert.AreEqual(100f, img.height, 0.001f);
		Assert.AreEqual(1, img.shapes.Count);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(1, shape.paths[0].closed);
		Assert.AreEqual(13, shape.paths[0].npts); // 1 move + 4 line segments * 3
		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_COLOR, shape.fill.type);
		Assert.AreEqual(0xFF0000FFu, shape.fill.color); // red + full alpha

		// No viewBox => identity scaling, bounds are the raw rect bounds.
		Assert.AreEqual(10f, shape.bounds[0], 0.001f);
		Assert.AreEqual(20f, shape.bounds[1], 0.001f);
		Assert.AreEqual(40f, shape.bounds[2], 0.001f);
		Assert.AreEqual(60f, shape.bounds[3], 0.001f);
	}

	[TestMethod]
	public void ParseCircle_ProducesClosedApproximation()
	{
		var svg = "<svg width=\"100\" height=\"100\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"none\" stroke=\"black\" stroke-width=\"2\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(1, shape.paths[0].closed);
		Assert.AreEqual(16, shape.paths[0].npts); // 1 move + 4 cubics*3 + closing line*3
		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_NONE, shape.fill.type);
		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_COLOR, shape.stroke.type);
		Assert.AreEqual(2f, shape.strokeWidth, 0.001f);
	}

	[TestMethod]
	public void ParseEllipse_ProducesClosedPath()
	{
		var svg = "<svg width=\"100\" height=\"100\"><ellipse cx=\"50\" cy=\"50\" rx=\"30\" ry=\"20\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(1, shape.paths[0].closed);
		Assert.AreEqual(16, shape.paths[0].npts);
		// Start point of the ellipse path is (cx + rx, cy) = (80, 50).
		Assert.AreEqual(80f, shape.paths[0].pts[0], 0.001f);
		Assert.AreEqual(50f, shape.paths[0].pts[1], 0.001f);
	}

	[TestMethod]
	public void ParsePolygon_ProducesClosedPath()
	{
		var svg = "<svg width=\"100\" height=\"100\"><polygon points=\"0,0 100,0 100,100 0,100\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(1, shape.paths[0].closed);
		Assert.AreEqual(13, shape.paths[0].npts); // 1 move + 3 line*3 + closing line*3
	}

	[TestMethod]
	public void ParsePolyline_IsNotClosed()
	{
		var svg = "<svg width=\"100\" height=\"100\"><polyline points=\"0,0 100,0\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(0, shape.paths[0].closed);
	}

	[TestMethod]
	public void ParseLine_IsNotClosed()
	{
		var svg = "<svg width=\"100\" height=\"100\"><line x1=\"1\" y1=\"2\" x2=\"3\" y2=\"4\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(0, shape.paths[0].closed);
		Assert.AreEqual(4, shape.paths[0].npts);
		Assert.AreEqual(1f, shape.paths[0].pts[0], 0.001f);
		Assert.AreEqual(2f, shape.paths[0].pts[1], 0.001f);
	}
}

[TestClass]
public class ViewBoxTests
{
	[TestMethod]
	public void ParseRect_ViewBox_ScalesCoordinates()
	{
		var svg = "<svg width=\"100\" height=\"100\" viewBox=\"0 0 10 10\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"blue\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		// scale = 100 / 10 = 10, tx = -0 = 0
		Assert.AreEqual(0f, shape.paths[0].pts[0], 0.001f);
		Assert.AreEqual(0f, shape.paths[0].pts[1], 0.001f);
		// end of the first lineTo: (10,0) scaled by 10
		Assert.AreEqual(100f, shape.paths[0].pts[6], 0.001f);
		Assert.AreEqual(0f, shape.paths[0].pts[7], 0.001f);
		Assert.AreEqual(0f, shape.bounds[0], 0.001f);
		Assert.AreEqual(0f, shape.bounds[1], 0.001f);
		Assert.AreEqual(100f, shape.bounds[2], 0.001f);
		Assert.AreEqual(100f, shape.bounds[3], 0.001f);
	}

	[TestMethod]
	public void ParseRect_ViewBox_OffsetsAreTranslated()
	{
		// viewBox starts at (5, 5) so coordinates are shifted by -5 before scaling.
		var svg = "<svg width=\"100\" height=\"100\" viewBox=\"5 5 10 10\"><rect x=\"5\" y=\"5\" width=\"10\" height=\"10\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		// (5,5) -> tx = -5, scale = 10 => (0,0)
		Assert.AreEqual(0f, shape.paths[0].pts[0], 0.001f);
		Assert.AreEqual(0f, shape.paths[0].pts[1], 0.001f);
		Assert.AreEqual(0f, shape.bounds[0], 0.001f);
		Assert.AreEqual(0f, shape.bounds[1], 0.001f);
		Assert.AreEqual(100f, shape.bounds[2], 0.001f);
		Assert.AreEqual(100f, shape.bounds[3], 0.001f);
	}

	[TestMethod]
	public void Parse_WithoutDimensions_UsesShapeBounds()
	{
		var svg = "<svg viewBox=\"0 0 100 100\"><rect x=\"10\" y=\"10\" width=\"20\" height=\"20\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		// width/height are inferred from the viewBox.
		Assert.AreEqual(100f, img.width, 0.001f);
		Assert.AreEqual(100f, img.height, 0.001f);
		var shape = img.shapes[0];
		Assert.AreEqual(10f, shape.bounds[0], 0.001f);
		Assert.AreEqual(30f, shape.bounds[2], 0.001f);
	}
}

[TestClass]
public class PathCommandTests
{
	[TestMethod]
	public void ParsePath_AllCommands_ParsesWithoutError()
	{
		var svg = "<svg width=\"100\" height=\"100\"><path d=\"M10 10 L20 20 C30 30 40 40 50 50 Q60 60 70 70 T80 80 A10 10 0 0 1 90 90 Z\" fill=\"none\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		Assert.AreEqual(1, img.shapes.Count);
		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		var path = shape.paths[0];
		Assert.AreEqual(1, path.closed);
		Assert.IsTrue(path.npts >= 4);
		Assert.AreEqual(0, (path.npts - 1) % 3); // valid cubic-bezier point count
	}

	[TestMethod]
	public void ParsePath_RelativeCommands_ParsesWithoutError()
	{
		var svg = "<svg width=\"100\" height=\"100\"><path d=\"m10 10 l20 0 c5 5 5 5 10 0 z\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(1, shape.paths[0].closed);
		Assert.AreEqual(0, (shape.paths[0].npts - 1) % 3);
	}

	[TestMethod]
	public void ParsePath_MultipleSubpaths_CreateMultiplePaths()
	{
		var svg = "<svg width=\"100\" height=\"100\"><path d=\"M0 0 L10 0 M0 10 L10 10\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(2, shape.paths.Count);
		Assert.AreEqual(4, shape.paths[0].npts);
		Assert.AreEqual(4, shape.paths[1].npts);
	}

	[TestMethod]
	public void ParsePath_HorizontalVerticalCommands_Parses()
	{
		var svg = "<svg width=\"100\" height=\"100\"><path d=\"M10 10 H90 V90 H10 Z\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		var shape = img.shapes[0];
		Assert.AreEqual(1, shape.paths.Count);
		Assert.AreEqual(1, shape.paths[0].closed);
		Assert.AreEqual(0, (shape.paths[0].npts - 1) % 3);
	}
}

[TestClass]
public class ColorTests
{
	[TestMethod]
	public void ParseColor_NamedBasic_Red()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"red\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		Assert.AreEqual(0xFF0000FFu, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_NamedFullTable_Rebeccapurple()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"rebeccapurple\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		// rebeccapurple = rgb(102, 51, 153)
		Assert.AreEqual(0xFF993366u, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_NamedFullTable_Tomato()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"tomato\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		// tomato = rgb(255, 99, 71)
		Assert.AreEqual(0xFF4763FFu, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_Hex6_Red()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"#ff0000\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		Assert.AreEqual(0xFF0000FFu, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_Hex3_ExpandsChannels()
	{
		// #abc == #aabbcc
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"#abc\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		Assert.AreEqual(0xFFCCBBAAu, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_RgbInt()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"rgb(255, 0, 128)\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		Assert.AreEqual(0xFF8000FFu, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_RgbPercent()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"rgb(100%, 0%, 50%)\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		// 100% -> 255, 0% -> 0, 50% -> round(127.5) = 128
		Assert.AreEqual(0xFF8000FFu, shape.fill.color);
	}

	[TestMethod]
	public void ParseColor_Unknown_FallsBackToGray()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"notacolor\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		Assert.AreEqual(0xFF808080u, shape.fill.color);
	}

	[TestMethod]
	public void ParseFill_None_ProducesNonePaint()
	{
		var svg = "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"none\"/></svg>";
		var shape = NSVG.Parse(svg, "px", 96).shapes[0];
		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_NONE, shape.fill.type);
	}
}

[TestClass]
public class GradientTests
{
	[TestMethod]
	public void ParseLinearGradient_ObjectBoundingBox()
	{
		var svg = "<svg width=\"100\" height=\"100\"><defs><linearGradient id=\"g1\"><stop offset=\"0\" stop-color=\"red\"/><stop offset=\"1\" stop-color=\"blue\"/></linearGradient></defs><rect width=\"100\" height=\"100\" fill=\"url(#g1)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT, shape.fill.type);
		Assert.IsNotNull(shape.fill.gradient);
		Assert.AreEqual(2, shape.fill.gradient.Value.nstops);
		Assert.AreEqual((sbyte)NSVGspreadType.NSVG_SPREAD_PAD, shape.fill.gradient.Value.spread);
		// stop[0] = red at offset 0
		Assert.AreEqual(0f, shape.fill.gradient.Value.stops[0].offset, 0.001f);
		Assert.AreEqual(0xFF0000FFu, shape.fill.gradient.Value.stops[0].color);
		// stop[1] = blue at offset 1 (NSVG_RGB is BGR: blue = 0x00FF0000, plus alpha)
		Assert.AreEqual(1f, shape.fill.gradient.Value.stops[1].offset, 0.001f);
		Assert.AreEqual(0xFFFF0000u, shape.fill.gradient.Value.stops[1].color);
	}

	[TestMethod]
	public void ParseRadialGradient_ObjectBoundingBox()
	{
		var svg = "<svg width=\"100\" height=\"100\"><defs><radialGradient id=\"g2\"><stop offset=\"0\" stop-color=\"white\"/><stop offset=\"1\" stop-color=\"black\"/></radialGradient></defs><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"url(#g2)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_RADIAL_GRADIENT, shape.fill.type);
		Assert.IsNotNull(shape.fill.gradient);
		Assert.AreEqual(2, shape.fill.gradient.Value.nstops);
		Assert.AreEqual(0f, shape.fill.gradient.Value.fx, 0.001f);
		Assert.AreEqual(0f, shape.fill.gradient.Value.fy, 0.001f);
	}

	[TestMethod]
	public void ParseLinearGradient_UserSpaceOnUse()
	{
		var svg = "<svg width=\"100\" height=\"100\"><defs><linearGradient id=\"g3\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"0\" x2=\"100\" y2=\"0\"><stop offset=\"0\" stop-color=\"red\"/><stop offset=\"1\" stop-color=\"blue\"/></linearGradient></defs><rect width=\"100\" height=\"100\" fill=\"url(#g3)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT, shape.fill.type);
		Assert.IsNotNull(shape.fill.gradient);
		// x1=0,y1=0,x2=100,y2=0 => xform aligned to the line
		Assert.AreEqual(2, shape.fill.gradient.Value.nstops);
	}

	[TestMethod]
	public void ParseStrokeGradient_Resolves()
	{
		var svg = "<svg width=\"100\" height=\"100\"><defs><linearGradient id=\"g4\"><stop offset=\"0\" stop-color=\"red\"/><stop offset=\"1\" stop-color=\"green\"/></linearGradient></defs><rect width=\"100\" height=\"100\" fill=\"none\" stroke=\"url(#g4)\" stroke-width=\"1\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_LINEAR_GRADIENT, shape.stroke.type);
		Assert.IsNotNull(shape.stroke.gradient);
	}

	[TestMethod]
	public void ParseFill_UrlUnknown_BecomesNone()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect width=\"100\" height=\"100\" fill=\"url(#missing)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_NONE, shape.fill.type);
	}
}

[TestClass]
public class StyleAndAttributeTests
{
	[TestMethod]
	public void ParseClass_StylesApply()
	{
		var svg = "<svg width=\"100\" height=\"100\"><style>.foo { fill: green; }</style><rect class=\"foo\" width=\"10\" height=\"10\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_COLOR, shape.fill.type);
		Assert.AreEqual(0xFF008000u, shape.fill.color); // green
	}

	[TestMethod]
	public void ParseClass_MultipleClassesAndStyles()
	{
		var svg = "<svg width=\"100\" height=\"100\"><style>.a { fill: red; stroke: black; } .b { stroke-width: 3; }</style><rect class=\"a b\" width=\"10\" height=\"10\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(0xFF0000FFu, shape.fill.color);
		Assert.AreEqual((sbyte)NSVGpaintType.NSVG_PAINT_COLOR, shape.stroke.type);
		Assert.AreEqual(3f, shape.strokeWidth, 0.001f);
	}

	[TestMethod]
	public void ParseInlineStyle_Overrides()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect style=\"fill: #00ff00\" width=\"10\" height=\"10\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(0xFF00FF00u, shape.fill.color); // green
	}

	[TestMethod]
	public void ParseDisplay_None_SetsNoVisibleFlag()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect width=\"10\" height=\"10\" display=\"none\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(0, shape.flags);
	}

	[TestMethod]
	public void ParseStrokeDashArray()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect width=\"10\" height=\"10\" stroke=\"black\" stroke-dasharray=\"4 2\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(2, shape.strokeDashCount);
		Assert.AreEqual(4f, shape.strokeDashArray[0], 0.001f);
		Assert.AreEqual(2f, shape.strokeDashArray[1], 0.001f);
	}

	[TestMethod]
	public void ParseStrokeDashArray_None_CountZero()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect width=\"10\" height=\"10\" stroke=\"black\" stroke-dasharray=\"none\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(0, shape.strokeDashCount);
	}

	[TestMethod]
	public void ParseStrokeMiterLimit_ClampsNegative()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect width=\"10\" height=\"10\" stroke=\"black\" stroke-miterlimit=\"-5\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(0f, shape.miterLimit, 0.001f);
	}

	[TestMethod]
	public void ParsePaintOrder_Encodes()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect width=\"10\" height=\"10\" paint-order=\"stroke fill markers\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		// default paint order is fill, stroke, markers
		Assert.AreNotEqual(0, shape.paintOrder);
	}
}

[TestClass]
public class TransformTests
{
	[TestMethod]
	public void ParseTransform_Translate_MovesShape()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" transform=\"translate(10 20)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(10f, shape.bounds[0], 0.001f);
		Assert.AreEqual(20f, shape.bounds[1], 0.001f);
		Assert.AreEqual(20f, shape.bounds[2], 0.001f);
		Assert.AreEqual(30f, shape.bounds[3], 0.001f);
	}

	[TestMethod]
	public void ParseTransform_Scale_ScalesShape()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" transform=\"scale(2)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(20f, shape.bounds[2], 0.001f);
		Assert.AreEqual(20f, shape.bounds[3], 0.001f);
	}

	[TestMethod]
	public void ParseTransform_Rotate_ChangesBounds()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" transform=\"rotate(90 0 0)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		// A 10x10 rect rotated 90 deg around the origin occupies [-10, 10] x [-10, 0]
		Assert.AreEqual(-10f, shape.bounds[0], 1.0f);
		Assert.AreEqual(0f, shape.bounds[2], 1.0f);
	}

	[TestMethod]
	public void ParseTransform_Matrix_ChangesBounds()
	{
		var svg = "<svg width=\"100\" height=\"100\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" transform=\"matrix(1 0 0 1 5 5)\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);
		var shape = img.shapes[0];

		Assert.AreEqual(5f, shape.bounds[0], 0.001f);
		Assert.AreEqual(5f, shape.bounds[1], 0.001f);
		Assert.AreEqual(15f, shape.bounds[2], 0.001f);
		Assert.AreEqual(15f, shape.bounds[3], 0.001f);
	}
}

[TestClass]
public class MiscTests
{
	[TestMethod]
	public void Parse_EmptyInput_ProducesEmptyImage()
	{
		var img = NSVG.Parse("", "px", 96);
		Assert.IsNotNull(img);
		Assert.AreEqual(0, img.shapes.Count);
	}

	[TestMethod]
	public void Parse_InvalidContent_DoesNotThrow()
	{
		var img = NSVG.Parse("<svg><garbage></garbage></svg>", "px", 96);
		Assert.AreEqual(0, img.shapes.Count);
	}

	[TestMethod]
	public void Parse_MillimeterUnits_Converts()
	{
		// 1mm at 96dpi = 96/25.4 px ~= 3.7795
		var svg = "<svg width=\"10mm\" height=\"10mm\"><rect x=\"0\" y=\"0\" width=\"10mm\" height=\"10mm\"/></svg>";

		var img = NSVG.Parse(svg, "px", 96);

		Assert.AreEqual(10f * 96f / 25.4f, img.width, 0.01f);
		var shape = img.shapes[0];
		Assert.AreEqual(10f * 96f / 25.4f, shape.bounds[2], 0.01f);
	}

	[TestMethod]
	public void ParseFromFile_ReadsAndParses()
	{
		var svg = "<svg width=\"50\" height=\"50\"><rect width=\"50\" height=\"50\" fill=\"blue\"/></svg>";
		var path = Path.Combine(Path.GetTempPath(), "nano_test_" + Guid.NewGuid().ToString("N") + ".svg");
		File.WriteAllText(path, svg);
		try
		{
			var img = NSVG.ParseFromFile(path, "px", 96);
			Assert.AreEqual(50f, img.width, 0.001f);
			Assert.AreEqual(1, img.shapes.Count);
			Assert.AreEqual(0xFFFF0000u, img.shapes[0].fill.color); // blue (BGR + alpha)
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public void DuplicatePath_CopiesData()
	{
		var svg = "<svg width=\"100\" height=\"100\"><path d=\"M0 0 L10 0 L10 10 Z\"/></svg>";
		var path = NSVG.Parse(svg, "px", 96).shapes[0].paths[0];

		var copy = NSVG.DuplicatePath(path);

		Assert.AreEqual(path.npts, copy.npts);
		Assert.AreEqual(path.closed, copy.closed);
		Assert.AreEqual(path.pts.Length, copy.pts.Length);
		Assert.IsFalse(ReferenceEquals(path.pts, copy.pts)); // deep copy
		for (int i = 0; i < path.pts.Length; i++)
			Assert.AreEqual(path.pts[i], copy.pts[i], 0.001f);
	}
}
