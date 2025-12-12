using System;
using System.Collections.Generic;
using System.Numerics;

namespace HoleyDiver;

public class PolygonTriangulator
{
    public struct TriangulationResult
    {
        public List<int> Triangles;
        public Vector3 PlaneNormal;
        public int RegionCount;
    }

    // Note: very basic, unoptimized implementation. Can be improved.
    public static TriangulationResult Triangulate(IReadOnlyList<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
            throw new ArgumentException("Must have at least 3 vertices");

        Vector3 centroid = ComputeCentroid(vertices);
        Vector3 normal = ComputeBestFitPlaneNormal(vertices, centroid);
        GetProjectionBasis(normal, out Vector3 uAxis, out Vector3 vAxis);

        var projected2D = new List<Vector2>();
        foreach (var vertex in vertices)
        {
            Vector3 relative = vertex - centroid;
            float u = Vector3.Dot(relative, uAxis);
            float v = Vector3.Dot(relative, vAxis);
            projected2D.Add(new Vector2(u, v));
        }

        const float epsilon = 1e-5f;
        var uniqueVertices = new List<Vector2>();
        var indexMap = new List<int>();

        for (int i = 0; i < projected2D.Count; i++)
        {
            int found = -1;
            for (int j = 0; j < uniqueVertices.Count; j++)
            {
                if (Vector2.Distance(projected2D[i], uniqueVertices[j]) < epsilon)
                {
                    found = j;
                    break;
                }
            }

            if (found >= 0)
            {
                indexMap.Add(found);
            }
            else
            {
                indexMap.Add(uniqueVertices.Count);
                uniqueVertices.Add(projected2D[i]);
            }
        }

        var initialPoly = new List<int>();
        for (int i = 0; i < vertices.Count; i++)
        {
            initialPoly.Add(indexMap[i]);
        }

        var polyLines = ExtractRegions(initialPoly, uniqueVertices);

        var allTriangles = new List<int>();

        foreach (var region in polyLines)
        {
            if (region.Count < 3) continue;

            var regionVerts = new List<Vector2>();
            foreach (var idx in region)
            {
                regionVerts.Add(uniqueVertices[idx]);
            }

            var tris = EarCutTriangulateSimple(regionVerts);

            for (int t = 0; t < tris.Count; t++)
            {
                int uniqueIdx = region[tris[t]];
                int origIdx = -1;
                for (int i = 0; i < indexMap.Count; i++)
                {
                    if (indexMap[i] == uniqueIdx)
                    {
                        origIdx = i;
                        break;
                    }
                }

                if (origIdx >= 0)
                    allTriangles.Add(origIdx);
            }
        }

        return new TriangulationResult
        {
            Triangles = allTriangles,
            PlaneNormal = normal,
            RegionCount = polyLines.Count
        };
    }

    private static List<List<int>> ExtractRegions(List<int> polyIndices, List<Vector2> vertices)
    {
        var polyLines = new List<List<int>>();
        polyLines.Add(new List<int>(polyIndices));

        int iteration = 0;
        while (polyLines[0].Count >= 6)
        {
            iteration++;
            int n = polyLines[0].Count;
            int bestI0 = -1, bestI1 = -1, bestLength = 0;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    int k0 = i;
                    int k1 = j;
                    int matchLength = 0;

                    Console.WriteLine($"i={i}, j={j}, polyLines[0][{i}]={polyLines[0][i]}, polyLines[0][{j}]={polyLines[0][j]}, equal={polyLines[0][i] == polyLines[0][j]}");
                    while (k0 != k1 && polyLines[0][k0] == polyLines[0][k1])
                    {
                        matchLength++;
                        k0 = (k0 + 1) % n;
                        k1 = (k1 - 1 + n) % n;
                    }

                    if (matchLength > bestLength)
                    {
                        bestLength = matchLength;
                        bestI0 = i;
                        bestI1 = j;
                    }
                }
            }

            Console.WriteLine($"Iteration {iteration}: n={n}, bestI0={bestI0}, bestI1={bestI1}, bestLength={bestLength}");
            Console.WriteLine($"  Current poly: [{string.Join(", ", polyLines[0])}]");

