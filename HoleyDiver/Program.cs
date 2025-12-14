using System;
using System.Collections.Generic;
using System.Numerics;

namespace HoleyDiver;

public class PolygonTriangulator
{
    public struct TriangulationResult
    {
        public int[] Triangles;
        public Vector3 PlaneNormal;
        public Vector3 Centroid;
        public int RegionCount;
    }

    public static TriangulationResult Triangulate(IReadOnlyList<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
            throw new ArgumentException("Must have at least 3 vertices");

        Vector3 centroid = ComputeCentroid(vertices);
        Vector3 normal = ComputeBestFitPlaneNormal(vertices, centroid);

        var projected2D = ProjectTo2D(vertices, centroid, normal);

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

        var uniqueIndices = new HashSet<int>(initialPoly);

        List<List<int>> polyLines;
        if (uniqueIndices.Count == initialPoly.Count)
        {
            polyLines = new List<List<int>> { initialPoly };
        }
        else
        {
            polyLines = ExtractRegions(initialPoly, uniqueVertices);
        }

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
            Triangles = allTriangles.ToArray(),
            PlaneNormal = normal,
            Centroid = centroid,
            RegionCount = polyLines.Count
        };
    }

    private static List<Vector2> ProjectTo2D(IReadOnlyList<Vector3> vertices, Vector3 centroid, Vector3 normal)
    {
        GetProjectionBasis(normal, out Vector3 uAxis, out Vector3 vAxis);

        var projected = new List<Vector2>();
        foreach (var vertex in vertices)
        {
            Vector3 relative = vertex - centroid;
            float u = Vector3.Dot(relative, uAxis);
            float v = Vector3.Dot(relative, vAxis);
            projected.Add(new Vector2(u, v));
        }

        if (IsValidProjection(vertices, projected))
        {
            return projected;
        }

        var projections = new (List<Vector2> proj, string name)[]
        {
            (ProjectToYZ(vertices), "YZ"),
            (ProjectToXZ(vertices), "XZ"),
            (ProjectToXY(vertices), "XY")
        };

        foreach (var (proj, name) in projections)
        {
            if (IsValidProjection(vertices, proj))
            {
                return proj;
            }
        }

        float bestArea = 0;
        List<Vector2> bestProj = projected;

        foreach (var (proj, name) in projections)
        {
            float area = Math.Abs(ComputeSignedArea(proj));
            if (area > bestArea)
            {
                bestArea = area;
                bestProj = proj;
            }
        }

        return bestProj;
    }

    private static bool IsValidProjection(IReadOnlyList<Vector3> vertices3D, List<Vector2> projected2D)
    {
        const float epsilon3D = 1e-5f;
        const float epsilon2D = 1e-5f;

        for (int i = 0; i < vertices3D.Count; i++)
        {
            for (int j = i + 1; j < vertices3D.Count; j++)
            {
                float dist3D = Vector3.Distance(vertices3D[i], vertices3D[j]);
                float dist2D = Vector2.Distance(projected2D[i], projected2D[j]);

                if (dist3D > epsilon3D && dist2D < epsilon2D)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<Vector2> ProjectToXY(IReadOnlyList<Vector3> vertices)
    {
        var result = new List<Vector2>();
        foreach (var v in vertices)
            result.Add(new Vector2(v.X, v.Y));
        return result;
    }

    private static List<Vector2> ProjectToXZ(IReadOnlyList<Vector3> vertices)
    {
        var result = new List<Vector2>();
        foreach (var v in vertices)
            result.Add(new Vector2(v.X, v.Z));
        return result;
    }

    private static List<Vector2> ProjectToYZ(IReadOnlyList<Vector3> vertices)
    {
        var result = new List<Vector2>();
        foreach (var v in vertices)
            result.Add(new Vector2(v.Y, v.Z));
        return result;
    }

    private static List<List<int>> ExtractRegions(List<int> polyIndices, List<Vector2> vertices)
    {
        var polyLines = new List<List<int>>();
        polyLines.Add(new List<int>(polyIndices));

        int safetyLimit = polyIndices.Count * polyIndices.Count;
        int outerIterations = 0;

        // Keep extracting until no more mirrored sequences are found
        bool foundMatch = true;
        while (foundMatch && outerIterations < safetyLimit)
        {
            foundMatch = false;
            outerIterations++;

            // Process polyLines[0] if it's large enough
            if (polyLines[0].Count < 4)
                break;

            int n = polyLines[0].Count;
            int bestI0 = -1, bestI1 = -1, bestLength = 0;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;

                    int k0 = i;
                    int k1 = j;
                    int matchLength = 0;
                    int maxMatchIterations = n;

                    while (k0 != k1 && polyLines[0][k0] == polyLines[0][k1] && matchLength < maxMatchIterations)
                    {
                        matchLength++;

                        int nextK0 = (k0 + 1) % n;
                        int nextK1 = (k1 - 1 + n) % n;

                        if (nextK0 == k1 || k0 == nextK1)
                            break;

                        k0 = nextK0;
                        k1 = nextK1;
                    }

                    if (matchLength > bestLength)
                    {
                        bestLength = matchLength;
                        bestI0 = i;
                        bestI1 = j;
                    }
                }
            }

            if (bestLength >= 1)
            {
                foundMatch = true;

                var newRegion = new List<int>();
                int start = (bestI0 + bestLength - 1) % n;
                int end = (bestI1 - (bestLength - 1) + n) % n;

                int k = start;
                int safetyCounter = 0;
                while (k != end && safetyCounter <= n)
                {
                    newRegion.Add(polyLines[0][k]);
                    k = (k + 1) % n;
                    safetyCounter++;
                }

                if (safetyCounter <= n)
                {
                    newRegion.Add(polyLines[0][end]);
                }

                var toDelete = new bool[n];
                k = bestI0;
                safetyCounter = 0;
                while (k != bestI1 && safetyCounter < n)
                {
                    toDelete[k] = true;
                    k = (k + 1) % n;
                    safetyCounter++;
                }

                for (int idx = n - 1; idx >= 0; idx--)
                {
                    if (toDelete[idx])
                    {
                        polyLines[0].RemoveAt(idx);
                    }
                }

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

                    if (poly0InsideNew && !newInsidePoly0)
                    {
                        var temp = polyLines[0];
                        polyLines[0] = newRegion;
                        newRegion = temp;
                    }

                    polyLines.Add(newRegion);
                }
                else if (newRegionValid && !poly0Valid)
                {
                    polyLines[0] = newRegion;
                }
            }
        }

        // Remove consecutive duplicate indices from all regions
        for (int ri = 0; ri < polyLines.Count; ri++)
        {
            polyLines[ri] = RemoveConsecutiveDuplicates(polyLines[ri]);
        }

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
            // Check if any hole shares vertices with the outer boundary
            // If so, they're not true holes but connected regions
            var outerSet = new HashSet<int>(polyLines[0]);
            bool hasSharedVertices = false;

            foreach (var hole in holes)
            {
                foreach (var idx in hole)
                {
                    if (outerSet.Contains(idx))
                    {
                        hasSharedVertices = true;
                        break;
                    }
                }
                if (hasSharedVertices) break;
            }

            if (hasSharedVertices)
            {
                // Return all regions as separate polygons to triangulate
                var allRegions = new List<List<int>> { polyLines[0] };
                allRegions.AddRange(holes);
                return allRegions;
            }

            // True holes - merge them into the outer boundary
            var combined = CombineWithHoles(polyLines[0], holes, vertices);
            return new List<List<int>> { combined };
        }

        return polyLines;
    }

    private static List<int> RemoveConsecutiveDuplicates(List<int> indices)
    {
        if (indices.Count < 2)
            return indices;

        var result = new List<int>();
        for (int i = 0; i < indices.Count; i++)
        {
            int next = (i + 1) % indices.Count;
            if (indices[i] != indices[next] || i == indices.Count - 1)
            {
                // Only add if not a duplicate of the next, or if it's the last element
                // But also check if last element equals first
                if (result.Count == 0 || indices[i] != result[result.Count - 1])
                {
                    result.Add(indices[i]);
                }
            }
        }

        // Check if first and last are the same
        if (result.Count > 1 && result[0] == result[result.Count - 1])
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
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
        var outerCopy = new List<int>(outer);

        var outerVerts = new List<Vector2>();
        foreach (var idx in outerCopy)
            outerVerts.Add(vertices[idx]);

        if (ComputeSignedArea(outerVerts) < 0)
            outerCopy.Reverse();

        var holesCopy = new List<List<int>>();
        for (int h = 0; h < holes.Count; h++)
        {
            var holeCopy = new List<int>(holes[h]);
            var holeVerts = new List<Vector2>();
            foreach (var idx in holeCopy)
                holeVerts.Add(vertices[idx]);

            if (ComputeSignedArea(holeVerts) > 0)
                holeCopy.Reverse();

            holesCopy.Add(holeCopy);
        }

        holesCopy.Sort((a, b) =>
        {
            float maxXa = float.MinValue;
            float maxXb = float.MinValue;
            foreach (var idx in a) maxXa = Math.Max(maxXa, vertices[idx].X);
            foreach (var idx in b) maxXb = Math.Max(maxXb, vertices[idx].X);
            return maxXb.CompareTo(maxXa);
        });

        var result = new List<int>(outerCopy);

        foreach (var hole in holesCopy)
        {
            MergeHoleIntoPolygon(result, hole, vertices);
        }

        return result;
    }

    private static void MergeHoleIntoPolygon(List<int> polygon, List<int> hole, List<Vector2> vertices)
    {
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

        int bridgePolyIdx = -1;
        float minDist = float.MaxValue;
        Vector2 intersectionPoint = Vector2.Zero;

        for (int i = 0; i < polygon.Count; i++)
        {
            int j = (i + 1) % polygon.Count;
            Vector2 p1 = vertices[polygon[i]];
            Vector2 p2 = vertices[polygon[j]];

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

        var newPolygon = new List<int>();

        for (int i = 0; i <= bridgePolyIdx; i++)
            newPolygon.Add(polygon[i]);

        for (int i = 0; i < hole.Count; i++)
        {
            int idx = (rightmostHoleIdx + i) % hole.Count;
            newPolygon.Add(hole[idx]);
        }

        newPolygon.Add(hole[rightmostHoleIdx]);
        newPolygon.Add(polygon[bridgePolyIdx]);

        for (int i = bridgePolyIdx + 1; i < polygon.Count; i++)
            newPolygon.Add(polygon[i]);

        polygon.Clear();
        polygon.AddRange(newPolygon);
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

    private static List<int> EarCutTriangulateSimple(List<Vector2> vertices)
    {
        var triangles = new List<int>();

        if (vertices.Count < 3)
            return triangles;

        // Remove duplicate consecutive vertices (including wrap-around from last to first)
        var cleanIndices = new List<int>();
        for (int i = 0; i < vertices.Count; i++)
        {
            int nextI = (i + 1) % vertices.Count;
            if (Vector2.Distance(vertices[i], vertices[nextI]) > 1e-5f)
            {
                cleanIndices.Add(i);
            }
        }

        if (cleanIndices.Count < 3)
            return triangles;

        // Build cleaned vertex list
        var cleanVerts = new List<Vector2>();
        foreach (var idx in cleanIndices)
        {
            cleanVerts.Add(vertices[idx]);
        }

        float area = ComputeSignedArea(cleanVerts);

        // If clockwise, reverse both the vertices and the index mapping
        if (area < 0)
        {
            cleanVerts.Reverse();
            cleanIndices.Reverse();
        }

        var nodeIndices = new LinkedList<int>();
        for (int i = 0; i < cleanVerts.Count; i++)
            nodeIndices.AddLast(i);

        var node = nodeIndices.First;
        int remaining = nodeIndices.Count;
        int iterations = 0;
        int maxIterations = remaining * remaining * 2;

        while (remaining > 2 && iterations < maxIterations)
        {
            iterations++;
            bool madeProgress = false;

            var startNode = node;
            int loopCount = 0;
            do
            {
                loopCount++;
                var prev = node.Previous ?? nodeIndices.Last;
                var next = node.Next ?? nodeIndices.First;

                bool isEar = IsEar(cleanVerts, nodeIndices, prev.Value, node.Value, next.Value);

                if (isEar)
                {
                    // Map back to original vertex indices
                    triangles.Add(cleanIndices[prev.Value]);
                    triangles.Add(cleanIndices[node.Value]);
                    triangles.Add(cleanIndices[next.Value]);

                    var toRemove = node;
                    node = next;
                    nodeIndices.Remove(toRemove);
                    remaining--;
                    madeProgress = true;
                    break;
                }
                else
                {
                    node = node.Next ?? nodeIndices.First;
                }
            } while (node != startNode && remaining > 2);

            if (!madeProgress)
            {
                var currNode = nodeIndices.First;
                bool foundAny = false;

                while (currNode != null && remaining > 2)
                {
                    var prevNode = currNode.Previous ?? nodeIndices.Last;
                    var nextNode = currNode.Next ?? nodeIndices.First;

                    if (IsEarRelaxed(cleanVerts, nodeIndices, prevNode.Value, currNode.Value, nextNode.Value))
                    {
                        triangles.Add(cleanIndices[prevNode.Value]);
                        triangles.Add(cleanIndices[currNode.Value]);
                        triangles.Add(cleanIndices[nextNode.Value]);

                        var toRemove = currNode;
                        node = nextNode;
                        nodeIndices.Remove(toRemove);
                        remaining--;
                        foundAny = true;
                        break;
                    }

                    currNode = currNode.Next;
                }

                if (!foundAny)
                    break;
            }
        }

        return triangles;
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

            // Skip vertices that are at the same position as any triangle vertex
            // (can happen after hole merging creates bridge edges)
            Vector2 p = vertices[idx];
            if (Vector2.DistanceSquared(p, a) < 1e-10f ||
                Vector2.DistanceSquared(p, b) < 1e-10f ||
                Vector2.DistanceSquared(p, c) < 1e-10f)
                continue;

            if (PointInTriangleStrict(p, a, b, c))
                return false;
        }

        return true;
    }

    private static bool IsEarRelaxed(List<Vector2> vertices, LinkedList<int> indices, int prev, int curr, int next)
    {
        Vector2 a = vertices[prev];
        Vector2 b = vertices[curr];
        Vector2 c = vertices[next];

        float cross = Cross(b - a, c - a);
        if (cross < -1e-10f)
            return false;

        if (Math.Abs(cross) < 1e-10f)
            return false;

        foreach (int idx in indices)
        {
            if (idx == prev || idx == curr || idx == next)
                continue;

            // Skip vertices that are at the same position as any triangle vertex
            Vector2 p = vertices[idx];
            if (Vector2.DistanceSquared(p, a) < 1e-10f ||
                Vector2.DistanceSquared(p, b) < 1e-10f ||
                Vector2.DistanceSquared(p, c) < 1e-10f)
                continue;

            if (PointInTriangleStrict(p, a, b, c))
                return false;
        }

        return true;
    }

    private static bool PointInTriangleStrict(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p - a, b - a);
        float d2 = Cross(p - b, c - b);
        float d3 = Cross(p - c, a - c);

        bool hasNeg = (d1 < -1e-10f) || (d2 < -1e-10f) || (d3 < -1e-10f);
        bool hasPos = (d1 > 1e-10f) || (d2 > 1e-10f) || (d3 > 1e-10f);

        return !(hasNeg && hasPos);
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
        var vertices = new List<Vector3>
        {
            new Vector3(-30,-18,85),
            new Vector3(-44,-15,79),
            new Vector3(-44,-7,87),

            new Vector3(-17,-7,104),
            new Vector3(-15,-8,103),

            new Vector3(-21,-9,100),
            new Vector3(-39,-10,87),
            new Vector3(-39,-14,83),
            new Vector3(-33,-15,85),
            new Vector3(-20,-10,99),
            new Vector3(-21,-9,100),

            new Vector3(-15,-8,103),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Console.WriteLine($"Plane Normal: {result.PlaneNormal}");
        Console.WriteLine($"Regions Detected: {result.RegionCount}");
        Console.WriteLine($"Triangles: {result.Triangles.Length / 3}");

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            Console.WriteLine(
                $"  Triangle: {result.Triangles[i]}, {result.Triangles[i + 1]}, {result.Triangles[i + 2]}");
        }

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            Console.WriteLine("[");
            Console.WriteLine($"new Vector3({vertices[result.Triangles[i]]}),".Replace("<", "").Replace(">", ""));
            Console.WriteLine($"new Vector3({vertices[result.Triangles[i + 1]]}),".Replace("<", "").Replace(">", ""));
            Console.WriteLine($"new Vector3({vertices[result.Triangles[i + 2]]}),".Replace("<", "").Replace(">", ""));
            Console.WriteLine("],");
            Console.WriteLine();
        }

        // for (int i = 0; i < result.Triangles.Length; i += 3)
        // {
        //     Console.WriteLine("<p>");
        //     Console.WriteLine("c(255,0,0)");
        //     Console.WriteLine("gr(40)");
        //     Console.WriteLine("fs(1)");
        //     Console.WriteLine($"p({vertices[result.Triangles[i]]})".Replace("<", "").Replace(">", "")
        //         .Replace(", ", ","));
        //     Console.WriteLine($"p({vertices[result.Triangles[i + 1]]})".Replace("<", "").Replace(">", "")
        //         .Replace(", ", ","));
        //     Console.WriteLine($"p({vertices[result.Triangles[i + 2]]})".Replace("<", "").Replace(">", "")
        //         .Replace(", ", ","));
        //     Console.WriteLine("</p>");
        //     Console.WriteLine();
        // }
    }
}