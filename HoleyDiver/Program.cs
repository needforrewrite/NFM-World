using EarcutNet;
using Microsoft.Xna.Framework;
using Poly2Tri;

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

        var vertices2d = ProjectTo2D(vertices, centroid, normal);

        var indexMap = new List<int>(vertices2d.Count);
        for (int i = 0; i < vertices2d.Count; i++)
        {
            indexMap.Add(i);
        }

        List<List<int>> polyLines = ExtractRegions(indexMap, vertices2d);

        var outerPoly = polyLines[0];
        var holePolys = polyLines[1..];

        // For polygons with holes, we need to include hole vertices in the triangulation
        // Build a complete vertex list and use constrained triangulation
        var allTriangles = new List<int>();

        // If there are holes, we need to use a different approach
        // Since simple centroid filtering doesn't work well, let's try adding hole vertices
        // to create a proper constrained triangulation

        // Use Mapbox Earcut.NET triangulation
        // var tessellation = Earcut.Tessellate(new double[] {
        //     0,   0,                 // Vertex 0 (outline)
        //     100,   0,                 // Vertex 1 (outline)
        //     100, 100,                 // Vertex 2 (outline)
        //     0, 100,                 // Vertex 3 (outline)
        //     20,  20,                 // Vertex 4 (hole)
        //     80,  20,                 // Vertex 5 (hole)
        //     80,  80,                 // Vertex 6 (hole)
        //     20,  80                  // Vertex 7 (hole)
        // }, new int[] {
        //     4                        // Index of the first Vertex of the hole
        // });

        var points = new List<double>();
        var holes = new List<int>();

        var index = 0;
        foreach (var point in outerPoly)
        {
            points.Add(vertices2d[point].X);
            points.Add(vertices2d[point].Y);
            index++;
        }
        
        foreach (var hole in holePolys)
        {
            holes.Add(index);
            foreach (var point in hole)
            {
                points.Add(vertices2d[point].X);
                points.Add(vertices2d[point].Y);
                index++;
            }
        }
        
        Console.WriteLine($"Triangulating polygon with {outerPoly.Count} outer vertices and {holePolys.Count} holes");
        Console.WriteLine(string.Join(", ", points));
        Console.WriteLine(string.Join(", ", holes));

        var tessellation = Earcut.Tessellate(points, holes);

        // tessellation contains a flat list of vertex indices. Each group of three indices forms a triangle.
        allTriangles.AddRange(tessellation);

        return new TriangulationResult
        {
            Triangles = allTriangles.ToArray(),
            PlaneNormal = normal,
            Centroid = centroid,
            RegionCount = 1 + holePolys.Count
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
        var result = new List<Vector2>(vertices.Count);
        foreach (var v in vertices)
            result.Add(new Vector2(v.X, v.Y));
        return result;
    }

    private static List<Vector2> ProjectToXZ(IReadOnlyList<Vector3> vertices)
    {
        var result = new List<Vector2>(vertices.Count);
        foreach (var v in vertices)
            result.Add(new Vector2(v.X, v.Z));
        return result;
    }

    private static List<Vector2> ProjectToYZ(IReadOnlyList<Vector3> vertices)
    {
        var result = new List<Vector2>(vertices.Count);
        foreach (var v in vertices)
            result.Add(new Vector2(v.Y, v.Z));
        return result;
    }

    private static List<List<int>> ExtractRegions(List<int> polyIndices, List<Vector2> vertices)
    {
        // Direct port of Python hole-finding algorithm
        List<List<int>> polyLines = [polyIndices];

        while (polyLines[0].Count >= 6)
        {
            polyLines.Add([]);

            int i0 = -1, i1 = -1, le = 0;
            int n = polyLines[0].Count;

            // Find the longest mirrored sequence
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    int k0 = i;
                    int k1 = j;
                    int cle = 0;

                    while (k0 != k1 && polyLines[0][k0] == polyLines[0][k1])
                    {
                        cle++;
                        k0 = (k0 + 1) % n;
                        k1 = (k1 - 1 + n) % n;
                    }

                    if (cle > le)
                    {
                        le = cle;
                        i0 = i;
                        i1 = j;
                    }
                }
            }

            if (le >= 1)
            {
                var delete = new bool[n];

                // Extract the new region (hole)
                int k = (i0 + le - 1) % n;
                int endK = (i1 - (le - 1) + n) % n;
                while (k != endK)
                {
                    polyLines[^1].Add(polyLines[0][k]);
                    k = (k + 1) % n;
                }

                // Mark indices to delete from outer
                k = i0;
                while (k != i1)
                {
                    delete[k] = true;
                    k = (k + 1) % n;
                }

                // Delete marked indices in reverse order
                for (int idx = n - 1; idx >= 0; idx--)
                {
                    if (delete[idx])
                    {
                        polyLines[0].RemoveAt(idx);
                    }
                }

                if (le == 1)
                {
                    if (Math.Min(polyLines[0].Count, polyLines[^1].Count) >= 3)
                    {
                        // Check if poly0 is inside the new region (swap if needed)
                        var points0 = new List<Vector2>();
                        var pointsNew = new List<Vector2>();

                        foreach (var idx in polyLines[0])
                            points0.Add(vertices[idx]);
                        foreach (var idx in polyLines[^1])
                            pointsNew.Add(vertices[idx]);

                        // If all points of poly0 are inside pointsNew, swap them
                        if (AllPointsInPolygon(points0, pointsNew))
                        {
                            (polyLines[0], polyLines[^1]) = (polyLines[^1], polyLines[0]);
                        }
                    }
                    else
                    {
                        if (polyLines[^1].Count < 3)
                        {
                            polyLines.RemoveAt(polyLines.Count - 1);
                        }
                        else
                        {
                            (polyLines[0], polyLines[^1]) = (polyLines[^1], polyLines[0]);
                            polyLines.RemoveAt(polyLines.Count - 1);
                        }
                    }
                }
            }
            else
            {
                polyLines.RemoveAt(polyLines.Count - 1);
                break;
            }
        }
        //
        // // Remove consecutive duplicate indices from all regions
        // for (int ri = 0; ri < polyLines.Count; ri++)
        // {
        //     polyLines[ri] = RemoveConsecutiveDuplicates(polyLines[ri]);
        // }
        //
        // polyLines.RemoveAll(r => r.Count < 3);
        //
        // if (polyLines.Count == 0)
        // {
        //     return new List<List<int>>();
        // }
        //
        // // Find the outer polygon (largest area)
        // int outerIdx = 0;
        // float maxArea = 0;
        // for (int i = 0; i < polyLines.Count; i++)
        // {
        //     var verts = new List<Vector2>();
        //     foreach (var idx in polyLines[i])
        //         verts.Add(vertices[idx]);
        //
        //     float area = Math.Abs(ComputeSignedArea(verts));
        //     if (area > maxArea)
        //     {
        //         maxArea = area;
        //         outerIdx = i;
        //     }
        // }
        //
        // if (outerIdx != 0)
        // {
        //     (polyLines[0], polyLines[outerIdx]) = (polyLines[outerIdx], polyLines[0]);
        // }
        //
        // // Mark holes with -1 prefix so they're passed to Poly2Tri as constraints
        // if (polyLines.Count > 1)
        // {
        //     var result = new List<List<int>> { polyLines[0] };
        //     for (int i = 1; i < polyLines.Count; i++)
        //     {
        //         var markedHole = new List<int> { -1 };
        //         markedHole.AddRange(polyLines[i]);
        //         result.Add(markedHole);
        //     }
        //     return result;
        // }

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

        // Sort holes by their centroid Y coordinate (bottom to top) to minimize bridge crossings
        holesCopy.Sort((a, b) =>
        {
            float sumYa = 0, sumYb = 0;
            foreach (var idx in a) sumYa += vertices[idx].Y;
            foreach (var idx in b) sumYb += vertices[idx].Y;
            float avgYa = sumYa / a.Count;
            float avgYb = sumYb / b.Count;
            return avgYa.CompareTo(avgYb);
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
        // Count how many times each vertex appears in the polygon
        // (to avoid bridging to vertices that are already part of a bridge)
        var vertexCounts = new Dictionary<int, int>();
        foreach (var idx in polygon)
        {
            if (!vertexCounts.ContainsKey(idx))
                vertexCounts[idx] = 0;
            vertexCounts[idx]++;
        }

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

                    bool iIsBridge = vertexCounts.GetValueOrDefault(polygon[i], 0) > 1;
                    bool jIsBridge = vertexCounts.GetValueOrDefault(polygon[j], 0) > 1;

                    // Skip edges where both vertices are already bridge points
                    // (these edges are part of previously merged holes)
                    if (iIsBridge && jIsBridge)
                        continue;

                    if (dist < minDist)
                    {
                        minDist = dist;
                        intersectionPoint = new Vector2(intersectX, holePoint.Y);

                        if (iIsBridge && !jIsBridge)
                            bridgePolyIdx = j;
                        else if (!iIsBridge && jIsBridge)
                            bridgePolyIdx = i;
                        else
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
                // Skip vertices that are already bridge points (appear multiple times)
                if (vertexCounts.GetValueOrDefault(polygon[i], 0) > 1)
                    continue;

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

    private static List<int> ComputeConvexHull(List<Vector2> vertices)
    {
        if (vertices.Count < 3)
            return Enumerable.Range(0, vertices.Count).ToList();

        // Use Graham scan algorithm to compute convex hull
        // Find the point with lowest Y (and leftmost if tie)
        int minIdx = 0;
        for (int i = 1; i < vertices.Count; i++)
        {
            if (vertices[i].Y < vertices[minIdx].Y ||
                (vertices[i].Y == vertices[minIdx].Y && vertices[i].X < vertices[minIdx].X))
            {
                minIdx = i;
            }
        }

        Vector2 pivot = vertices[minIdx];

        // Sort points by polar angle with respect to pivot
        var indices = Enumerable.Range(0, vertices.Count).ToList();
        indices.RemoveAt(minIdx);

        indices.Sort((a, b) =>
        {
            Vector2 va = vertices[a] - pivot;
            Vector2 vb = vertices[b] - pivot;

            float cross = va.X * vb.Y - va.Y * vb.X;
            if (Math.Abs(cross) > 1e-9f)
                return cross > 0 ? -1 : 1;

            // Collinear - sort by distance
            float distA = va.LengthSquared();
            float distB = vb.LengthSquared();
            return distA.CompareTo(distB);
        });

        // Build convex hull using stack
        var hull = new List<int> { minIdx };

        foreach (var idx in indices)
        {
            // Remove points that make a right turn
            while (hull.Count >= 2)
            {
                Vector2 p1 = vertices[hull[hull.Count - 2]];
                Vector2 p2 = vertices[hull[hull.Count - 1]];
                Vector2 p3 = vertices[idx];

                float cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
                if (cross > 1e-9f)
                    break;

                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(idx);
        }

        return hull;
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
        var cleanVerts = new List<Vector2>(cleanIndices.Count);
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

        var node = nodeIndices.First!;
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
                var prev = node.Previous ?? nodeIndices.Last!;
                var next = node.Next ?? nodeIndices.First!;

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
                    node = node.Next ?? nodeIndices.First!;
                }
            } while (node != startNode && remaining > 2);

            if (!madeProgress)
            {
                var currNode = nodeIndices.First;
                bool foundAny = false;

                while (currNode != null && remaining > 2)
                {
                    var prevNode = currNode.Previous ?? nodeIndices.Last!;
                    var nextNode = currNode.Next ?? nodeIndices.First!;

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
                {
                    // Console.WriteLine($"DEBUG EarCut: Stuck at iteration {iterations}, remaining={remaining}");
                    // Console.WriteLine($"DEBUG EarCut: remaining vertices: [{string.Join(", ", nodeIndices)}]");
                    //
                    // // Debug why each vertex fails
                    // var debugNode = nodeIndices.First;
                    // Console.WriteLine("DEBUG EarCut: Vertex positions:");
                    // while (debugNode != null)
                    // {
                    //     int idx = debugNode.Value;
                    //     Console.WriteLine($"  node {idx} (vert {cleanIndices[idx]}): {cleanVerts[idx]}");
                    //     debugNode = debugNode.Next;
                    // }
                    //
                    // debugNode = nodeIndices.First;
                    // while (debugNode != null)
                    // {
                    //     var prevNode = debugNode.Previous ?? nodeIndices.Last;
                    //     var nextNode = debugNode.Next ?? nodeIndices.First;
                    //
                    //     int pv = prevNode.Value, cv = debugNode.Value, nv = nextNode.Value;
                    //     Vector2 a = cleanVerts[pv], b = cleanVerts[cv], c = cleanVerts[nv];
                    //     float cross = Cross(b - a, c - a);
                    //
                    //     bool hasPointInside = false;
                    //     int pointInsideIdx = -1;
                    //     foreach (int idx in nodeIndices)
                    //     {
                    //         if (idx == pv || idx == cv || idx == nv) continue;
                    //         Vector2 p = cleanVerts[idx];
                    //         if (Vector2.DistanceSquared(p, a) < 1e-10f ||
                    //             Vector2.DistanceSquared(p, b) < 1e-10f ||
                    //             Vector2.DistanceSquared(p, c) < 1e-10f) continue;
                    //         if (PointInTriangleStrict(p, a, b, c))
                    //         {
                    //             hasPointInside = true;
                    //             pointInsideIdx = idx;
                    //             break;
                    //         }
                    //     }
                    //
                    //     Console.WriteLine($"DEBUG EarCut: node {cv} (orig {cleanIndices[cv]}): prev={pv}, next={nv}, cross={cross:F6}, pointInside={hasPointInside} (idx={pointInsideIdx})");
                    //     debugNode = debugNode.Next;
                    // }

                    break;
                }
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

        // For merged polygons with holes, allow any winding as long as no points inside
        // Skip near-degenerate triangles
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
            // new Vector3(42.5f,23.800001f,207.40001f),
            // new Vector3(42.5f,-8.5f,207.40001f),
            // new Vector3(27.2f,-20.400002f,207.40001f),
            // new Vector3(13.6f,-23.800001f,207.40001f),
            // new Vector3(-13.6f,-23.800001f,207.40001f),
            // new Vector3(-27.2f,-20.400002f,207.40001f),
            // new Vector3(-42.5f,-8.5f,207.40001f),
            // new Vector3(-42.5f,23.800001f,207.40001f),
            // new Vector3(-35.7f,23.800001f,207.40001f),
            // new Vector3(35.7f,23.800001f,207.40001f),
            // new Vector3(35.7f,11.900001f,207.40001f),
            // new Vector3(-35.7f,11.900001f,207.40001f),
            // new Vector3(-35.7f,-5.1000004f,207.40001f),
            // new Vector3(-23.800001f,-15.3f,207.40001f),
            // new Vector3(-13.6f,-17f,207.40001f),
            // new Vector3(13.6f,-17f,207.40001f),
            // new Vector3(23.800001f,-15.3f,207.40001f),
            // new Vector3(35.7f,-5.1000004f,207.40001f),
            // new Vector3(35.7f,23.800001f,207.40001f),
            new Vector3(-72,0,103),
            new Vector3(-72,-3,103),
            new Vector3(-72,-3,111),
            new Vector3(-72,31,111),
            new Vector3(-72,42,111),
            new Vector3(-72,42,98),
            new Vector3(-72,42,90),
            new Vector3(-72,42,29),
            new Vector3(-72,42,22),
            new Vector3(-72,42,8),
            new Vector3(-72,31,8),
            new Vector3(-72,-3,8),
            new Vector3(-72,-3,26),
            new Vector3(-72,20,26),
            new Vector3(-72,20,97),
            new Vector3(-72,0,103),
            new Vector3(-72,-3,103),
            new Vector3(-72,-3,26),
            new Vector3(-72,20,26),
            new Vector3(-72,20,97),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Console.WriteLine($"Plane Normal: {result.PlaneNormal}");
        Console.WriteLine($"Regions Detected: {result.RegionCount}");
        Console.WriteLine($"Triangles: {result.Triangles.Length / 3f}");

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            Console.WriteLine("[");
            Console.WriteLine($"new Vector3({vertices[result.Triangles[i]].X}f, {vertices[result.Triangles[i]].Y}f, {vertices[result.Triangles[i]].Z}f)");
            Console.WriteLine($"new Vector3({vertices[result.Triangles[i]].X}f, {vertices[result.Triangles[i]].Y}f, {vertices[result.Triangles[i]].Z}f)");
            Console.WriteLine($"new Vector3({vertices[result.Triangles[i]].X}f, {vertices[result.Triangles[i]].Y}f, {vertices[result.Triangles[i]].Z}f)");
            Console.WriteLine("],");
            Console.WriteLine();
        }

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            Console.WriteLine("<p>");
            Console.WriteLine($"c({Random.Shared.Next(0, 256)},{Random.Shared.Next(0, 256)},{Random.Shared.Next(0, 256)})");
            Console.WriteLine("gr(40)");
            Console.WriteLine("fs(1)");
            Console.WriteLine($"p({vertices[result.Triangles[i]].X},{vertices[result.Triangles[i]].Y},{vertices[result.Triangles[i]].Z})");
            Console.WriteLine($"p({vertices[result.Triangles[i + 1]].X},{vertices[result.Triangles[i + 1]].Y},{vertices[result.Triangles[i + 1]].Z})");
            Console.WriteLine($"p({vertices[result.Triangles[i + 2]].X},{vertices[result.Triangles[i + 2]].Y},{vertices[result.Triangles[i + 2]].Z})");
            Console.WriteLine("</p>");
            Console.WriteLine();
        }
    }
}