            if (bestLength >= 1)
            {
                // Show what vertices are matching
                Console.WriteLine($"  Matching vertices starting at {bestI0} and {bestI1}:");
                int k0 = bestI0, k1 = bestI1;
                for (int m = 0; m < bestLength; m++)
                {
                    Console.WriteLine($"    polyLines[0][{k0}]={polyLines[0][k0]} == polyLines[0][{k1}]={polyLines[0][k1]}");
                    k0 = (k0 + 1) % n;
                    k1 = (k1 - 1 + n) % n;
                }

                var newRegion = new List<int>();
                int start = (bestI0 + bestLength - 1) % n;
                int end = (bestI1 - (bestLength - 1) + n) % n;

                Console.WriteLine($"  Extracting from {start} to {end}");

                int k = start;
                while (k != end)
                {
                    newRegion.Add(polyLines[0][k]);
                    k = (k + 1) % n;
                }

                newRegion.Add(polyLines[0][end]);

                Console.WriteLine($"  New region: [{string.Join(", ", newRegion)}]");

                var toDelete = new bool[n];
                k = bestI0;
                while (k != bestI1)
                {
                    toDelete[k] = true;
                    k = (k + 1) % n;
                }

                Console.WriteLine($"  Deleting indices: [{string.Join(", ", Enumerable.Range(0, n).Where(i => toDelete[i]))}]");

                for (int idx = n - 1; idx >= 0; idx--)
                {
                    if (toDelete[idx])
                    {
                        polyLines[0].RemoveAt(idx);
                    }
                }

                Console.WriteLine($"  After deletion, polyLines[0]: [{string.Join(", ", polyLines[0])}]");

                bool poly0Valid = polyLines[0].Count >= 3;
                bool newRegionValid = newRegion.Count >= 3;

                if (poly0Valid && newRegionValid)
                {
                    var points0 = new List<Vector2>();
                    var pointsNew = new List<Vector2>();

                    foreach (var idx in polyLines[0])
                        points0.Add(vertices[idx]);
                    foreach (var idx in newRegion)
                        pointsNew.Add(vertices[idx]);

                    bool poly0InsideNew = AllPointsInPolygon(points0, pointsNew);
                    bool newInsidePoly0 = AllPointsInPolygon(pointsNew, points0);

                    Console.WriteLine($"  poly0InsideNew={poly0InsideNew}, newInsidePoly0={newInsidePoly0}");

                    if (poly0InsideNew && !newInsidePoly0)
                    {
                        Console.WriteLine($"  Swapping poly0 and newRegion");
                        var temp = polyLines[0];
                        polyLines[0] = newRegion;
                        newRegion = temp;
                    }

                    polyLines.Add(newRegion);
                }
                else if (newRegionValid && !poly0Valid)
                {
                    Console.WriteLine($"  Replacing poly0 with newRegion (poly0 invalid)");
                    polyLines[0] = newRegion;
                }
                else
                {
                    Console.WriteLine($"  Discarding newRegion (invalid)");
                }
            }
            else
            {
                Console.WriteLine($"  No match found, breaking");
                break;
            }
        }

        Console.WriteLine($"\nAfter extraction loop:");
        for (int i = 0; i < polyLines.Count; i++)
        {
            Console.WriteLine($"  polyLines[{i}]: [{string.Join(", ", polyLines[i])}]");
        }

        // Rest of the method...
        polyLines.RemoveAll(r => r.Count < 3);

        if (polyLines.Count == 0)
        {
            return new List<List<int>>();
        }

        int outerIdx = 0;
        float maxArea = 0;
        for (int i = 0; i < polyLines.Count; i++)
        {
            var verts = new List<Vector2>();
            foreach (var idx in polyLines[i])
                verts.Add(vertices[idx]);

            float area = Math.Abs(ComputeSignedArea(verts));
            if (area > maxArea)
            {
                maxArea = area;
                outerIdx = i;
            }
        }

        if (outerIdx != 0)
        {
            var temp = polyLines[0];
            polyLines[0] = polyLines[outerIdx];
            polyLines[outerIdx] = temp;
        }

        var holes = new List<List<int>>();
        for (int i = 1; i < polyLines.Count; i++)
        {
            holes.Add(polyLines[i]);
        }

        if (holes.Count > 0)
        {
            var combined = CombineWithHoles(polyLines[0], holes, vertices);
            return new List<List<int>> { combined };
        }

        return polyLines;
    }

    private static bool AllPointsInPolygon(List<Vector2> points, List<Vector2> polygon)
    {
        foreach (var p in points)
        {
            if (!PointInPolygon(p, polygon))
                return false;
        }

        return true;
    }

    private static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    private static List<int> CombineWithHoles(List<int> outer, List<List<int>> holes, List<Vector2> vertices)
    {
        var outerVerts = new List<Vector2>();
        foreach (var idx in outer)
            outerVerts.Add(vertices[idx]);

        if (ComputeSignedArea(outerVerts) < 0)
            outer.Reverse();

        for (int h = 0; h < holes.Count; h++)
        {
            var holeVerts = new List<Vector2>();
            foreach (var idx in holes[h])
                holeVerts.Add(vertices[idx]);

            if (ComputeSignedArea(holeVerts) > 0)
                holes[h].Reverse();
        }

        // Sort holes by rightmost X (descending)
        holes.Sort((a, b) =>
        {
            float maxXa = float.MinValue;
            float maxXb = float.MinValue;
            foreach (var idx in a) maxXa = Math.Max(maxXa, vertices[idx].X);
            foreach (var idx in b) maxXb = Math.Max(maxXb, vertices[idx].X);
            return maxXb.CompareTo(maxXa);
        });

        var result = new List<int>(outer);

        foreach (var hole in holes)
        {
            MergeHoleIntoPolygon(result, hole, vertices);
        }

        return result;
    }

    private static void MergeHoleIntoPolygon(List<int> polygon, List<int> hole, List<Vector2> vertices)
    {
        // Find rightmost vertex in hole
        int rightmostHoleIdx = 0;
        float maxX = vertices[hole[0]].X;
        for (int i = 1; i < hole.Count; i++)
        {
            if (vertices[hole[i]].X > maxX)
            {
                maxX = vertices[hole[i]].X;
                rightmostHoleIdx = i;
            }
        }

        Vector2 holePoint = vertices[hole[rightmostHoleIdx]];

        // Find bridge point on outer polygon
        int bridgePolyIdx = -1;
        float minDist = float.MaxValue;
        Vector2 intersectionPoint = Vector2.Zero;

        for (int i = 0; i < polygon.Count; i++)
        {
            int j = (i + 1) % polygon.Count;
            Vector2 p1 = vertices[polygon[i]];
            Vector2 p2 = vertices[polygon[j]];

            // Check if horizontal ray from holePoint intersects edge p1-p2
            if ((p1.Y <= holePoint.Y && p2.Y > holePoint.Y) ||
                (p2.Y <= holePoint.Y && p1.Y > holePoint.Y))
            {
                float t = (holePoint.Y - p1.Y) / (p2.Y - p1.Y);
                float intersectX = p1.X + t * (p2.X - p1.X);

                if (intersectX > holePoint.X)
                {
                    float dist = intersectX - holePoint.X;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        intersectionPoint = new Vector2(intersectX, holePoint.Y);
                        bridgePolyIdx = p1.X > p2.X ? i : j;
                    }
                }
            }
        }

        // Refine bridge vertex
        if (bridgePolyIdx >= 0)
        {
            Vector2 bridgeCandidate = vertices[polygon[bridgePolyIdx]];
            float bestAngle = float.MaxValue;
            int bestIdx = bridgePolyIdx;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p = vertices[polygon[i]];
                if (p.X >= holePoint.X &&
                    PointInTriangle(p, holePoint, intersectionPoint, bridgeCandidate))
                {
                    float angle = MathF.Abs(MathF.Atan2(p.Y - holePoint.Y, p.X - holePoint.X));
                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        bestIdx = i;
                    }
                }
            }

            bridgePolyIdx = bestIdx;
        }

        if (bridgePolyIdx < 0) return;

        // Build new polygon with hole merged in
        var newPolygon = new List<int>();

        // Add polygon vertices up to and including bridge point
        for (int i = 0; i <= bridgePolyIdx; i++)
            newPolygon.Add(polygon[i]);

        // Add hole vertices starting from rightmost
        for (int i = 0; i < hole.Count; i++)
        {
            int idx = (rightmostHoleIdx + i) % hole.Count;
            newPolygon.Add(hole[idx]);
        }

        // Close bridge back
        newPolygon.Add(hole[rightmostHoleIdx]);
        newPolygon.Add(polygon[bridgePolyIdx]);

        // Add remaining polygon vertices
        for (int i = bridgePolyIdx + 1; i < polygon.Count; i++)
            newPolygon.Add(polygon[i]);

        polygon.Clear();
        polygon.AddRange(newPolygon);
    }

    private static List<int> EarCutTriangulateSimple(List<Vector2> vertices)
    {
        var triangles = new List<int>();

        if (vertices.Count < 3)
            return triangles;

        bool reversed = false;
        var verts = new List<Vector2>(vertices);
        if (ComputeSignedArea(verts) < 0)
        {
            verts.Reverse();
            reversed = true;
        }

        var indices = new LinkedList<int>();
        for (int i = 0; i < verts.Count; i++)
            indices.AddLast(i);

        var node = indices.First;
        int remaining = indices.Count;
        int iterations = 0;
        int maxIterations = remaining * remaining * 2;

        while (remaining > 2 && iterations < maxIterations)
        {
            iterations++;
            var prev = node.Previous ?? indices.Last;
            var next = node.Next ?? indices.First;

            if (IsEar(verts, indices, prev.Value, node.Value, next.Value))
            {
                if (reversed)
                {
                    int n = vertices.Count;
                    triangles.Add(n - 1 - prev.Value);
                    triangles.Add(n - 1 - node.Value);
                    triangles.Add(n - 1 - next.Value);
                }
                else
                {
                    triangles.Add(prev.Value);
                    triangles.Add(node.Value);
                    triangles.Add(next.Value);
                }

                var toRemove = node;
                node = next;
                indices.Remove(toRemove);
                remaining--;
            }
            else
            {
                node = node.Next ?? indices.First;
            }
        }

        return triangles;
    }

    private static Vector3 ComputeCentroid(IReadOnlyList<Vector3> vertices)
    {
        Vector3 sum = Vector3.Zero;
        foreach (var v in vertices)
            sum += v;
        return sum / vertices.Count;
    }

    private static Vector3 ComputeBestFitPlaneNormal(IReadOnlyList<Vector3> vertices, Vector3 centroid)
    {
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;

        foreach (var v in vertices)
        {
            Vector3 r = v - centroid;
            xx += r.X * r.X;
            xy += r.X * r.Y;
            xz += r.X * r.Z;
            yy += r.Y * r.Y;
            yz += r.Y * r.Z;
            zz += r.Z * r.Z;
        }

        float detX = yy * zz - yz * yz;
        float detY = xx * zz - xz * xz;
        float detZ = xx * yy - xy * xy;

        float maxDet = Math.Max(detX, Math.Max(detY, detZ));

        Vector3 normal;
        if (maxDet == detX)
            normal = new Vector3(detX, xz * yz - xy * zz, xy * yz - xz * yy);
        else if (maxDet == detY)
            normal = new Vector3(xz * yz - xy * zz, detY, xy * xz - yz * xx);
        else
            normal = new Vector3(xy * yz - xz * yy, xy * xz - yz * xx, detZ);

        float length = normal.Length();
        if (length < 1e-10f)
        {
            normal = Vector3.Zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];
                normal.X += (current.Y - next.Y) * (current.Z + next.Z);
                normal.Y += (current.Z - next.Z) * (current.X + next.X);
                normal.Z += (current.X - next.X) * (current.Y + next.Y);
            }

            length = normal.Length();
        }

        return length > 1e-10f ? normal / length : Vector3.UnitZ;
    }

    private static void GetProjectionBasis(Vector3 normal, out Vector3 uAxis, out Vector3 vAxis)
    {
        Vector3 arbitrary = Math.Abs(normal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
        uAxis = Vector3.Normalize(Vector3.Cross(normal, arbitrary));
        vAxis = Vector3.Cross(normal, uAxis);
    }

    private static float ComputeSignedArea(List<Vector2> vertices)
    {
        float area = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            int j = (i + 1) % vertices.Count;
            area += vertices[i].X * vertices[j].Y;
            area -= vertices[j].X * vertices[i].Y;
        }

        return area / 2;
    }

    private static bool IsEar(List<Vector2> vertices, LinkedList<int> indices, int prev, int curr, int next)
    {
        Vector2 a = vertices[prev];
        Vector2 b = vertices[curr];
        Vector2 c = vertices[next];

        float cross = Cross(b - a, c - a);
        if (cross <= 1e-10f)
            return false;

        foreach (int idx in indices)
        {
            if (idx == prev || idx == curr || idx == next)
                continue;

            if (PointInTriangle(vertices[idx], a, b, c))
                return false;
        }

        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p - a, b - a);
        float d2 = Cross(p - b, c - b);
        float d3 = Cross(p - c, a - c);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }
}

