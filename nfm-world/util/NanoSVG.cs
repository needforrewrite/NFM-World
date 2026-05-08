using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using nfm_world.driverinterface;
using NvgSharp;
using static NanoSVG.NSVGpaintType;
using static NanoSVG.NSVGlineJoin;
using static NanoSVG.NSVGlineCap;
using static NanoSVG.NSVGflags;

// ReSharper disable InconsistentNaming

/* https://github.com/flibitijibibo/SVG4FNA
 *
 * SVG4FNA - SVG Container and Renderer for FNA
 * Copyright (c) 2024 Ethan Lee
 *
 * NanoVG - Antialiased vector graphics rendering library for OpenGL
 * Copyright (c) 2013 Mikko Mononen memon@inside.org
 *
 * NanoSVG - Simple single-header-file SVG parse
 * Copyright (c) 2013-14 Mikko Mononen memon@inside.org
 *
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from
 * the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 * claim that you wrote the original software. If you use this software in a
 * product, an acknowledgment in the product documentation would be
 * appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not be
 * misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source distribution.
 *
 * Ethan "flibitijibibo" Lee <flibitijibibo@flibitijibibo.com>
 *
 */

namespace NanoSVG;

public enum NSVGpaintType
{
	NSVG_PAINT_UNDEF = -1,
	NSVG_PAINT_NONE = 0,
	NSVG_PAINT_COLOR = 1,
	NSVG_PAINT_LINEAR_GRADIENT = 2,
	NSVG_PAINT_RADIAL_GRADIENT = 3,
}

public enum NSVGspreadType
{
	NSVG_SPREAD_PAD = 0,
	NSVG_SPREAD_REFLECT = 1,
	NSVG_SPREAD_REPEAT = 2,
}

public enum NSVGlineJoin
{
	NSVG_JOIN_MITER = 0,
	NSVG_JOIN_ROUND = 1,
	NSVG_JOIN_BEVEL = 2,
}

public enum NSVGlineCap
{
	NSVG_CAP_BUTT = 0,
	NSVG_CAP_ROUND = 1,
	NSVG_CAP_SQUARE = 2,
}

public enum NSVGfillRule
{
	NSVG_FILLRULE_NONZERO = 0,
	NSVG_FILLRULE_EVENODD = 1,
}

public enum NSVGflags
{
	NSVG_FLAGS_VISIBLE = 0x01,
}

public enum NSVGpaintOrder
{
	NSVG_PAINT_FILL = 0x00,
	NSVG_PAINT_MARKERS = 0x01,
	NSVG_PAINT_STROKE = 0x02,
}

public partial struct NSVGgradientStop
{
	// [NativeTypeName("unsigned int")]
	public uint color;

	public float offset;
}

public partial struct NSVGgradient
{
	// [NativeTypeName("float[6]")]
	public _xform_e__FixedBuffer xform;

	// [NativeTypeName("char")]
	public sbyte spread;

	public float fx;

	public float fy;

	public int nstops;

	// [NativeTypeName("NSVGgradientStop[1]")]
	public _stops_e__FixedBuffer stops;

	[InlineArray(6)]
	public partial struct _xform_e__FixedBuffer
	{
		public float e0;
	}

	public partial struct _stops_e__FixedBuffer
	{
		public NSVGgradientStop e0;

		[UnscopedRef]
		public ref NSVGgradientStop this[int index] => ref Unsafe.Add(ref e0, index);

		[UnscopedRef]
		public Span<NSVGgradientStop> AsSpan(int length) => MemoryMarshal.CreateSpan(ref e0, length);
	}
}

public unsafe partial struct NSVGpaint
{
	// [NativeTypeName("signed char")]
	public sbyte type;

	// [NativeTypeName("__AnonymousRecord_nanosvg_L130_C2")]
	public _Anonymous_e__Union Anonymous;

	[UnscopedRef]
	public ref uint color => ref Anonymous.color;

	[UnscopedRef]
	public ref NSVGgradient* gradient => ref Anonymous.gradient;

	[StructLayout(LayoutKind.Explicit)]
	public unsafe partial struct _Anonymous_e__Union
	{
		[FieldOffset(0)]
		// [NativeTypeName("unsigned int")]
		public uint color;

		[FieldOffset(0)]
		public NSVGgradient* gradient;
	}
}

public unsafe partial struct NSVGpath
{
	public float* pts;

	public int npts;

	// [NativeTypeName("char")]
	public sbyte closed;

