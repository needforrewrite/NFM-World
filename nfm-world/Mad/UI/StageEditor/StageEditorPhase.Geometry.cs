using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    private bool IsMouseInViewport(int x, int y)
    {
        return x >= _viewportMin.X && x <= _viewportMax.X &&
               y >= _viewportMin.Y && y <= _viewportMax.Y;
    }
    
    private void RebuildAllWalls()
    {
        if (ActiveTab?.Stage == null) return;
        
        // Get the wall mesh from GameSparker
        var wallPart = BackendGameSparker.GetStagePart("nfmm/thewall");
        if (wallPart.Rad == null)
        {
            SentrySdk.CaptureMessage("Wall mesh not found!");
            Logging.Error("Wall mesh not found!");
            return;
        }
        
        // Clear the wall meshes list
        ActiveTab.WallMeshes.Clear();
        
        Logging.Info($"Rebuilding walls: {ActiveTab.StageWalls.Count} wall groups");
        
        // Generate wall meshes based on StageWalls definitions
        foreach (var wall in ActiveTab.StageWalls)
        {
            var n = wall.Count;
            var o = wall.Position;
            var p = wall.Offset;
            
            Logging.Debug($"Creating wall: {wall.Direction}, count={n}, pos={o}, offset={p}");
            
            for (int q = 0; q < n; q++)
            {
                f64Vector3 position;
                f64Euler rotation;
                
                switch (wall.Direction)
                {
                    case WallDirection.Right:
                        position = new f64Vector3(o, World.Ground, q * 4800 + p);
                        rotation = f64Euler.Identity;
                        break;
                    case WallDirection.Left:
                        position = new f64Vector3(o, World.Ground, q * 4800 + p);
                        rotation = new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                        break;
                    case WallDirection.Top:
                        position = new f64Vector3(q * 4800 + p, World.Ground, o);
                        rotation = new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                        break;
                    case WallDirection.Bottom:
                        position = new f64Vector3(q * 4800 + p, World.Ground, o);
                        rotation = new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
                        break;
                    default:
                        position = f64Vector3.Zero;
                        rotation = f64Euler.Identity;
                        break;
                }
                
                ActiveTab.WallMeshes.Add(new MeshedGameObject(new Mesh(wallPart.Rad), position, rotation));
            }
        }
        
        Logging.Debug($"Total wall meshes created: {ActiveTab.WallMeshes.Count}");
        
        // Rebuild scene so new wall meshes are included in instanced rendering
        RecreateScene();
    }

    private bool HasPlaceableStageGeometry()
    {
        return ActiveTab != null && ActiveTab.ScenePieces.Any(p => !p.PiecePlacement.IsWall);
    }

    private void AutoGenerateStageBordersFromGeometry()
    {
        if (ActiveTab == null) return;

        // Collect all non-wall pieces (in the NFM editor, all piece types are unified in ScenePieces)
        var pieces = ActiveTab.ScenePieces.Where(p => !p.PiecePlacement.IsWall).ToList();
        if (pieces.Count == 0)
            return;

        // ── Compute min/max bounds expanded by each piece's MaxRadius ──
        // Matches the Java makeWalls logic: iterate nodes, parts, fixPoints, repairPoints, piles
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        foreach (var piece in pieces)
        {
            float px = (float)piece.Position.X;
            float pz = (float)piece.Position.Z;
            // maxR from Models.partMap in Java  ≡  Rad.MaxRadius in C#
            float radius = piece.Rad.MaxRadius > 0 ? piece.Rad.MaxRadius : 0f;
            minX = MathF.Min(minX, px - radius);
            maxX = MathF.Max(maxX, px + radius);
            minZ = MathF.Min(minZ, pz - radius);
            maxZ = MathF.Max(maxZ, pz + radius);
        }

        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(minZ) || !float.IsFinite(maxZ))
            return;

        const int wallSpacing = 4800;
        const int halfSpacing = wallSpacing / 2; // 2400 — matches the Java constant

        // ── X axis (top/bottom walls) — per Java makeWalls: count = ceil(span/4800) or span/4800+1, center, offset = min+2400 ──
        int minXa = (int)MathF.Floor(minX);
        int maxXa = (int)MathF.Ceiling(maxX);
        int spanX = maxXa - minXa;
        int countX = (int)(spanX / (float)wallSpacing) + 1;
        int stageCenterX = (countX * wallSpacing - spanX) / 2;
        minXa -= stageCenterX;
        maxXa += stageCenterX;
        int offsetX = minXa + halfSpacing;

        // ── Z axis (left/right walls) ──
        int minZa = (int)MathF.Floor(minZ);
        int maxZa = (int)MathF.Ceiling(maxZ);
        int spanZ = maxZa - minZa;
        int countZ = (int)(spanZ / (float)wallSpacing) + 1;
        int stageCenterZ = (countZ * wallSpacing - spanZ) / 2;
        minZa -= stageCenterZ;
        maxZa += stageCenterZ;
        int offsetZ = minZa + halfSpacing;

        // ── Assign wall IDs ──
        int nextWallId = 0;
        foreach (var piece in ActiveTab.ScenePieces)
            nextWallId = Math.Max(nextWallId, piece.Id + 1);
        foreach (var wall in ActiveTab.StageWalls)
            nextWallId = Math.Max(nextWallId, wall.Id + 1);

        var rebuiltWalls = KeyedCollection.From<int, EditorStageWall>(w => w.Id);

        // Per Java: maxl={countZ, minXa, offsetZ}  maxr={countZ, maxXa, offsetZ}
        //           maxb={countX, minZa, offsetX}  maxt={countX, maxZa, offsetX}
        var leftWall   = new EditorStageWall(WallDirection.Left,   countZ, minXa, offsetZ, nextWallId++);
        var rightWall  = new EditorStageWall(WallDirection.Right,  countZ, maxXa, offsetZ, nextWallId++);
        var bottomWall = new EditorStageWall(WallDirection.Bottom, countX, minZa, offsetX, nextWallId++);
        var topWall    = new EditorStageWall(WallDirection.Top,    countX, maxZa, offsetX, nextWallId++);

        rebuiltWalls.Add(leftWall);
        rebuiltWalls.Add(rightWall);
        rebuiltWalls.Add(bottomWall);
        rebuiltWalls.Add(topWall);
        ActiveTab.StageWalls = rebuiltWalls;

        // Select the right wall by default (convention from original code)
        ActiveTab.SelectedWallId = rightWall.Id;
        ActiveTab.ActivePieceId = -1;
        ActiveTab.ActivePieceId = -1;
        ActiveTab.SelectedPieceIds.Clear();
        ActiveTab.HasUnsavedChanges = true;

        RebuildAllWalls();
    }

    private void RenderAutoGenerateBordersButton(string idSuffix = "")
    {
        bool canAutoGenerate = HasPlaceableStageGeometry();
        if (!canAutoGenerate)
            ImGui.BeginDisabled();

        if (ImGui.Button($"Auto-Generate Borders##autoborder{idSuffix}", new Vector2(-1, 0)))
        {
            PushUndoSnapshot();
            AutoGenerateStageBordersFromGeometry();
        }

        if (!canAutoGenerate)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Add at least one non-wall stage piece before generating borders.");
        }
    }
    
    private static void RenderSelectionHighlights(NFMWorld.Graphics.ICommandBuffer cb, StageEditorTab tab)
    {
        if (tab.SelectedPieceIds.Count == 0)
            return;

        int neededVertices = 0;
        foreach (var id in tab.SelectedPieceIds)
        {
            if (tab.ScenePieces.GetValueOrDefault(id)?.Obj != null)
                neededVertices += 24;
        }

        if (neededVertices == 0)
            return;

        Debug.RenderHighlights(cb, GameSparker.GraphicsDevice, tab.SelectedPieceIds
            .Select(id => tab.ScenePieces.GetValueOrDefault(id)?.Obj!)
            .Where(obj => obj != null!),
            activeCamera);
    }
    
    /// <summary>
    /// Renders a translucent ghost preview of the pending placement part at _pendingPlacementPos.
    /// Shows semi-transparent filled polygons plus a bright wireframe outline.
    /// </summary>
    private void RenderPlacementPreview(NFMWorld.Graphics.ICommandBuffer cb)
    {
        if (_pendingPlacementPartIndex < 0 || _pendingPlacementPartIndex >= _availableParts.Count) return;
        var part = _availableParts[_pendingPlacementPartIndex];
        
        var pos = new Vector3((float)_pendingPlacementPos.X, (float)_pendingPlacementPos.Y + _pendingPlacementYOff, (float)_pendingPlacementPos.Z);
        float yawRad = -_pendingPlacementYaw * (float)Math.PI / 180f; // negate to match engine convention
        var rotMatrix = Matrix.CreateRotationY(yawRad);
        
        var worldMatrix = rotMatrix * Matrix.CreateTranslation(pos);
        
        var fillColor = new Color(0.3f, 0.8f, 1.0f, 0.35f);
        var wireColor = new Color(0.1f, 0.9f, 1.0f, 1.0f);
        
        Debug.RenderGhost(cb, GameSparker.GraphicsDevice, part, worldMatrix, fillColor, wireColor, activeCamera);
    }
    
    private Vector3 ComputeSelectionCentroid()
    {
        if (ActiveTab == null) return Vector3.Zero;
        var ids = ActiveTab.SelectedPieceIds;
        var pieces = ActiveTab.ScenePieces.Where(p => ids.Contains(p.Id)).ToList();
        if (pieces.Count == 0) return Vector3.Zero;
        return new Vector3(
            (float)pieces.Average(p => (double)p.Position.X),
            (float)pieces.Average(p => (double)p.Position.Y),
            (float)pieces.Average(p => (double)p.Position.Z));
    }

    private void ProcessOnePreviewThumbnail(NFMWorld.Graphics.ICommandBuffer cb)
    {
        if (_previewQueue.Count == 0) return;
        var (name, rad) = _previewQueue.Dequeue();
        if (_partPreviews.ContainsKey(name)) return;

        // Find bounding sphere to set up camera
        float maxR = rad.MaxRadius > 0 ? rad.MaxRadius : 300;

        var rt = GameSparker.GraphicsDevice.CreateRenderTarget(
            new NFMWorld.Graphics.RenderTargetDesc(PreviewSize, PreviewSize, NFMWorld.Graphics.TextureFormat.Rgba8));

        cb.SetRenderTarget(rt);
        cb.SetViewport(new NFMWorld.Graphics.Viewport(0, 0, PreviewSize, PreviewSize));
        cb.SetScissorRect(new NFMWorld.Graphics.ScissorRect(0, 0, PreviewSize, PreviewSize));
        cb.Clear(
            NFMWorld.Graphics.ClearOptions.Color | NFMWorld.Graphics.ClearOptions.Depth,
            new NFMWorld.Graphics.ColorRgba(45 / 255f, 45 / 255f, 48 / 255f, 1f));

        // Set up a simple isometric-ish view camera for the preview
        float camDist = maxR * 3f;
        var eye = new Vector3(camDist * 0.7f, camDist * 0.6f, camDist * 0.7f);
        var target = Vector3.Zero;

        var oldSnap = World.Snap;
        World.Snap = new Color3(100, 100, 100);
        using var mesh = new ImmediateMesh(rad);

        var camera = new PerspectiveCamera()
        {
            Fov = 40f,
            Position = eye,
            LookAt = target
        };
        camera.OnBeforeRender(1f);
        mesh.Render(cb, camera, null);

        cb.SetRenderTarget(null);
        cb.SetViewport(new NFMWorld.Graphics.Viewport(0, 0, GameSparker.Game.Window.Width, GameSparker.Game.Window.Height));
        GameSparker.Game.SetFullScreenScissor(cb);
        World.Snap = oldSnap;

        // The thumbnail is shown by ImGui, so hand it the render target's colour texture directly -
        // this is what SdlImGuiRenderer.BindTexture(ITexture) exists for (Panels.cs flips V when it
        // images it, which is unrelated to the texture's own orientation).
        _partPreviews[name] = (rt, GameSparker.Game.ImguiRenderer!.BindTexture(rt.ColorTexture));
    }

    private void QueuePartPreview(string name, Rad3d rad)
    {
        if (!_partPreviews.ContainsKey(name))
            _previewQueue.Enqueue((name, rad));
    }
    
    /// <summary>
    /// Constructs a pick ray from screen coordinates, handling both perspective and orthographic cameras.
    /// For perspective, the ray originates at the camera position and goes through the near plane.
    /// For orthographic, the ray originates on the near plane at the mouse position and goes
    /// in the camera's forward direction (all rays are parallel).
    /// </summary>
    private (Vector3 Origin, Vector3 Direction) GetPickRay(int screenX, int screenY)
    {
        // The viewport is always the whole window (the old GraphicsDevice.Viewport was read for
        // these dimensions only; screenX/screenY arrive in window pixels).
        float ndcX = (2.0f * screenX) / GameSparker.Game.Window.Width - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / GameSparker.Game.Window.Height;
        
        var projMatrix = activeCamera.ProjectionMatrix;
        Matrix.Invert(projMatrix, out var invProj);
        var viewMatrix = activeCamera.ViewMatrix;
        Matrix.Invert(viewMatrix, out var invView);
        
        if (activeCamera is OrthoCamera)
        {
            // Orthographic: all rays are parallel in the camera's forward direction.
            // The origin varies per pixel — unproject the NDC point on the near plane.
            var nearView = Vector4.Transform(new Vector4(ndcX, ndcY, 0f, 1f), invProj);
            var nearWorld = Vector4.Transform(new Vector4(nearView.X, nearView.Y, nearView.Z, 1f), invView);
            var origin = new Vector3(nearWorld.X, nearWorld.Y, nearWorld.Z);
            var forward = Vector3.Normalize(activeCamera.LookAt - activeCamera.Position);
            return (origin, forward);
        }
        else
        {
            // Perspective: ray from camera position through the near plane point.
            var rayClip = new Vector4(ndcX, ndcY, -1f, 1f);
            var rayEye = Vector4.Transform(rayClip, invProj);
            rayEye.Z = -1.0f;
            rayEye.W = 0.0f;
            var rayWorld4 = Vector4.Transform(rayEye, invView);
            var direction = Vector3.Normalize(new Vector3(rayWorld4.X, rayWorld4.Y, rayWorld4.Z));
            return (activeCamera.Position, direction);
        }
    }

    /// <summary>
    /// Computes the intersection of the mouse ray with the horizontal ground plane (Y = 250)
    /// in world space. Returns false if the ray doesn't hit the plane (parallel or behind camera).
    /// </summary>
    private bool TryGetGroundPositionAtMouse(int screenX, int screenY, out Vector3 result)
    {
        result = default;
        
        var (rayOrigin, rayDirection) = GetPickRay(screenX, screenY);
        
        const float groundY = 250f;
        if (Math.Abs(rayDirection.Y) < 0.0001f) return false;
        float t = (groundY - rayOrigin.Y) / rayDirection.Y;
        if (t <= 0) return false;
        
        result = rayOrigin + rayDirection * t;
        return true;
    }
    
    private int PerformRayPicking(int screenX, int screenY)
    {
        if (ActiveTab.ScenePieces.Count == 0) return -1;
        
        var (rayOrigin, rayDirection) = GetPickRay(screenX, screenY);
        
        // Find closest intersected piece using proper ray-triangle intersection
        float closestDistance = float.MaxValue;
        int closestPieceId = -1;
        
        foreach (var piece in ActiveTab.ScenePieces)
        {
            if (piece.Obj == null) continue;
            
            var mesh = piece.Obj;
            
            // Create rotation matrix matching the game engine's order
            // Rotation is stored as (Pitch, Yaw, Roll) in degrees
            // Negate yaw to match game engine's coordinate system  
            var yaw = -piece.Rotation.Yaw.Radians;
            var pitch = piece.Rotation.Pitch.Radians;
            var roll = piece.Rotation.Roll.Radians;
            
            // Create individual rotation matrices and combine them
            var rotationMatrix = 
                Matrix.CreateRotationY((float)yaw) *
                Matrix.CreateRotationX((float)pitch) *
                Matrix.CreateRotationZ((float)roll);
            
            // Test each polygon
            foreach (var poly in mesh.Rad.Polys)
            {
                if (poly.Points.Length < 3) continue;
                
                // Transform all vertices to world space
                var worldVerts = new Vector3[poly.Points.Length];
                for (int i = 0; i < poly.Points.Length; i++)
                {
                    var localVert = new Vector3(
                        poly.Points[i].X,
                        poly.Points[i].Y,
                        poly.Points[i].Z
                    );
                    
                    // Apply rotation then translation
                    var rotated = Vector3.Transform(localVert, rotationMatrix);
                    worldVerts[i] = new Vector3(
                        rotated.X + (float)piece.Position.X,
                        rotated.Y + (float)piece.Position.Y,
                        rotated.Z + (float)piece.Position.Z
                    );
                }

                for (var i = 0; i < poly.Triangles.Length; i += 3)
                {
                    var v0 = worldVerts[poly.Triangles[i]];
                     var v1 = worldVerts[poly.Triangles[i + 1]];
                     var v2 = worldVerts[poly.Triangles[i + 2]];
                    
                    // Try both winding orders
                    if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v1, v2, out float dist))
                    {
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestPieceId = piece.Id;
                        }
                    }
                    else if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v2, v1, out dist))
                    {
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            closestPieceId = piece.Id;
                        }
                    }
                }
            }
        }
        
        return closestPieceId;
    }

    private int PerformWallRayPicking(int screenX, int screenY)
    {
        if (ActiveTab == null || ActiveTab.StageWalls.Count == 0)
            return -1;

        var (rayOrigin, rayDirection) = GetPickRay(screenX, screenY);
        float closestDistance = float.MaxValue;
        int closestWallId = -1;

        foreach (var wall in ActiveTab.StageWalls)
        {
            int n = wall.Count;
            int o = wall.Position;
            int p = wall.Offset;

            for (int q = 0; q < n; q++)
            {
                var center = wall.Direction switch
                {
                    WallDirection.Right => new Vector3(o, World.Ground, q * WALL_SEGMENT_SPACING + p),
                    WallDirection.Left => new Vector3(o, World.Ground, q * WALL_SEGMENT_SPACING + p),
                    WallDirection.Top => new Vector3(q * WALL_SEGMENT_SPACING + p, World.Ground, o),
                    WallDirection.Bottom => new Vector3(q * WALL_SEGMENT_SPACING + p, World.Ground, o),
                    _ => Vector3.Zero
                };

                // Top/bottom walls run along X; left/right walls run along Z.
                var halfExtents = wall.Direction is WallDirection.Top or WallDirection.Bottom
                    ? new Vector3(WALL_SEGMENT_HALF_LENGTH, WALL_SEGMENT_HALF_HEIGHT, WALL_SEGMENT_HALF_WIDTH)
                    : new Vector3(WALL_SEGMENT_HALF_WIDTH, WALL_SEGMENT_HALF_HEIGHT, WALL_SEGMENT_HALF_LENGTH);

                var boxMin = center - halfExtents;
                var boxMax = center + halfExtents;

                if (RayIntersectsBox(rayOrigin, rayDirection, boxMin, boxMax, out float dist) && dist < closestDistance)
                {
                    closestDistance = dist;
                    closestWallId = wall.Id;
                }
            }
        }

        return closestWallId;
    }

    private void RenderSelectedWallHighlight(NFMWorld.Graphics.ICommandBuffer cb, StageEditorTab tab)
    {
        if (tab.SelectedWallId < 0)
            return;

        var wall = tab.StageWalls.GetValueOrDefault(tab.SelectedWallId);
        if (wall == null)
            return;

        var boxes = new List<(Vector3 Min, Vector3 Max)>();
        var color = new Color(0.35f, 0.9f, 1f, 1f);

        int n = wall.Count;
        int o = wall.Position;
        int p = wall.Offset;
        for (int q = 0; q < n; q++)
        {
            var center = wall.Direction switch
            {
                WallDirection.Right => new Vector3(o, World.Ground, q * WALL_SEGMENT_SPACING + p),
                WallDirection.Left => new Vector3(o, World.Ground, q * WALL_SEGMENT_SPACING + p),
                WallDirection.Top => new Vector3(q * WALL_SEGMENT_SPACING + p, World.Ground, o),
                WallDirection.Bottom => new Vector3(q * WALL_SEGMENT_SPACING + p, World.Ground, o),
                _ => Vector3.Zero
            };

            var halfExtents = wall.Direction is WallDirection.Top or WallDirection.Bottom
                ? new Vector3(WALL_SEGMENT_HALF_LENGTH, WALL_SEGMENT_HALF_HEIGHT, WALL_SEGMENT_HALF_WIDTH)
                : new Vector3(WALL_SEGMENT_HALF_WIDTH, WALL_SEGMENT_HALF_HEIGHT, WALL_SEGMENT_HALF_LENGTH);

            boxes.Add((center - halfExtents, center + halfExtents));
        }

        if (boxes.Count == 0)
            return;

        // Was a raw BasicEffect + DrawUserPrimitives with DepthStencilState.None; Debug draws the
        // same wire boxes as thick lines through the shared debug line pipeline (depth off).
        Debug.RenderWireBoxes(cb, GameSparker.GraphicsDevice, CollectionsMarshal.AsSpan(boxes), color, activeCamera);
    }

    private bool RayIntersectsTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float distance)
    {
        const float EPSILON = 0.001f; // Increased for better tolerance
        distance = 0;
        
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        
        // Check for degenerate triangle
        var edgeLen1 = edge1.Length();
        var edgeLen2 = edge2.Length();
        if (edgeLen1 < EPSILON || edgeLen2 < EPSILON)
            return false;
        
        var h = Vector3.Cross(rayDirection, edge2);
        var a = Vector3.Dot(edge1, h);
        
        // More lenient parallel check
        if (Math.Abs(a) < EPSILON)
            return false;
        
        var f = 1.0f / a;
        var s = rayOrigin - v0;
        var u = f * Vector3.Dot(s, h);
        
        // More lenient bounds checking
        if (u < -EPSILON || u > 1.0f + EPSILON)
            return false;
        
        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(rayDirection, q);
        
        // More lenient bounds checking
        if (v < -EPSILON || u + v > 1.0f + EPSILON)
            return false;
        
        var t = f * Vector3.Dot(edge2, q);
        
        // Accept slightly negative distances for near-miss cases
        if (t > -EPSILON)
        {
            distance = Math.Max(t, 0); // Clamp to non-negative
            return true;
        }
        
        return false;
    }
    
    private bool RayIntersectsBox(Vector3 rayOrigin, Vector3 rayDirection, Vector3 boxMin, Vector3 boxMax, out float distance)
    {
        // Slab method for ray-AABB intersection
        distance = 0;
        float tmin = float.MinValue;
        float tmax = float.MaxValue;
        
        // Check X slab
        if (Math.Abs(rayDirection.X) > 0.0001f)
        {
            float tx1 = (boxMin.X - rayOrigin.X) / rayDirection.X;
            float tx2 = (boxMax.X - rayOrigin.X) / rayDirection.X;
            tmin = Math.Max(tmin, Math.Min(tx1, tx2));
            tmax = Math.Min(tmax, Math.Max(tx1, tx2));
        }
        else if (rayOrigin.X < boxMin.X || rayOrigin.X > boxMax.X)
        {
            return false;
        }
        
        // Check Y slab
        if (Math.Abs(rayDirection.Y) > 0.0001f)
        {
            float ty1 = (boxMin.Y - rayOrigin.Y) / rayDirection.Y;
            float ty2 = (boxMax.Y - rayOrigin.Y) / rayDirection.Y;
            tmin = Math.Max(tmin, Math.Min(ty1, ty2));
            tmax = Math.Min(tmax, Math.Max(ty1, ty2));
        }
        else if (rayOrigin.Y < boxMin.Y || rayOrigin.Y > boxMax.Y)
        {
            return false;
        }
        
        // Check Z slab
        if (Math.Abs(rayDirection.Z) > 0.0001f)
        {
            float tz1 = (boxMin.Z - rayOrigin.Z) / rayDirection.Z;
            float tz2 = (boxMax.Z - rayOrigin.Z) / rayDirection.Z;
            tmin = Math.Max(tmin, Math.Min(tz1, tz2));
            tmax = Math.Min(tmax, Math.Max(tz1, tz2));
        }
        else if (rayOrigin.Z < boxMin.Z || rayOrigin.Z > boxMax.Z)
        {
            return false;
        }
        
        if (tmax >= tmin && tmax >= 0)
        {
            distance = Math.Max(tmin, 0); // Return entry point distance
            return true;
        }
        
        return false;
    }

}