public class Program
{
    public static void Main()
    {
//         Vector3[][] polys =
//         [
//             [
//                 new Vector3(-32, -10, 55),
//                 new Vector3(-34, -14, 0),
//                 new Vector3(-15, -14, 5),
//                 new Vector3(-15, -10, 52),
//             ],
//             [
//                 new Vector3(-15, -14, 5),
//                 new Vector3(-5, -14, 5),
//                 new Vector3(-5, -12, 52),
//                 new Vector3(-15, -10, 52),
//             ],
//             [
//                 new Vector3(32, -10, 55),
//                 new Vector3(34, -14, 0),
//                 new Vector3(15, -14, 5),
//                 new Vector3(15, -10, 52),
//             ],
//             [
//                 new Vector3(15, -14, 5),
//                 new Vector3(5, -14, 5),
//                 new Vector3(5, -12, 52),
//                 new Vector3(15, -10, 52),
//             ],
//             [
//                 new Vector3(-5, -14, 5),
//                 new Vector3(-5, -12, 52),
//                 new Vector3(5, -12, 52),
//                 new Vector3(5, -14, 5),
//             ],
//             [
//                 new Vector3(-31, -14, 0),
//                 new Vector3(-34, -14, 0),
//                 new Vector3(-25, -26, -17),
//                 new Vector3(-22, -26, -17),
//             ],
//             [
//                 new Vector3(31, -14, 0),
//                 new Vector3(34, -14, 0),
//                 new Vector3(25, -26, -17),
//                 new Vector3(22, -26, -17),
//             ],
//             [
//                 new Vector3(-15, -14, 5),
//                 new Vector3(-31, -14, 0),
//                 new Vector3(-22, -26, -17),
//                 new Vector3(0, -26, -13),
//                 new Vector3(22, -26, -17),
//                 new Vector3(31, -14, 0),
//                 new Vector3(15, -14, 5),
//             ],
//             [
//                 new Vector3(-23, -26, -44),
//                 new Vector3(-25, -26, -17),
//                 new Vector3(0, -26, -13),
//                 new Vector3(25, -26, -17),
//                 new Vector3(23, -26, -44),
//                 new Vector3(18, -26, -49),
//                 new Vector3(0, -26, -52),
//                 new Vector3(-18, -26, -49),
//             ],
//             [
//                 new Vector3(-25, -26, -17),
//                 new Vector3(-25, -26, -22),
//                 new Vector3(-34, -14, -5),
//                 new Vector3(-34, -14, 0),
//             ],
//             [
//                 new Vector3(25, -26, -17),
//                 new Vector3(25, -26, -22),
//                 new Vector3(34, -14, -5),
//                 new Vector3(34, -14, 0),
//             ],
//             [
//                 new Vector3(-25, -26, -22),
//                 new Vector3(-23, -26, -44),
//                 new Vector3(-32, -14, -55),
//                 new Vector3(-34, -14, -5),
//             ],
//             [
//                 new Vector3(25, -26, -22),
//                 new Vector3(23, -26, -44),
//                 new Vector3(32, -14, -55),
//                 new Vector3(34, -14, -5),
//             ],
//             [
//                 new Vector3(-23, -26, -44),
//                 new Vector3(-18, -26, -49),
//                 new Vector3(-25, -14, -66),
//                 new Vector3(-32, -14, -55),
//             ],
//             [
//                 new Vector3(23, -26, -44),
//                 new Vector3(18, -26, -49),
//                 new Vector3(25, -14, -66),
//                 new Vector3(32, -14, -55),
//             ],
//             [
//                 new Vector3(-25, -14, -66),
//                 new Vector3(-18, -26, -49),
//                 new Vector3(0, -26, -50),
//                 new Vector3(18, -26, -49),
//                 new Vector3(25, -14, -66),
//                 new Vector3(0, -14, -67),
//             ],
//             [
//                 new Vector3(-32, -14, -55),
//                 new Vector3(-35, -20, -110),
//                 new Vector3(-10, -14, -105),
//                 new Vector3(-10, -14, -67),
//                 new Vector3(-25, -14, -66),
//             ],
//             [
//                 new Vector3(32, -14, -55),
//                 new Vector3(35, -20, -110),
//                 new Vector3(10, -14, -105),
//                 new Vector3(10, -14, -67),
//                 new Vector3(25, -14, -66),
//             ],
//             [
//                 new Vector3(10, -14, -67),
//                 new Vector3(10, -14, -105),
//                 new Vector3(-10, -14, -105),
//                 new Vector3(-10, -14, -67),
//                 new Vector3(0, -14, -67),
//             ],
//             [
//                 new Vector3(-34, 7, 12),
//                 new Vector3(-34, 7, 0),
//                 new Vector3(-34, -14, 0),
//                 new Vector3(-32, -10, 55),
//                 new Vector3(-32, -3, 50),
//                 new Vector3(-32, 5, 50),
//                 new Vector3(-33, 7, 42),
//                 new Vector3(-33, -3, 37),
//                 new Vector3(-34, -3, 17),
//             ],
//             [
//                 new Vector3(-34, -5, 17),
//                 new Vector3(-34, -5, 4),
//                 new Vector3(-34, -11, 4),
//                 new Vector3(-34, -11, 17),
//                 new Vector3(-34, -5, 17),
//                 new Vector3(-34, -5, 16),
//                 new Vector3(-34, -11, 16),
//                 new Vector3(-34, -11, 13),
//                 new Vector3(-34, -5, 13),
//                 new Vector3(-34, -5, 12),
//                 new Vector3(-34, -11, 12),
//                 new Vector3(-34, -11, 9),
//                 new Vector3(-34, -5, 9),
//                 new Vector3(-34, -5, 8),
//                 new Vector3(-34, -11, 8),
//                 new Vector3(-34, -11, 5),
//                 new Vector3(-34, -5, 5),
//                 new Vector3(-34, -5, 4),
//                 new Vector3(-34, -11, 4),
//                 new Vector3(-34, -5, 4),
//             ],
//             [
//                 new Vector3(-32, -14, -55),
//                 new Vector3(-34, -14, 0),
//                 new Vector3(-34, 7, 0),
//                 new Vector3(-32, 7, -50),
//             ],
//             [
//                 new Vector3(-35, -20, -110),
//                 new Vector3(-32, -14, -55),
//                 new Vector3(-32, 7, -50),
//                 new Vector3(-31, 6, -58),
//                 new Vector3(-31, -3, -62),
//                 new Vector3(-32, -3, -82),
//                 new Vector3(-32, 2, -85),
//                 new Vector3(-35, 0, -100),
//                 new Vector3(-35, -10, -100),
//             ],
//
//             [
//                 new Vector3(-32, 5, 50),
//                 new Vector3(-33, 7, 42),
//                 new Vector3(-33, 11, 42),
//                 new Vector3(-32, 9, 50),
//             ],
//             [
//                 new Vector3(-34, 11, 12),
//                 new Vector3(-34, 7, 12),
//                 new Vector3(-34, 7, 0),
//                 new Vector3(-32, 7, -50),
//                 new Vector3(-32, 11, -50),
//             ],
//             [
//                 new Vector3(-32, 7, -50),
//                 new Vector3(-31, 6, -58),
//                 new Vector3(-31, 11, -58),
//                 new Vector3(-32, 11, -50),
//             ],
//             [
//                 new Vector3(-35, 0, -100),
//                 new Vector3(-32, 2, -85),
//                 new Vector3(-32, 7, -87),
//                 new Vector3(-32, 11, -87),
//             ],
//             [
//                 new Vector3(34, 7, 12),
//                 new Vector3(34, 7, 0),
//                 new Vector3(34, -14, 0),
//                 new Vector3(32, -10, 55),
//                 new Vector3(32, -3, 50),
//                 new Vector3(32, 5, 50),
//                 new Vector3(33, 7, 42),
//                 new Vector3(33, -3, 37),
//                 new Vector3(34, -3, 17),
//             ],
//             [
//                 new Vector3(34, -5, 17),
//                 new Vector3(34, -5, 4),
//                 new Vector3(34, -11, 4),
//                 new Vector3(34, -11, 17),
//                 new Vector3(34, -5, 17),
//                 new Vector3(34, -5, 16),
//                 new Vector3(34, -11, 16),
//                 new Vector3(34, -11, 13),
//                 new Vector3(34, -5, 13),
//                 new Vector3(34, -5, 12),
//                 new Vector3(34, -11, 12),
//                 new Vector3(34, -11, 9),
//                 new Vector3(34, -5, 9),
//                 new Vector3(34, -5, 8),
//                 new Vector3(34, -11, 8),
//                 new Vector3(34, -11, 5),
//                 new Vector3(34, -5, 5),
//                 new Vector3(34, -5, 4),
//                 new Vector3(34, -11, 4),
//                 new Vector3(34, -5, 4),
//             ],
//             [
//                 new Vector3(32, -14, -55),
//                 new Vector3(34, -14, 0),
//                 new Vector3(34, 7, 0),
//                 new Vector3(32, 7, -50),
//             ],
//             [
//                 new Vector3(35, -20, -110),
//                 new Vector3(32, -14, -55),
//                 new Vector3(32, 7, -50),
//                 new Vector3(31, 6, -58),
//                 new Vector3(31, -3, -62),
//                 new Vector3(32, -3, -82),
//                 new Vector3(32, 2, -85),
//                 new Vector3(35, 0, -100),
//                 new Vector3(35, -10, -100),
//             ],
//
//             [
//                 new Vector3(32, 5, 50),
//                 new Vector3(33, 7, 42),
//                 new Vector3(33, 11, 42),
//                 new Vector3(32, 9, 50),
//             ],
//             [
//                 new Vector3(34, 11, 12),
//                 new Vector3(34, 7, 12),
//                 new Vector3(34, 7, 0),
//                 new Vector3(32, 7, -50),
//                 new Vector3(32, 11, -50),
//             ],
//             [
//                 new Vector3(32, 7, -50),
//                 new Vector3(31, 6, -58),
//                 new Vector3(31, 11, -58),
//                 new Vector3(32, 11, -50),
//             ],
//             [
//                 new Vector3(35, 0, -100),
//                 new Vector3(32, 2, -85),
//                 new Vector3(32, 7, -87),
//                 new Vector3(32, 11, -87),
//             ],
//             [
//                 new Vector3(-31, -8, 50),
//                 new Vector3(-31, -1, 50),
//                 new Vector3(-15, -2, 50),
//                 new Vector3(-16, -6, 50),
//             ],
//             [
//                 new Vector3(31, -8, 50),
//                 new Vector3(31, -1, 50),
//                 new Vector3(15, -2, 50),
//                 new Vector3(16, -6, 50),
//             ],
//             [
//                 new Vector3(-5, -12, 52),
//                 new Vector3(-15, -10, 52),
//                 new Vector3(-13, -2, 50),
//                 new Vector3(13, -2, 50),
//                 new Vector3(15, -10, 52),
//                 new Vector3(5, -12, 52),
//             ],
//             [
//                 new Vector3(-15, -10, 52),
//                 new Vector3(-32, -10, 55),
//                 new Vector3(-32, -3, 50),
//                 new Vector3(-31, -1, 50),
//                 new Vector3(-31, -8, 50),
//                 new Vector3(-16, -6, 50),
//                 new Vector3(-15, -2, 50),
//                 new Vector3(-13, -2, 50),
//             ],
//             [
//                 new Vector3(15, -10, 52),
//                 new Vector3(32, -10, 55),
//                 new Vector3(32, -3, 50),
//                 new Vector3(31, -1, 50),
//                 new Vector3(31, -8, 50),
//                 new Vector3(16, -6, 50),
//                 new Vector3(15, -2, 50),
//                 new Vector3(13, -2, 50),
//             ],
//             [
//                 new Vector3(-32, 5, 50),
//                 new Vector3(32, 5, 50),
//                 new Vector3(32, -3, 50),
//                 new Vector3(31, -1, 50),
//                 new Vector3(15, -2, 50),
//                 new Vector3(13, -2, 50),
//                 new Vector3(-13, -2, 50),
//                 new Vector3(-15, -2, 50),
//                 new Vector3(-31, -1, 50),
//                 new Vector3(-32, -3, 50),
//             ],
//             [
//                 new Vector3(-32, 5, 50),
//                 new Vector3(-32, 9, 50),
//                 new Vector3(32, 9, 50),
//                 new Vector3(32, 5, 50),
//             ],
// // back
//             [
//                 new Vector3(-35, -20, -110),
//                 new Vector3(-35, -10, -100),
//                 new Vector3(-10, -10, -100),
//                 new Vector3(-10, -14, -105),
//             ],
//             [
//                 new Vector3(35, -20, -110),
//                 new Vector3(35, -10, -100),
//                 new Vector3(10, -10, -100),
//                 new Vector3(10, -14, -105),
//             ],
//             [
//                 new Vector3(-10, -14, -105),
//                 new Vector3(-10, -10, -100),
//                 new Vector3(10, -10, -100),
//                 new Vector3(10, -14, -105),
//             ],
//             [
//                 new Vector3(-33, -9, -100),
//                 new Vector3(-33, -4, -100),
//                 new Vector3(-17, -6, -100),
//                 new Vector3(-17, -9, -100),
//             ],
//             [
//                 new Vector3(33, -9, -100),
//                 new Vector3(33, -4, -100),
//                 new Vector3(17, -6, -100),
//                 new Vector3(17, -9, -100),
//             ],
//             [
//                 new Vector3(17, -6, -100),
//                 new Vector3(33, -4, -100),
//                 new Vector3(35, 0, -100),
//                 new Vector3(-35, 0, -100),
//                 new Vector3(-33, -4, -100),
//                 new Vector3(-17, -6, -100),
//                 new Vector3(-17, -9, -100),
//                 new Vector3(17, -9, -100),
//             ],
//             [
//                 new Vector3(-35, 0, -100),
//                 new Vector3(-35, -10, -100),
//                 new Vector3(35, -10, -100),
//                 new Vector3(35, 0, -100),
//                 new Vector3(33, -4, -100),
//                 new Vector3(33, -9, -100),
//                 new Vector3(17, -9, -100),
//                 new Vector3(-17, -9, -100),
//                 new Vector3(-33, -9, -100),
//                 new Vector3(-33, -4, -100),
//             ],
//             [
//                 new Vector3(-35, 0, -100),
//                 new Vector3(35, 0, -100),
//                 new Vector3(32, 11, -87),
//                 new Vector3(-32, 11, -87),
//             ],
// // bellow
//             [
//                 new Vector3(32, 9, 50),
//                 new Vector3(33, 11, 42),
//                 new Vector3(-33, 11, 42),
//                 new Vector3(-32, 9, 50),
//             ],
//             [
//                 new Vector3(34, 11, 12),
//                 new Vector3(33, 11, 42),
//                 new Vector3(-33, 11, 42),
//                 new Vector3(-34, 11, 12),
//             ],
//             [
//                 new Vector3(34, 11, 12),
//                 new Vector3(32, 11, -50),
//                 new Vector3(-32, 11, -50),
//                 new Vector3(-34, 11, 12),
//             ],
//             [
//                 new Vector3(31, 11, -58),
//                 new Vector3(32, 11, -50),
//                 new Vector3(-32, 11, -50),
//                 new Vector3(-31, 11, -58),
//             ],
//             [
//                 new Vector3(31, 11, -58),
//                 new Vector3(32, 11, -87),
//                 new Vector3(-32, 11, -87),
//                 new Vector3(-31, 11, -58),
//             ],
//         ];
//
//         var random = new Random();
//         foreach (var poly in polys)
//         {
//             Console.WriteLine("yield return (object[]) [");
//             var result = PolygonTriangulator.Triangulate(poly);
//             
//             Console.WriteLine($"Plane Normal: {result.PlaneNormal}");
//             Console.WriteLine($"Regions Detected: {result.RegionCount}");
//             Console.WriteLine($"Triangles: {result.Triangles.Count / 3}");
//             //
//             // for (int i = 0; i < result.Triangles.Count; i += 3)
//             // {
//             //     Console.WriteLine($"  Triangle: {result.Triangles[i]}, {result.Triangles[i+1]}, {result.Triangles[i+2]}");
//             // }
//             
//             // for (int i = 0; i < result.Triangles.Count; i += 3)
//             // {
//             //     Console.WriteLine("<p>");
//             //     Console.WriteLine($"c({random.Next(0, 256)},{random.Next(0, 256)},{random.Next(0, 256)})");
//             //     Console.WriteLine($"p({poly[result.Triangles[i]]:0})".Replace("<", "").Replace(">", "").Replace(", ", ","));
//             //     Console.WriteLine($"p({poly[result.Triangles[i+1]]:0})".Replace("<", "").Replace(">", "").Replace(", ", ","));
//             //     Console.WriteLine($"p({poly[result.Triangles[i+2]]:0})".Replace("<", "").Replace(">", "").Replace(", ", ","));
//             //     Console.WriteLine("</p>");
//             //     Console.WriteLine();
//             // }
//             
//             Console.WriteLine(
//                 """
//                     (Vector3[])
//                     [
//                 """);
//             foreach (var point in poly)
//             {
//                 Console.WriteLine($"        new Vector3({point:0}),".Replace("<", "").Replace(">", ""));
//             }
//             Console.WriteLine(
//                 """
//                     ],
//                 """);
//             
//             Console.WriteLine(
//                 """
//                     (Vector3[][])
//                     [
//                 """);
//             for (int i = 0; i < result.Triangles.Count; i += 3)
//             {
//                 Console.WriteLine("        [");
//                 Console.WriteLine($"            new Vector3({poly[result.Triangles[i]]:0}),".Replace("<", "").Replace(">", ""));
//                 Console.WriteLine($"            new Vector3({poly[result.Triangles[i+1]]:0}),".Replace("<", "").Replace(">", ""));
//                 Console.WriteLine($"            new Vector3({poly[result.Triangles[i+2]]:0}),".Replace("<", "").Replace(">", ""));
//                 Console.WriteLine("        ],");
//                 Console.WriteLine();
//             }
//             Console.WriteLine(
//                 $"""
//                     ],
//                     new Vector3({result.PlaneNormal.X}f, {result.PlaneNormal.Y}f, {result.PlaneNormal.Z}f),
//                     {result.RegionCount}
//                 """.Replace("<", "").Replace(">", ""));
//             Console.WriteLine("];");
//         }

        // polygon with self-intersecting path creating holes
        var vertices = new List<Vector3>
        {
            new Vector3(-47.6000023f,-45.9000015f,5.10000038f),
            new Vector3(-47.6000023f,-42.5f,5.10000038f),
            new Vector3(-37.4000015f,-42.5f,6.80000019f),
            new Vector3(0f,-42.5f,11.9000006f),
            new Vector3(37.4000015f,-42.5f,6.80000019f),
            new Vector3(47.6000023f,-42.5f,5.10000038f),
            new Vector3(47.6000023f,-45.9000015f,5.10000038f),
            new Vector3(37.4000015f,-45.9000015f,6.80000019f),
            new Vector3(0f,-45.9000015f,11.9000006f),
            new Vector3(-37.4000015f,-45.9000015f,6.80000019f),
        };
        
        var result = PolygonTriangulator.Triangulate(vertices);
        
        Console.WriteLine($"Plane Normal: {result.PlaneNormal}");
        Console.WriteLine($"Regions Detected: {result.RegionCount}");
        Console.WriteLine($"Triangles: {result.Triangles.Count / 3}");
        
        for (int i = 0; i < result.Triangles.Count; i += 3)
        {
            Console.WriteLine($"  Triangle: {result.Triangles[i]}, {result.Triangles[i+1]}, {result.Triangles[i+2]}");
        }
        
        for (int i = 0; i < result.Triangles.Count; i += 3)
        {
            Console.WriteLine("<p>");
            Console.WriteLine("c(255,0,0)");
            Console.WriteLine("gr(40)");
            Console.WriteLine("fs(1)");
            Console.WriteLine($"p(-{vertices[result.Triangles[i]]:0})".Replace("<", "").Replace(">", "").Replace(", ", ","));
            Console.WriteLine($"p(-{vertices[result.Triangles[i+1]]:0})".Replace("<", "").Replace(">", "").Replace(", ", ","));
            Console.WriteLine($"p(-{vertices[result.Triangles[i+2]]:0})".Replace("<", "").Replace(">", "").Replace(", ", ","));
            Console.WriteLine("</p>");
            Console.WriteLine();
        }
    }
}