using LibTessDotNet;
using Poly2Tri;

namespace HoleyDiver;

public class PolygonTriangulator
{
    public struct TriangulationResult
    {
        public uint[] Triangles;
        public Vector3 PlaneNormal;
        public Vector3 Centroid;
        public int RegionCount;
    }

    public enum TriangulationAlgorithm : byte
    {
        Phyrexian,
        Libtess
    }

    public static TriangulationResult Triangulate(IReadOnlyList<Vector3> vertices, TriangulationAlgorithm algorithm = TriangulationAlgorithm.Libtess)
    {
        if (vertices == null || vertices.Count < 3)
            throw new ArgumentException("Must have at least 3 vertices");

        Vector3 centroid = ComputeCentroid(vertices);
        Vector3 normal = ComputeBestFitPlaneNormal(vertices, centroid);

        if (TryTriangulateSimple(vertices, centroid, normal, out var result))
        {
            return result;
        }

        var projected2D = ProjectTo2D(vertices, centroid, normal);

        if (algorithm == TriangulationAlgorithm.Libtess)
        {
            // Create an instance of the tessellator. Can be reused.
            var tess = new Tess();

            // Construct the contour from inputData.
            // A polygon can be composed of multiple contours which are all tessellated at the same time.
            int numPoints = projected2D.Count;
            var contour = new ContourVertex[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                if (float.IsNaN(projected2D[i].X) || float.IsNaN(projected2D[i].Y))
                {
                    Console.WriteLine($"Projected vertex {i} has NaN coordinates: {vertices[i]}");
                    continue;
                }

                // NOTE : Z is here for convenience if you want to keep a 3D vertex position throughout the tessellation process but only X and Y are important.
                contour[i].Position = new Vec3(projected2D[i].X, projected2D[i].Y, i);
            }

            // Add the contour with a specific orientation, use "Original" if you want to keep the input orientation.
            tess.AddContour(contour);

            // Tessellate!
            // The winding rule determines how the different contours are combined together.
            // See http://www.glprogramming.com/red/chapter11.html (section "Winding Numbers and Winding Rules") for more information.
            // If you want triangles as output, you need to use "Polygons" type as output and 3 vertices per polygon.
            tess.Tessellate();

            // Same call but the last callback is optional. Data will be null because no interpolated data would have been generated.
            //tess.Tessellate(LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, 3); // Some vertices will have null Data in this case.

            var simpleTriangles = new uint[tess.ElementCount * 3];
            int numTriangles = tess.ElementCount;
            for (int i = 0; i < numTriangles; i++)
            {
                var v0 = tess.Vertices[tess.Elements[i * 3]].Position;
                var v1 = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
                var v2 = tess.Vertices[tess.Elements[i * 3 + 2]].Position;
                simpleTriangles[i * 3] = (uint)v0.Z;
                simpleTriangles[i * 3 + 1] = (uint)v1.Z;
                simpleTriangles[i * 3 + 2] = (uint)v2.Z;
            }

            return new TriangulationResult
            {
                Triangles = simpleTriangles,
                PlaneNormal = normal,
                Centroid = centroid,
                RegionCount = 1
            };
        }
        else
        {
            const float epsilon = 1e-5f;
            var uniqueVertices = new List<Vector2>();
            var indexMap = new List<int>(projected2D.Count);

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

            var initialPoly = new List<int>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                initialPoly.Add(indexMap[i]);
            }

            var uniqueIndices = new HashSet<int>(initialPoly);

            List<List<int>> polyLines;
            if (uniqueIndices.Count == initialPoly.Count)
            {
                polyLines = [initialPoly];
            }
            else
            {
                polyLines = ExtractRegions(initialPoly, uniqueVertices);
            }

            // WORKAROUND: ExtractRegions sometimes produces incomplete outer boundaries
            // The outer boundary should include all vertices not in holes, in path order
            if (polyLines.Count > 1)
            {
                // Collect all hole vertices
                var holeVertices = new HashSet<int>();
                for (int r = 1; r < polyLines.Count; r++)
                {
                    foreach (var idx in polyLines[r])
                    {
                        if (idx != -1)  // Skip the hole marker
                            holeVertices.Add(idx);
                    }
                }

                // Reconstruct outer polygon from original path, excluding hole vertices
                var reconstructedOuter = new List<int>();
                var seenOuter = new HashSet<int>();

                foreach (var idx in initialPoly)
                {
                    if (!holeVertices.Contains(idx) && !seenOuter.Contains(idx))
                    {
                        reconstructedOuter.Add(idx);
                        seenOuter.Add(idx);
                    }
                }

                if (reconstructedOuter.Count >= 3)
                {
                    polyLines[0] = reconstructedOuter;
                }
            }

            // Separate outer polygon from holes (holes are marked with -1 as first element)
            List<int>? outerPoly = null;
            var holePolys = new List<List<int>>();

            foreach (var region in polyLines)
            {
                if (region.Count > 0 && region[0] == -1)
                {
                    // This is a hole - remove the marker
                    holePolys.Add(region.Skip(1).ToList());
                }
                else if (outerPoly == null)
                {
                    outerPoly = region;
                }
                else
                {
                    // Additional outer regions (shouldn't happen with current logic)
                    // Just add them as separate polygons to triangulate
                }
            }

            // Remove any hole vertices that are shared with the outer boundary (bridge points)
            if (outerPoly != null && holePolys.Count > 0)
            {
                var outerSet = new HashSet<int>(outerPoly);

                for (int h = 0; h < holePolys.Count; h++)
                {
                    holePolys[h] = holePolys[h].Where(idx => !outerSet.Contains(idx)).ToList();
                }
                // Remove any holes that became too small
                holePolys.RemoveAll(h => h.Count < 3);
            }

            if (outerPoly == null || outerPoly.Count < 3)
            {
                return new TriangulationResult
                {
                    Triangles = Array.Empty<uint>(),
                    PlaneNormal = normal,
                    Centroid = centroid,
                    RegionCount = 0
                };
            }

            // For polygons with holes, we need to include hole vertices in the triangulation
            // Build a complete vertex list and use constrained triangulation
            var allTriangles = new List<uint>();

            // If there are holes, we need to use a different approach
            // Since simple centroid filtering doesn't work well, let's try adding hole vertices
            // to create a proper constrained triangulation

            if (holePolys.Count > 0)
            {
                // Use Poly2Tri constrained Delaunay triangulation for polygons with holes
                try
                {
                    // Convert outer polygon to PolygonPoints
                    var outerPoints = new List<PolygonPoint>(outerPoly.Count);
                    foreach (var idx in outerPoly)
                    {
                        var pt = uniqueVertices[idx];
                        outerPoints.Add(new PolygonPoint(pt.X, pt.Y));
                    }

                    var poly = new Polygon(outerPoints);

                    // Add each hole
                    foreach (var hole in holePolys)
                    {
                        var holePoints = new List<PolygonPoint>(hole.Count);
                        foreach (var idx in hole)
                        {
                            var pt = uniqueVertices[idx];
                            holePoints.Add(new PolygonPoint(pt.X, pt.Y));
                        }

                        poly.AddHole(new Polygon(holePoints));
                    }

                    // Triangulate using constrained Delaunay
                    DTSweepContext tcx = new DTSweepContext();
                    tcx.PrepareTriangulation(poly);
                    DTSweep.Triangulate(tcx);

                    // Extract triangles and map back to original vertex indices
                    foreach (var tri in poly.Triangles)
                    {
                        var triIndices = new List<uint>(3);
                        for (int i = 0; i < 3; i++)
                        {
                            var p = tri.Points[i];
                            // Find the unique vertex index that matches this point
                            int uniqueIdx = -1;
                            for (int j = 0; j < uniqueVertices.Count; j++)
                            {
                                if (Math.Abs(uniqueVertices[j].X - p.X) < 1e-5f &&
                                    Math.Abs(uniqueVertices[j].Y - p.Y) < 1e-5f)
                                {
                                    uniqueIdx = j;
                                    break;
                                }
                            }

                            if (uniqueIdx >= 0)
                            {
                                // Map to original vertex index
                                int origIdx = -1;
                                for (int k = 0; k < indexMap.Count; k++)
                                {
                                    if (indexMap[k] == uniqueIdx)
                                    {
                                        origIdx = k;
                                        break;
                                    }
                                }
                                if (origIdx >= 0)
                                {
                                    triIndices.Add((uint)origIdx);
                                }
                            }
                        }

                        // Only add complete triangles (must have exactly 3 vertices)
                        if (triIndices.Count == 3)
                        {
                            allTriangles.AddRange(triIndices);
                        }
                        else
                        {
                            Console.WriteLine("Delaunay created degenerate triangle, this should never happen!");
                        }
                    }

                    return new TriangulationResult
                    {
                        Triangles = allTriangles.ToArray(),
                        PlaneNormal = normal,
                        Centroid = centroid,
                        RegionCount = 1 + holePolys.Count
                    };
                }
                catch (Exception ex)
                {
                    // If Poly2Tri fails, fall back to simple ear-cut with centroid filtering
                    Console.WriteLine($"Poly2Tri triangulation failed: {ex.Message}. Falling back to ear-cut.");
                }

                // Fallback: use ear-cut with centroid filtering
                var outerVerts = new List<Vector2>(outerPoly.Count);
                foreach (var idx in outerPoly)
                    outerVerts.Add(uniqueVertices[idx]);

                var outerTris = EarCutTriangulateSimple(outerVerts);

                // For the outer triangles, filter out any that have centroid inside a hole
                for (int t = 0; t < outerTris.Count; t += 3)
                {
                    int i0 = outerPoly[outerTris[t]];
                    int i1 = outerPoly[outerTris[t + 1]];
                    int i2 = outerPoly[outerTris[t + 2]];

                    Vector2 triCentroid = (uniqueVertices[i0] + uniqueVertices[i1] + uniqueVertices[i2]) / 3f;

                    bool insideHole = false;
                    foreach (var hole in holePolys)
                    {
                        var holeVerts = new List<Vector2>();
                        foreach (var idx in hole)
                            holeVerts.Add(uniqueVertices[idx]);

                        if (PointInPolygon(triCentroid, holeVerts))
                        {
                            insideHole = true;
                            break;
                        }
                    }

                    if (!insideHole)
                    {
                        // Map back to original indices
                        for (int vi = 0; vi < 3; vi++)
                        {
                            int uniqueIdx = outerPoly[outerTris[t + vi]];
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
                                allTriangles.Add((uint)origIdx);
                        }
                    }
                }

                return new TriangulationResult
                {
                    Triangles = allTriangles.ToArray(),
                    PlaneNormal = normal,
                    Centroid = centroid,
                    RegionCount = 1 + holePolys.Count
                };
            }

            // No holes - simple triangulation
            var outerVertsSimple = new List<Vector2>();
            foreach (var idx in outerPoly)
                outerVertsSimple.Add(uniqueVertices[idx]);

            var trisSimple = EarCutTriangulateSimple(outerVertsSimple);

            for (int t = 0; t < trisSimple.Count; t += 3)
            {
                for (int vi = 0; vi < 3; vi++)
                {
                    int uniqueIdx = outerPoly[trisSimple[t + vi]];
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
                        allTriangles.Add((uint)origIdx);
                }
            }

            return new TriangulationResult
            {
                Triangles = allTriangles.ToArray(),
                PlaneNormal = normal,
                Centroid = centroid,
                RegionCount = 1
            };
        }
    }

    private static bool TryTriangulateSimple(IReadOnlyList<Vector3> vertices, Vector3 centroid, Vector3 normal, out TriangulationResult result)
    {
        if (IsConvex(vertices) && DoesNotContainDuplicatePoints(vertices))
        {
            // simple polygon: fan triangulation
            
            var tris = new List<uint>(vertices.Count - 2);
            for (uint i = 1; i < vertices.Count - 1; i++)
                tris.AddRange((uint)0, i, i + 1);
            result = new TriangulationResult
            {
                Triangles = tris.ToArray(),
                PlaneNormal = normal,
                Centroid = centroid,
                RegionCount = 1
            };
            return true;
        }

        result = default;
        return false;
    }

    private static bool DoesNotContainDuplicatePoints(IReadOnlyList<Vector3> vertices)
    {
        const float epsilon = 1e-5f;
        for (int i = 0; i < vertices.Count; i++)
        {
            for (int j = i + 1; j < vertices.Count; j++)
            {
                if (Vector3.Distance(vertices[i], vertices[j]) < epsilon)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsPlanar(IReadOnlyList<Vector3> pts, float eps = 1e-6f)
    {
        if (pts.Count < 3) return true;
        Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]);
        n = Vector3.Normalize(n);

        for (int i = 3; i < pts.Count; i++)
            if (MathF.Abs(Vector3.Dot(pts[i] - pts[0], n)) > eps) return false;
        return true;
    }

    private static bool IsConvex(IReadOnlyList<Vector3> pts)
    {
        if (!IsPlanar(pts)) return false;
        Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]);
        n = Vector3.Normalize(n);

        // orthonormal basis for projection
        Vector3 u = (pts[1] - pts[0]);
        u = Vector3.Normalize(u);
        Vector3 v = Vector3.Cross(n, u);
        v = Vector3.Normalize(v);

        var proj = pts.Select(p => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v))).ToList();

        int sign = 0;
        int count = proj.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = proj[i], b = proj[(i + 1) % count], c = proj[(i + 2) % count];
            float z = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (MathF.Abs(z) < 1e-10f) continue;
            int s = z > 0 ? 1 : -1;
            if (sign == 0) sign = s;
            else if (s != sign) return false;
        }
        return true;
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
                if (float.IsNaN(projected2D[i].X) || float.IsNaN(projected2D[j].X) ||
                    float.IsNaN(projected2D[i].Y) || float.IsNaN(projected2D[j].Y))
                {
                    return false;
                }
                
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
        List<List<int>> polyLines = [[..polyIndices]];

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

                // If the extracted region is the entire polygon, we've found
                // a wrap-around duplicate (polygon closure) rather than a true hole.
                // Stop iterating to avoid an infinite loop.
                if (newRegion.Count >= n)
                {
                    break;
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

        // Recursively split any region that still contains duplicate vertices
        // (i.e., self-intersecting polygons that weren't fully decomposed)
        bool anySplit = true;
        int maxRecursivePasses = 10;
        int recursivePass = 0;
        while (anySplit && recursivePass < maxRecursivePasses)
        {
            anySplit = false;
            recursivePass++;
            var newRegions = new List<List<int>>();
            foreach (var region in polyLines)
            {
                // Skip hole markers
                if (region.Count > 0 && region[0] == -1)
                {
                    newRegions.Add(region);
                    continue;
                }

                var uniqueSet = new HashSet<int>(region);
                if (uniqueSet.Count < region.Count && region.Count >= 6)
                {
                    // This region has duplicate vertices — try to split it further
                    var subRegions = ExtractRegions(region, vertices);
                    if (subRegions.Count > 1)
                    {
                        // Successfully split — add all sub-regions.
                        // ExtractRegions returns [outer, -1+hole1, -1+hole2, ...]
                        // We need to find the outer (largest area) and add holes separately.
                        // But since we're going to re-select the outer by area later,
                        // just add all sub-region vertices (stripping -1 markers from holes).
                        foreach (var sr in subRegions)
                        {
                            // Strip -1 hole marker if present
                            var cleaned = sr.Count > 0 && sr[0] == -1 ? sr.Skip(1).ToList() : sr;
                            var deduped = RemoveConsecutiveDuplicates(cleaned);
                            if (deduped.Count >= 3)
                                newRegions.Add(deduped);
                        }
                        anySplit = true;
                    }
                    else
                    {
                        newRegions.Add(region);
                    }
                }
                else
                {
                    newRegions.Add(region);
                }
            }
            polyLines = newRegions;
            polyLines.RemoveAll(r => r.Count < 3);
        }

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
            // Mark holes with -1 prefix so they're passed to Poly2Tri as constraints
            var result = new List<List<int>> { polyLines[0] };
            foreach (var hole in holes)
            {
                var markedHole = new List<int> { -1 };
                markedHole.AddRange(hole);
                result.Add(markedHole);
            }
            return result;
        }

        return polyLines;
    }

    /// <summary>
    /// Specialized region extraction for zigzag bridge-format polygons
    /// (e.g., car grills). Identifies vertical "bars" and extracts the
    /// rectangular holes between consecutive bar pairs.
    /// Returns null if the polygon doesn't match this pattern.
    /// </summary>
    private static List<List<int>>? ExtractRegionsByBarPairs(List<int> polyIndices, List<Vector2> vertices)
    {
        int n = polyIndices.Count;

        // Map each unique vertex to its positions in the polygon
        var posMap = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int idx = polyIndices[i];
            if (!posMap.ContainsKey(idx))
                posMap[idx] = new List<int>();
            posMap[idx].Add(i);
        }

        // Only applies to polygons with duplicate vertices
        if (posMap.Count == n) return null;

        // Build bars: find vertices at similar X that form vertical segments.
        // A "bar" is defined by a top vertex (high Y) and a bottom vertex (low Y)
        // that are connected by an edge in the polygon, or are at the same X.
        // For grill patterns, bars connect vertices at the same X coordinate
        // (one at Y≈63-75, one at Y≈-7-6).
        var bars = new List<(int topIdx, int botIdx, float x, float topY, float botY)>();

        foreach (var kvp in posMap)
        {
            int idx = kvp.Key;
            var v = vertices[idx];

            // Look for another vertex at the same X with significantly different Y
            foreach (var otherKvp in posMap)
            {
                int otherIdx = otherKvp.Key;
                if (otherIdx <= idx) continue;
                var otherV = vertices[otherIdx];

                if (Math.Abs(v.X - otherV.X) < 2f && Math.Abs(v.Y - otherV.Y) > 20f)
                {
                    // Found a potential bar pair
                    float topY, botY;
                    int topIdx, botIdx;
                    if (v.Y > otherV.Y)
                    {
                        topIdx = idx; botIdx = otherIdx;
                        topY = v.Y; botY = otherV.Y;
                    }
                    else
                    {
                        topIdx = otherIdx; botIdx = idx;
                        topY = otherV.Y; botY = v.Y;
                    }
                    bars.Add((topIdx, botIdx, (v.X + otherV.X) / 2f, topY, botY));
                }
            }
        }

        if (bars.Count < 2) return null;

        // Sort bars by X coordinate (left to right)
        bars.Sort((a, b) => a.x.CompareTo(b.x));

        // Build holes: each hole is the rectangular space between consecutive bars.
        // Hole vertices: top-left, top-right, bottom-right, bottom-left.
        var holes = new List<List<int>>();
        for (int i = 0; i < bars.Count - 1; i++)
        {
            var leftBar = bars[i];
            var rightBar = bars[i + 1];

            // Check if these bars are actually connected by edges in the polygon
            // (i.e., there's a top-edge segment and a bottom-edge segment)
            // For simplicity, just create the quadrilateral hole.
            int tl = leftBar.topIdx;   // top-left
            int tr = rightBar.topIdx;  // top-right
            int br = rightBar.botIdx;  // bottom-right
            int bl = leftBar.botIdx;   // bottom-left

            // Validate: all 4 vertices must exist and be distinct
            var holeSet = new HashSet<int> { tl, tr, br, bl };
            if (holeSet.Count == 4)
            {
                holes.Add(new List<int> { tl, tr, br, bl });
            }
        }

        if (holes.Count == 0) return null;

        // Build outer polygon: all unique vertices NOT exclusively used by holes.
        // Bridge vertices (used by 2+ holes) stay in the outer.
        var holeInteriorVerts = new HashSet<int>();
        var holeVerts = new HashSet<int>();
        foreach (var hole in holes)
        {
            foreach (var idx in hole)
                holeVerts.Add(idx);
        }

        // Count how many holes each vertex appears in
        var vertHoleCount = new Dictionary<int, int>();
        foreach (var hole in holes)
        {
            var seen = new HashSet<int>();
            foreach (var idx in hole)
            {
                if (seen.Add(idx))
                {
                    vertHoleCount.TryGetValue(idx, out int c);
                    vertHoleCount[idx] = c + 1;
                }
            }
        }

        // Vertices that appear in exactly 1 hole are hole-interior
        foreach (var kvp in vertHoleCount)
        {
            if (kvp.Value == 1)
                holeInteriorVerts.Add(kvp.Key);
        }

        // Outer = all unique vertices in path order, excluding hole-interior vertices
        var outerPoly = new List<int>();
        var seenOuter = new HashSet<int>();
        foreach (var idx in polyIndices)
        {
            if (!holeInteriorVerts.Contains(idx) && !seenOuter.Contains(idx))
            {
                outerPoly.Add(idx);
                seenOuter.Add(idx);
            }
        }

        if (outerPoly.Count < 3) return null;

        // Mark holes with -1 prefix
        var result = new List<List<int>> { outerPoly };
        foreach (var hole in holes)
        {
            var markedHole = new List<int> { -1 };
            markedHole.AddRange(hole);
            result.Add(markedHole);
        }
        return result;
    }

    /// <summary>
    /// Splits a self-touching polygon into simple regions by reconnecting edges
    /// at duplicate vertices (bridge points). At each bridge point, the polygon
    /// touches itself; by swapping the outgoing edge connections we separate the
    /// polygon into non-self-intersecting loops.
    /// The largest loop by area is the outer boundary; the rest are holes.
    /// </summary>
    private static List<List<int>> ExtractRegionsByDuplicateVertices(List<int> polyIndices, List<Vector2> vertices)
    {
        int n = polyIndices.Count;

        // Build occurrence map: vertex -> list of positions
        var occurrenceMap = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int idx = polyIndices[i];
            if (!occurrenceMap.ContainsKey(idx))
                occurrenceMap[idx] = new List<int>();
            occurrenceMap[idx].Add(i);
        }

        // Build adjacency: for each position, the "next" position (forward edge).
        // We'll modify this to split at bridge points.
        // next[i] = position that follows i in the (possibly modified) polygon
        var next = new int[n];
        for (int i = 0; i < n; i++)
            next[i] = (i + 1) % n;

        // At each duplicate vertex, swap outgoing edges to separate the loops.
        // For a vertex appearing at positions a and b:
        //   Original: ...prev(a)→a→next(a)...   ...prev(b)→b→next(b)...
        //   Swapped:  ...prev(a)→a→next(b)...   ...prev(b)→b→next(a)...
        // (prev = the position that points TO a/b via the 'next' array)
        foreach (var kvp in occurrenceMap)
        {
            var positions = kvp.Value;
            // Process pairs of consecutive occurrences
            for (int p = 0; p < positions.Count - 1; p++)
            {
                int a = positions[p];
                int b = positions[p + 1];

                // Find prev(a): the position i where next[i] == a
                int prevA = -1;
                for (int i = 0; i < n; i++)
                {
                    if (next[i] == a) { prevA = i; break; }
                }

                // Find prev(b):
                int prevB = -1;
                for (int i = 0; i < n; i++)
                {
                    if (next[i] == b) { prevB = i; break; }
                }

                if (prevA < 0 || prevB < 0) continue;

                // Swap: prevA→a should now go to next[b], prevB→b should go to next[a]
                // But also, the edges leaving a and b need to be rewired.
                // After swap:
                //   next[prevA] = b  (so prevA→b instead of prevA→a)
                //   next[a] stays as is for now, or we swap the outgoing edges
                //
                // Actually, the standard polygon-splitting operation at a
                // self-touching vertex with two entries and two exits:
                //   Entry edges: prevA→a, prevB→b
                //   Exit edges:  a→next[a], b→next[b]
                // Swap the exit connections: prevA→a→next[b], prevB→b→next[a]
                int nextA = next[a];
                int nextB = next[b];
                next[a] = nextB;
                next[b] = nextA;
            }
        }

        // Now extract all cycles from the 'next' array
        var visited = new bool[n];
        var cycles = new List<List<int>>();

        for (int start = 0; start < n; start++)
        {
            if (visited[start]) continue;

            var cycle = new List<int>();
            int cur = start;
            do
            {
                visited[cur] = true;
                cycle.Add(polyIndices[cur]);
                cur = next[cur];
            } while (cur != start && !visited[cur]);

            if (cycle.Count >= 3)
                cycles.Add(cycle);
        }

        if (cycles.Count <= 1)
            return new List<List<int>> { new(polyIndices) };

        // Remove consecutive duplicates from each cycle
        for (int i = 0; i < cycles.Count; i++)
            cycles[i] = RemoveConsecutiveDuplicates(cycles[i]);

        cycles.RemoveAll(c => c.Count < 3);

        if (cycles.Count <= 1)
            return new List<List<int>> { new(polyIndices) };

        // Find the largest-area cycle — this is the outer boundary
        int outerIdx = 0;
        float maxArea = 0;
        for (int i = 0; i < cycles.Count; i++)
        {
            var verts = new List<Vector2>();
            foreach (var idx in cycles[i])
                verts.Add(vertices[idx]);
            float area = Math.Abs(ComputeSignedArea(verts));
            if (area > maxArea)
            {
                maxArea = area;
                outerIdx = i;
            }
        }

        var outerPoly = cycles[outerIdx];

        // Remaining cycles that are geometrically inside the outer are holes
        var outerVerts = new List<Vector2>();
        foreach (var idx in outerPoly)
            outerVerts.Add(vertices[idx]);

        var holes = new List<List<int>>();
        for (int i = 0; i < cycles.Count; i++)
        {
            if (i == outerIdx) continue;
            if (cycles[i].Count < 3) continue;

            // Check if all vertices of this cycle are inside the outer polygon
            bool allInside = true;
            foreach (var idx in cycles[i])
            {
                if (!PointInPolygon(vertices[idx], outerVerts))
                {
                    allInside = false;
                    break;
                }
            }

            if (allInside)
                holes.Add(cycles[i]);
        }

        if (holes.Count == 0)
            return new List<List<int>> { outerPoly };

        // Mark holes with -1 prefix
        var result = new List<List<int>> { outerPoly };
        foreach (var hole in holes)
        {
            var markedHole = new List<int> { -1 };
            markedHole.AddRange(hole);
            result.Add(markedHole);
        }
        return result;
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

    public static Vector3 ComputeCentroid<T>(T vertices) where T : IReadOnlyList<Vector3>
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

    public static Vector3 ComputeBestFitPlaneNormal(IReadOnlyList<Vector3> vertices, Vector3 centroid)
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
            new(-99, 6, 298),
            new(-99, 63, 313),
            new(-88, 63, 318),
            new(-76, 63, 324),
            new(-65, 63, 329),
            new(-53, 63, 336),
            new(-42, 63, 342),
            new(-28, 63, 349),
            new(-17, 63, 355),
            new(-17, 6, 331),
            new(-29, 6, 326),
            new(-28, 63, 349),
            new(-42, 63, 342),
            new(-41, 6, 321),
            new(-53, 6, 316),
            new(-53, 63, 336),
            new(-65, 63, 329),
            new(-65, 6, 311),
            new(-76, 6, 306),
            new(-76, 63, 324),
            new(-88, 63, 318),
            new(-88, 6, 302),
            new(-99, 6, 298),
            new(-112, -7, 288),
            new(0, -7, 326),
            new(0, 75, 374),
            new(-112, 75, 311),
            new(-112, -7, 288),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Console.WriteLine($"Regions Detected: {result.RegionCount}");
        Console.WriteLine($"Triangles: {result.Triangles.Length / 3}");
        Console.WriteLine($"Plane normal: {result.PlaneNormal}");

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            var v0 = vertices[(int)result.Triangles[i]];
            var v1 = vertices[(int)result.Triangles[i + 1]];
            var v2 = vertices[(int)result.Triangles[i + 2]];
            Console.WriteLine($"  tri {i / 3}: [{result.Triangles[i]}] {v0}, [{result.Triangles[i + 1]}] {v1}, [{result.Triangles[i + 2]}] {v2}");
        }
    }
}