	// [NativeTypeName("float[4]")]
	public _bounds_e__FixedBuffer bounds;

	// [NativeTypeName("struct NSVGpath *")]
	public NSVGpath* next;

	[InlineArray(4)]
	public partial struct _bounds_e__FixedBuffer
	{
		public float e0;
	}
}

public unsafe partial struct NSVGshape
{
	// [NativeTypeName("char[64]")]
	public _id_e__FixedBuffer id;

	public NSVGpaint fill;

	public NSVGpaint stroke;

	public float opacity;

	public float strokeWidth;

	public float strokeDashOffset;

	// [NativeTypeName("float[8]")]
	public _strokeDashArray_e__FixedBuffer strokeDashArray;

	// [NativeTypeName("char")]
	public sbyte strokeDashCount;

	// [NativeTypeName("char")]
	public sbyte strokeLineJoin;

	// [NativeTypeName("char")]
	public sbyte strokeLineCap;

	public float miterLimit;

	// [NativeTypeName("char")]
	public sbyte fillRule;

	// [NativeTypeName("unsigned char")]
	public byte paintOrder;

	// [NativeTypeName("unsigned char")]
	public byte flags;

	// [NativeTypeName("float[4]")]
	public _bounds_e__FixedBuffer bounds;

	// [NativeTypeName("char[64]")]
	public _fillGradient_e__FixedBuffer fillGradient;

	// [NativeTypeName("char[64]")]
	public _strokeGradient_e__FixedBuffer strokeGradient;

	// [NativeTypeName("float[6]")]
	public _xform_e__FixedBuffer xform;

	public NSVGpath* paths;

	// [NativeTypeName("struct NSVGshape *")]
	public NSVGshape* next;

	[InlineArray(64)]
	public partial struct _id_e__FixedBuffer
	{
		public sbyte e0;
	}

	[InlineArray(8)]
	public partial struct _strokeDashArray_e__FixedBuffer
	{
		public float e0;
	}

	[InlineArray(4)]
	public partial struct _bounds_e__FixedBuffer
	{
		public float e0;
	}

	[InlineArray(64)]
	public partial struct _fillGradient_e__FixedBuffer
	{
		public sbyte e0;
	}

	[InlineArray(64)]
	public partial struct _strokeGradient_e__FixedBuffer
	{
		public sbyte e0;
	}

	[InlineArray(6)]
	public partial struct _xform_e__FixedBuffer
	{
		public float e0;
	}
}

public unsafe partial struct NSVGimage
{
	public float width;

	public float height;

	public NSVGshape* shapes;
}

public partial struct NSVGrasterizer;

public static unsafe partial class Methods
{
	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern NSVGimage* nsvgParseFromFile(/*[NativeTypeName("const char *")]*/ sbyte* filename, /*[NativeTypeName("const char *")]*/ sbyte* units, float dpi);

	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern NSVGimage* nsvgParse(/*[NativeTypeName("char *")]*/ sbyte* input, /*[NativeTypeName("const char *")]*/ sbyte* units, float dpi);

	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern NSVGpath* nsvgDuplicatePath(NSVGpath* p);

	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void nsvgDelete(NSVGimage* image);

	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern NSVGrasterizer* nsvgCreateRasterizer();

	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void nsvgRasterize(NSVGrasterizer* r, NSVGimage* image, float tx, float ty, float scale, /*[NativeTypeName("unsigned char *")]*/ byte* dst, int w, int h, int stride);

