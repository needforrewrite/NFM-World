#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2024 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

namespace NFMWorld.FNA3D.Vertices
{
	public struct VertexElement(
		int offset,
		VertexElementFormat elementFormat,
		VertexElementUsage elementUsage,
		int usageIndex)
	{
		#region Public Properties

		public int Offset { get; set; } = offset;

		public VertexElementFormat VertexElementFormat { get; set; } = elementFormat;

		public VertexElementUsage VertexElementUsage { get; set; } = elementUsage;

		public int UsageIndex { get; set; } = usageIndex;

		#endregion

		#region Public Static Operators and Override Methods

		public override int GetHashCode()
		{
			// TODO: Fix hashes
			return 0;
		}

		public override string ToString()
		{
			return (
				"{{Offset:" + Offset.ToString() +
				" Format:" + VertexElementFormat.ToString() +
				" Usage:" + VertexElementUsage.ToString() +
				" UsageIndex: " + UsageIndex.ToString() +
				"}}"
			);
		}

		public override bool Equals(object? obj)
		{
			if (obj == null)
			{
				return false;
			}
			if (obj.GetType() != base.GetType())
			{
				return false;
			}
			return (this == ((VertexElement) obj));
		}

		public static bool operator ==(VertexElement left, VertexElement right)
		{
			return (	(left.Offset == right.Offset) &&
					(left.UsageIndex == right.UsageIndex) &&
					(left.VertexElementUsage == right.VertexElementUsage) &&
					(left.VertexElementFormat == right.VertexElementFormat)	);
		}

		public static bool operator !=(VertexElement left, VertexElement right)
		{
			return !(left == right);
		}

		#endregion
	}
}