	[DllImport("nanosvg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void nsvgDeleteRasterizer(NSVGrasterizer* param0);
}

public static unsafe class NanoSVG
{
	static Color getNVGColor(uint color)
	{
		return new Color(
			(color >> 0) & 0xff,
			(color >> 8) & 0xff,
			(color >> 16) & 0xff,
			(color >> 24) & 0xff
		);
	}

	private static void nvgTransformInverse(
		NvgContext vg,
		InlineArray6<float> dst,
		NSVGgradient._xform_e__FixedBuffer src)
	{
		var transform = new NvgSharp.Transform
		{
			T1 = src[0],
			T2 = src[1],
			T3 = src[2],
			T4 = src[3],
			T5 = src[4],
			T6 = src[5]
		};
		transform = transform.BuildInverse();
		dst[0] = transform.T1;
		dst[1] = transform.T2;
		dst[2] = transform.T3;
		dst[3] = transform.T4;
		dst[4] = transform.T5;
		dst[5] = transform.T6;
	}

	private static void nvgTransformPoint(out float dstx, out float dsty, InlineArray6<float> xform, float srcx, float srcy)
	{
		var transform = new NvgSharp.Transform
		{
			T1 = xform[0],
			T2 = xform[1],
			T3 = xform[2],
			T4 = xform[3],
			T5 = xform[4],
			T6 = xform[5]
		};

		transform.TransformPoint(out dstx, out dsty, srcx, srcy);
	}

	private static Paint getPaint(NvgContext vg, NSVGpaint *p)
	{
		NSVGgradient *g;
		Color icol, ocol;
		var inverse = new InlineArray6<float>();
		Vector2 s, e;

		Debug.Assert(p->type == (sbyte)NSVG_PAINT_LINEAR_GRADIENT || p->type == (sbyte)NSVG_PAINT_RADIAL_GRADIENT);
		g = p->gradient;
		Debug.Assert(g->nstops >= 1);
		icol = getNVGColor(g->stops[0].color);
		ocol = getNVGColor(g->stops[g->nstops - 1].color);

		nvgTransformInverse(vg, inverse, g->xform);

		// FIXME: Is it always the case that the gradient should be transformed from (0, 0) to (0, 1)?
		nvgTransformPoint(out s.X, out s.Y, inverse, 0, 0);
		nvgTransformPoint(out e.X, out e.Y, inverse, 0, 1);

		if (p->type == (sbyte)NSVG_PAINT_LINEAR_GRADIENT)
		{
			return vg.LinearGradient(s.X, s.Y, e.X, e.Y, icol, ocol);
		}
		else
		{
			return vg.RadialGradient(s.X, s.Y, 0.0f, 160, icol, ocol);
		}
	}

	private static float getLineCrossing(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
	{
		Vector2 b, d, e;
		float m;

		b = p2 - p0;
		d = p1 - p0;
		e = p3 - p2;
		m = d.X * e.Y - d.Y * e.X;

		// Check if lines are parallel, or if either pair of points are equal
		if (float.Abs(m) < 1e-6)
		{
			return float.NaN;
		}
		return -(d.X * b.Y - d.Y * b.X) / m;
	}

	public static void nvgDrawSVG(NvgContext vg, NSVGimage *svg)
	{
		// Iterate shape linked list
		for (NSVGshape *shape = svg->shapes; shape != null; shape = shape->next)
		{
			// Visibility
			if ((shape->flags & (sbyte)NSVG_FLAGS_VISIBLE) == 0)
			{
				continue;
			}

			vg.SaveState();

			// Opacity
			if (shape->opacity < 1.0)
			{
				vg.GlobalAlpha(shape->opacity);
			}

			// Build path
			vg.BeginPath();

			// Iterate path linked list
			for (NSVGpath *path = shape->paths; path != null; path = path->next)
			{
				vg.MoveTo(path->pts[0], path->pts[1]);
				for (int i = 1; i < path->npts; i += 3)
				{
					float *p = &path->pts[2*i];
					vg.BezierTo(p[0], p[1], p[2], p[3], p[4], p[5]);
					// nvgLineTo(vg, p[4], p[5]);
				}

				// Close path
				if (path->closed != 0)
				{
					vg.ClosePath();
				}

				// Compute whether this is a hole or a solid.
				// Assume that no paths are crossing (usually true for normal SVG graphics).
				// Also assume that the topology is the same if we use straight lines rather than Beziers (not always the case but usually true).
				// Using the even-odd fill rule, if we draw a line from a point on the path to a point outside the boundary (e.g. top left) and count the number of times it crosses another path, the parity of this count determines whether the path is a hole (odd) or solid (even).
				int crossings = 0;
				Vector2 p0, p1;
				p0.X = path->pts[0];
				p0.Y = path->pts[1];
				p1.X = path->bounds[0] - 1.0f;
				p1.Y = path->bounds[1] - 1.0f;
				// Iterate all other paths
				for (NSVGpath *path2 = shape->paths; path2 != null; path2 = path2->next)
				{
					if (path2 == path)
					{
						continue;
					}

					// Iterate all lines on the path
					if (path2->npts < 4)
					{
						continue;
					}
					for (int i = 1; i < path2->npts + 3; i += 3)
					{
						float *p = &path2->pts[2*i];
						Vector2 p2, p3;
						// The previous point
						p2.X = p[-2];
						p2.Y = p[-1];
						// The current point
						if (i < path2->npts)
						{
							p3.X = p[4];
							p3.Y = p[5];
						}
						else
						{
							p3.X = path2->pts[0];
							p3.Y = path2->pts[1];
						}
						float crossing = getLineCrossing(p0, p1, p2, p3);
						float crossing2 = getLineCrossing(p2, p3, p0, p1);
						if (0.0 <= crossing && crossing < 1.0 && 0.0 <= crossing2)
						{
							crossings++;
						}
					}
				}

				if (crossings % 2 == 0)
				{
					vg.PathWinding(Solidity.Solid);
				}
				else
				{
					vg.PathWinding(Solidity.Hole);
				}
			}

			// Fill shape
			if (shape->fill.type != 0)
			{
				switch ((NSVGpaintType)shape->fill.type)
				{
					case NSVG_PAINT_COLOR:
					{
						var color = getNVGColor(shape->fill.color);
						vg.FillColor(color);
						break;
					}
					case NSVG_PAINT_LINEAR_GRADIENT:
					case NSVG_PAINT_RADIAL_GRADIENT:
					{
						vg.FillPaint(getPaint(vg, &shape->fill));
						break;
					}
				}
				vg.Fill();
			}

			// Stroke shape
			if (shape->stroke.type != 0)
			{
				vg.StrokeWidth(shape->strokeWidth);
				// strokeDashOffset, strokeDashArray, strokeDashCount not yet supported
				vg.LineCap(((NSVGlineCap)shape->strokeLineCap) switch
				{
					NSVG_CAP_BUTT => LineCap.Butt,
					NSVG_CAP_ROUND => LineCap.Round,
					NSVG_CAP_SQUARE => LineCap.Square,
					_ => throw new ArgumentOutOfRangeException()
				});
				vg.LineJoin(((NSVGlineJoin)shape->strokeLineJoin) switch
				{
					NSVG_JOIN_MITER => LineCap.Miter,
					NSVG_JOIN_ROUND => LineCap.Round,
					NSVG_JOIN_BEVEL => LineCap.Bevel,
					_ => throw new ArgumentOutOfRangeException()
				});

				switch ((NSVGpaintType)shape->stroke.type)
				{
					case NSVG_PAINT_COLOR:
					{
						var color = getNVGColor(shape->stroke.color);
						vg.StrokeColor(color);
						break;
					}
					case NSVG_PAINT_LINEAR_GRADIENT:
					{
						// NSVGgradient *g = shape->stroke.gradient;
						// printf("		lin grad: %f\t%f\n", g->fx, g->fy);
						break;
					}
				}
				vg.Stroke();
			}

			vg.RestoreState();
		}
	}
}

public unsafe class NanoSVGImage : IDisposable, IImage
{
	private NSVGimage* svg;

	public int Height => svg != null ? (int)svg->height : throw new ObjectDisposedException(nameof(NanoSVGImage), "Object was disposed");
	public int Width => svg != null ? (int)svg->width : throw new ObjectDisposedException(nameof(NanoSVGImage), "Object was disposed");

	public NanoSVGImage(ReadOnlySpan<byte> data, ReadOnlySpan<byte> units = default, float dpi = 96.0f)
	{
		// copy data to new native buffer and null-terminate
		var dataLen = Marshal.AllocHGlobal(data.Length + 1);
		try
		{
			var buffer = new Span<byte>((void*)dataLen, data.Length + 1);
			data.CopyTo(buffer);
			buffer[data.Length] = 0;

			fixed (byte* pData = buffer)
			fixed (byte* pUnits = units.IsEmpty ? "px"u8 : units)
			{
				var sdata = (sbyte*)pData;
				var sunits = (sbyte*)pUnits;
				svg = Methods.nsvgParse(sdata, sunits, dpi);
				if (svg == null)
				{
					throw new InvalidOperationException("Failed to parse SVG data");
				}
			}
		}
		finally
		{
			Marshal.FreeHGlobal(dataLen);
		}
	}

	public static NanoSVGImage FromStream(Stream stream, ReadOnlySpan<byte> units = default, float dpi = 96.0f)
	{
		using var arr = new ArrayPoolBufferWriter<byte>();
		stream.CopyTo(arr.AsStream());
		return new NanoSVGImage(arr.WrittenSpan, units, dpi);
	}

	public void Draw(NvgContext vg)
	{
		NanoSVG.nvgDrawSVG(vg, svg);
	}
	
	~NanoSVGImage()
	{
		Dispose();
	}

	public void Dispose()
	{
		if (svg != null)
		{
			svg = null;
			GC.SuppressFinalize(this);
			Methods.nsvgDelete(svg);
		}
	}
}