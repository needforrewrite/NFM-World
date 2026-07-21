using System.Collections.ObjectModel;
using Hexa.NET.ImGui;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private void CreateEmptyStage(string stageName, string? startPartName = null)
    {
        // Create a new tab with empty stage
        var tab = new StageEditorTab();
        tab.TabName = stageName;
        tab.StageFileName = ConvertStageNameToFilename(stageName);
        tab.Stage = new EditorStage();
        tab.StageRenderer = new ClientStageRenderer(_graphicsDevice, tab.Stage);
        
        // Set default values for properties in the tab
        tab.SkyColor = new Color3(135, 206, 235);
        tab.FogColor = new Color3(135, 206, 235);
        tab.GroundColor = new Color3(100, 200, 100);
        tab.PolysColor = new Color3(90, 190, 90);
        tab.PolysEnabled = false;
        tab.CloudsEnabled = false;
        tab.CloudsColor = new Color3(210, 210, 210);
        tab.CloudsParam4 = 1;
        tab.CloudsHeight = -1000;
        tab.CloudCoverage = 1.0f;
        tab.MountainsEnabled = false;
        tab.MountainsSeed = 0;
        tab.SnapA = 0;
        tab.SnapB = 0;
        tab.SnapC = 0;
        tab.FadeFrom = 10000;
        
        // Also update World for immediate effect
        World.Sky = tab.SkyColor;
        World.Fog = tab.FogColor;
        World.GroundColor = tab.GroundColor;
        World.FadeFrom = tab.FadeFrom;
        World.HasPolys = false;
        World.DrawPolys = false;
        World.HasClouds = false;
        World.DrawClouds = false;
        World.DrawMountains = false;
        World.Snap = new Color3(0, 0, 0);
        
        _tabs.Add(tab);
        _activeTabIndex = _tabs.Count - 1;
        
        // Place start piece at origin if specified
        if (!string.IsNullOrEmpty(startPartName))
        {
            var partData = BackendGameSparker.GetStagePart(startPartName);
            if (partData.Rad != null)
            {
                var startPos = new f64Vector3((fix64)0, (fix64)250, (fix64)0);
                var startMesh = StageObject.CreateDefaultObject(partData.Rad, startPos, f64Euler.Identity);
                int partId = tab.GetNextPieceId();
                var instance = new StagePieceInstance(startMesh, partId);
                instance.Position = startPos;
                instance.Rotation = f64Euler.Identity;
                tab.ScenePieces.Add(instance);
                tab.Stage.pieces.Add(startMesh);
                Logging.Info($"Placed start piece '{startPartName}' at origin.");
            }
            else
            {
                Logging.Warning($"Start piece '{startPartName}' could not be loaded (GetStagePart returned null Rad).");
            }
        }
        
        RebuildClientRenderer();
        SaveStage(); // Automatically save the new stage
        
        Logging.Info($"Created new stage: {stageName} (filename: {tab.StageFileName})");
    }
    
    private string ConvertStageNameToFilename(string stageName)
    {
        // Convert to lowercase and replace spaces with underscores
        return stageName.ToLower().Replace(' ', '_');
    }
    
    private void SaveStage()
    {
        if (ActiveTab == null || ActiveTab.Stage == null || string.IsNullOrWhiteSpace(ActiveTab.StageFileName))
        {
            Logging.Info("Cannot save: no stage loaded");
            return;
        }
        
        // Ensure the user stages directory exists
        var userStagesDir = "data/stages/user";
        Directory.CreateDirectory(userStagesDir);
        
        var filePath = $"{userStagesDir}/{ActiveTab.StageFileName}.txt";
        
        try
        {
            using var writer = new StreamWriter(filePath);
            
            // Write stage parameters from active tab's stored values
            writer.WriteLine($"name({ActiveTab.TabName})");
            writer.WriteLine($"sky({(int)ActiveTab.SkyColor.R},{(int)ActiveTab.SkyColor.G},{(int)ActiveTab.SkyColor.B})");
            writer.WriteLine($"fog({(int)ActiveTab.FogColor.R},{(int)ActiveTab.FogColor.G},{(int)ActiveTab.FogColor.B})");
            writer.WriteLine($"ground({(int)ActiveTab.GroundColor.R},{(int)ActiveTab.GroundColor.G},{(int)ActiveTab.GroundColor.B})");
            
            // Write polys parameter
            if (ActiveTab.PolysEnabled)
            {
                writer.WriteLine($"polys({(int)ActiveTab.PolysColor.R},{(int)ActiveTab.PolysColor.G},{(int)ActiveTab.PolysColor.B})");
            }
            else
            {
                writer.WriteLine("polys(false)");
            }
            
            // Write snap parameter
            writer.WriteLine($"snap({ActiveTab.SnapA},{ActiveTab.SnapB},{ActiveTab.SnapC})");
            
            // Write clouds parameter
            if (ActiveTab.CloudsEnabled)
            {
                writer.WriteLine($"clouds({(int)ActiveTab.CloudsColor.R},{(int)ActiveTab.CloudsColor.G},{(int)ActiveTab.CloudsColor.B},{ActiveTab.CloudsParam4},{ActiveTab.CloudsHeight})");
                writer.WriteLine($"cloudcoverage({ActiveTab.CloudCoverage})");
            }
            else
            {
                writer.WriteLine("clouds(false)");
            }
            
            // Write mountains parameter
            if (ActiveTab.MountainsEnabled)
            {
                writer.WriteLine($"mountains({ActiveTab.MountainsSeed})");
            }
            else
            {
                writer.WriteLine("mountains(false)");
            }
            
            writer.WriteLine($"fadefrom({ActiveTab.FadeFrom})");
            
            // Write unknown parameters
            foreach (var param in ActiveTab.UnknownParameters)
            {
                writer.WriteLine(param);
            }

            // Write known-but-unsupported directives exactly as loaded.
            foreach (var directive in ActiveTab.PreservedDirectives)
            {
                writer.WriteLine(directive);
            }
            
            writer.WriteLine();
            
            // ── Build piece lists ──────────────────────────────────────────────────
            var allNonWall = ActiveTab.ScenePieces
                .Where(p => !p.PiecePlacement.IsWall)
                .ToList();
            var groupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
            var ungroupedPieces = allNonWall.Where(p => !groupedIds.Contains(p.Id)).ToList();
            int ungroupedSlotSave = ActiveTab.UngroupedOrderIndex >= 0
                ? Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count)
                : ActiveTab.HierarchyGroups.Count; // default: after all groups
            
            // Local helper: serialise one piece as set/chk/fix
            void WritePiece(StagePieceInstance piece)
            {
                if (piece.Obj == null) return;
                var pos = piece.Position;
                var rot = piece.Rotation;
                string pieceId;
#if WRITE_NFMM_PIECES_AS_STRING
                int numericId = -1;
                if (piece.Name.StartsWith("nfmm/"))
                {
                    var baseName = piece.Name.Substring(5);
                    var idx = Array.IndexOf(BackendGameSparker.StageRads, baseName);
                    if (idx >= 0) { numericId = idx + 10; pieceId = numericId.ToString(); }
                    else pieceId = piece.Name;
                }
                else
#endif
                {
                    pieceId = piece.Name;
                }
                int yCoord = (int)pos.Y;
                int rotX   = (int)rot.Xz.Degrees;
                if (piece.PiecePlacement.Type == PiecePlacementType.FixHoop)
                {
                    writer.WriteLine($"fix({pieceId},{(int)pos.X},{(int)pos.Z},{yCoord},{rotX})");
                }
                else if (piece.PiecePlacement.Type == PiecePlacementType.CheckPoint)
                {
                    bool isAir = piece.Name.Contains("nfmm/aircheckpoint");
                    if (yCoord == 250) writer.WriteLine($"chk({pieceId},{(int)pos.X},{(int)pos.Z},{rotX})");
                    else { int fileY = isAir ? yCoord : 250 - yCoord; writer.WriteLine($"chk({pieceId},{(int)pos.X},{(int)pos.Z},{rotX},{fileY})"); }
                }
                else
                {
                    if (yCoord == 250) writer.WriteLine($"set({pieceId},{(int)pos.X},{(int)pos.Z},{rotX})");
                    else writer.WriteLine($"set({pieceId},{(int)pos.X},{(int)pos.Z},{rotX},{250 - yCoord})");
                }
            }
            
            // ── Write pieces in visual order ────────────────────────────────────────
            // New format: each group is preceded by  #editor_group(Name)
            // and its pieces appear immediately below.  Ungrouped pieces appear at
            // ungroupedSlotSave (0 = before all groups, groups.Count = after all groups).
            for (int slot = 0; slot <= ActiveTab.HierarchyGroups.Count; slot++)
            {
                if (slot == ungroupedSlotSave && ungroupedPieces.Count > 0)
                {
                    foreach (var piece in ungroupedPieces)
                        WritePiece(piece);
                    writer.WriteLine();
                }
                if (slot < ActiveTab.HierarchyGroups.Count)
                {
                    var group = ActiveTab.HierarchyGroups[slot];
                    var groupPieces = group.PieceIds
                        .Select(id => allNonWall.Find(p => p.Id == id))
                        .Where(p => p != null).ToList()!;
                    writer.WriteLine($"#editor_group({group.Name})");
                    foreach (var piece in groupPieces)
                        WritePiece(piece);
                    writer.WriteLine();
                }
            }
            
            // ── Stage walls ─────────────────────────────────────────────────────────
            if (ActiveTab.StageWalls.Count > 0)
            {
                foreach (var wall in ActiveTab.StageWalls)
                {
                    string command = wall.Direction switch
                    {
                        WallDirection.Right  => "maxr",
                        WallDirection.Left   => "maxl",
                        WallDirection.Top    => "maxt",
                        WallDirection.Bottom => "maxb",
                        _                              => "maxr"
                    };
                    writer.WriteLine($"{command}({wall.Count},{wall.Position},{wall.Offset})");
                }
                writer.WriteLine();
            }
            
            // ── Editor metadata (non-default ungrouped order) ────────────────────────
            if (ActiveTab.UngroupedOrderIndex >= 0)
                writer.WriteLine($"#editor_ungrouped_order({ActiveTab.UngroupedOrderIndex})");
            
            Logging.Info($"Stage saved to: {filePath}");
            ActiveTab.HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error saving stage: {ex.Message}");
        }
    }
    
    private void ExportTopDownImage()
    {
        if (ActiveTab?.Stage == null || ActiveTab.StageRenderer == null || ActiveTab.Scene == null)
        {
            _exportResultMessage = "No stage loaded.";
            return;
        }

        // Calculate bounding box from all pieces
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var piece in ActiveTab.ScenePieces)
        {
            float x = (float)piece.Position.X;
            float z = (float)piece.Position.Z;
            float r = piece.Rad.MaxRadius > 0 ? (float)piece.Rad.MaxRadius : 500f;
            minX = Math.Min(minX, x - r);
            maxX = Math.Max(maxX, x + r);
            minZ = Math.Min(minZ, z - r);
            maxZ = Math.Max(maxZ, z + r);
        }

        if (minX > maxX) { minX = -1000; maxX = 1000; minZ = -1000; maxZ = 1000; }

        minX -= _exportPadding; maxX += _exportPadding;
        minZ -= _exportPadding; maxZ += _exportPadding;

        float stageWidth = maxX - minX;
        float stageDepth = maxZ - minZ;
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        // Create export render target
        var rt = new RenderTarget2D(_graphicsDevice, _exportWidth, _exportHeight, false, SurfaceFormat.Color, DepthFormat.Depth24);
        var prevRTs = _graphicsDevice.GetRenderTargets();
        var prevViewport = _graphicsDevice.Viewport;

        try
        {
            _graphicsDevice.SetRenderTarget(rt);
            _graphicsDevice.Viewport = new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, _exportWidth, _exportHeight);
            _graphicsDevice.Clear(Color.Transparent);

            // Build a dedicated ortho camera sized to cover the whole stage
            var exportCam = new OrthoCamera
            {
                Width = _exportWidth,
                Height = _exportHeight
            };
            // OrthoScale = world units per pixel — choose the larger axis so nothing is clipped
            exportCam.OrthoScale = Math.Max(stageWidth / _exportWidth, stageDepth / _exportHeight);
            exportCam.PositionWithoutInterpolation = new Vector3(centerX, -50000f, centerZ);
            exportCam.LookAtWithoutInterpolation  = new Vector3(centerX, 0f, centerZ);
            exportCam.UpWithoutInterpolation      = Vector3.UnitZ;
            exportCam.OnBeforeRender(1f);

            // Swap the scene's active camera
            var prevCamera = activeCamera;
            activeCamera = exportCam;
            ActiveTab.Scene.ActiveCamera = exportCam;

            // Suppress environment and fog for a clean top-down render
            var prevFadeFrom  = World.FadeFrom;
            var prevGround    = ActiveTab.StageRenderer.ground;
            var prevSky       = ActiveTab.StageRenderer.sky;
            var prevPolys     = ActiveTab.StageRenderer.polys;
            var prevClouds    = ActiveTab.StageRenderer.clouds;
            var prevMountains = ActiveTab.StageRenderer.mountains;

            World.FadeFrom = 9999999;
            ActiveTab.StageRenderer.ground    = null!;
            ActiveTab.StageRenderer.sky       = null!;
            ActiveTab.StageRenderer.polys     = null;
            ActiveTab.StageRenderer.clouds    = null;
            ActiveTab.StageRenderer.mountains = null;
            // Use pure magenta as a chroma-key background — it can't appear in stage geometry
            var prevSkyWorld = World.Sky;
            World.Sky = new Color3(255, 0, 255);

            try
            {
                ActiveTab.Scene.Render(1f, false);
            }
            finally
            {
                ActiveTab.StageRenderer.ground    = prevGround;
                ActiveTab.StageRenderer.sky       = prevSky;
                ActiveTab.StageRenderer.polys     = prevPolys;
                ActiveTab.StageRenderer.clouds    = prevClouds;
                ActiveTab.StageRenderer.mountains = prevMountains;
                World.FadeFrom  = prevFadeFrom;
                World.Sky       = prevSkyWorld;
                activeCamera    = prevCamera;
                ActiveTab.Scene.ActiveCamera = prevCamera;
            }
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(prevRTs);
            _graphicsDevice.Viewport = prevViewport;
        }

        // Remove background by sampling the dominant border color and flood-filling
        // connected pixels. This is robust even if Scene.Render clears to its own color.
        var pixels = new Color[_exportWidth * _exportHeight];
        rt.GetData(pixels);

        int QuantizeColor(Color c)
        {
            int r = c.R >> 3;
            int g = c.G >> 3;
            int b = c.B >> 3;
            return (r << 10) | (g << 5) | b;
        }

        bool IsNearMatte(Color c, Color matte, int tolerance)
        {
            return Math.Abs(c.R - matte.R) <= tolerance &&
                   Math.Abs(c.G - matte.G) <= tolerance &&
                   Math.Abs(c.B - matte.B) <= tolerance;
        }

        var borderCounts = new Dictionary<int, int>();
        var borderSamples = new Dictionary<int, (int SumR, int SumG, int SumB, int Count)>();

        void AddBorderSample(Color c)
        {
            int key = QuantizeColor(c);
            if (!borderCounts.TryGetValue(key, out var currentCount))
                currentCount = 0;
            borderCounts[key] = currentCount + 1;

            if (!borderSamples.TryGetValue(key, out var sample))
                sample = (0, 0, 0, 0);
            borderSamples[key] = (sample.SumR + c.R, sample.SumG + c.G, sample.SumB + c.B, sample.Count + 1);
        }

        int width = _exportWidth;
        int height = _exportHeight;

        for (int x = 0; x < width; x++)
        {
            AddBorderSample(pixels[x]);
            AddBorderSample(pixels[(height - 1) * width + x]);
        }

        for (int y = 0; y < height; y++)
        {
            AddBorderSample(pixels[y * width]);
            AddBorderSample(pixels[y * width + (width - 1)]);
        }

        int matteKey = 0;
        int matteCount = -1;
        foreach (var kvp in borderCounts)
        {
            if (kvp.Value > matteCount)
            {
                matteKey = kvp.Key;
                matteCount = kvp.Value;
            }
        }

        var matteSample = borderSamples[matteKey];
        var matteColor = new Color(
            (byte)(matteSample.SumR / Math.Max(1, matteSample.Count)),
            (byte)(matteSample.SumG / Math.Max(1, matteSample.Count)),
            (byte)(matteSample.SumB / Math.Max(1, matteSample.Count)),
            255);

        const int matteTolerance = 40;
        var visited = new bool[pixels.Length];
        var queue = new Queue<int>();

        void EnqueueIfBackground(int index)
        {
            if (visited[index])
                return;

            var c = pixels[index];
            if (!IsNearMatte(c, matteColor, matteTolerance))
                return;

            visited[index] = true;
            queue.Enqueue(index);
        }

        for (int x = 0; x < width; x++)
        {
            EnqueueIfBackground(x);
            EnqueueIfBackground((height - 1) * width + x);
        }

        for (int y = 0; y < height; y++)
        {
            EnqueueIfBackground(y * width);
            EnqueueIfBackground(y * width + (width - 1));
        }

        int transparentCount = 0;
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            pixels[idx] = Color.Transparent;
            transparentCount++;

            int px = idx % width;
            int py = idx / width;

            if (px > 0)
                EnqueueIfBackground(idx - 1);
            if (px < width - 1)
                EnqueueIfBackground(idx + 1);
            if (py > 0)
                EnqueueIfBackground(idx - width);
            if (py < height - 1)
                EnqueueIfBackground(idx + width);
        }

        // Second pass: remove enclosed matte-colored islands that are not connected
        // to the border flood fill (e.g. pockets fully surrounded by track meshes).
        int enclosedTransparentCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].A > 0 && IsNearMatte(pixels[i], matteColor, matteTolerance))
            {
                pixels[i] = Color.Transparent;
                enclosedTransparentCount++;
            }
        }

        Logging.Info($"Top-down export matte key: R={matteColor.R},G={matteColor.G},B={matteColor.B}, edgeRemoved={transparentCount}, enclosedRemoved={enclosedTransparentCount}");

        // Write back to a plain Texture2D so SaveAsPng carries our modified alpha.
        var exportTex = new Texture2D(_graphicsDevice, _exportWidth, _exportHeight, false, SurfaceFormat.Color);
        exportTex.SetData(pixels);

        // Save PNG next to the stage file
        var exportDir  = "data/stages/user";
        Directory.CreateDirectory(exportDir);
        var filePath = $"{exportDir}/{ActiveTab.StageFileName}_topdown.png";

        try
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            exportTex.SaveAsPng(fs, _exportWidth, _exportHeight);
            _exportResultMessage = $"Saved: {filePath}";
            Logging.Info($"Exported top-down image: {filePath}");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            _exportResultMessage = $"Error: {ex.Message}";
            Logging.Error($"Export failed: {ex.Message}");
        }
        finally
        {
            rt.Dispose();
            exportTex.Dispose();
        }
    }

    private void RefreshAvailableStages()
    {
        _availableStages.Clear();
        
        var userStagesDir = "data/stages/user";
        if (!Directory.Exists(userStagesDir))
        {
            return;
        }
        
        var files = Directory.GetFiles(userStagesDir, "*.txt");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            _availableStages.Add(fileName);
        }
        
        _availableStages.Sort();
    }
    
    private void LoadStage(string stageFileName)
    {
        // Check if this stage is already open in a tab
        foreach (var tab in _tabs)
        {
            if (tab.StageFileName == stageFileName)
            {
                // Stage already open, switch to that tab
                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i] == tab)
                    {
                        _activeTabIndex = i;
                        UpdateCameraPosition();
                        break;
                    }
                }
                Logging.Info($"Stage '{stageFileName}' is already open, switched to that tab.");
                return;
            }
        }
        
        try
        {
            // Create a new tab
            var tab = new StageEditorTab();
            tab.StageFileName = stageFileName;
            
            // Load the stage using the Stage class (it expects filename without extension)
            tab.Stage = new BackendStage($"user/{stageFileName}");
            tab.TabName = tab.Stage.Name;
            
            // Remove all wall pieces from the stage BEFORE creating the ClientStageRenderer
            // so it never includes them as children
            int removedCount = 0;
            for (int i = tab.Stage.pieces.Count - 1; i >= 0; i--)
            {
                var piece = tab.Stage.pieces[i];
                if (piece is StageObject collisionObject && (collisionObject.FileName == "thewall" || collisionObject.FileName.Contains("wall")))
                {
                    tab.Stage.pieces.RemoveAt(i);
                    removedCount++;
                }
            }
            Logging.Info($"Removed {removedCount} wall pieces from stage");
            
            tab.StageRenderer = new ClientStageRenderer(_graphicsDevice, tab.Stage);
            
            var stageLoader = tab.Stage.stageLoader;

            tab.UngroupedOrderIndex = stageLoader.UngroupedOrderIndex;
            tab.UnknownParameters.AddRange(stageLoader.unknownParameters);
            LoadPreservedDirectives(tab, stageFileName);

            foreach (var wallDef in stageLoader.wallDefs)
            {
                tab.StageWalls.Add(new EditorStageWall(wallDef.Direction, wallDef.Count, wallDef.Position, wallDef.Offset, tab.GetNextPieceId()));
            }
            
            // Populate editor pieces from loaded stage
            foreach (var piece in tab.Stage.pieces)
            {
                if (piece is not StageObject collisionObject)
                    continue;
                
                // Skip wall pieces - they're handled as EditorStageWalls
                if (collisionObject.OriginalPlacement.IsWall)
                    continue;

                var instance = new StagePieceInstance(collisionObject, tab.GetNextPieceId());
                
                tab.ScenePieces.Add(instance);
            }
            
            // ── Resolve group membership ────────────────────────────────────────────
            foreach (var group in stageLoader.groups)
            {
                // New format group: #editor_group(Name) with no keys, pieces are tracked by file order instead
                var hierarchyGroup = new HierarchyGroup
                {
                    Id = tab.GetNextGroupId(),
                    Name = group.Name,
                    PieceIds = group.Pieces
                        .Select(piece => tab.ScenePieces.FirstOrDefault(p => p.PiecePlacement == piece)?.Id ?? -1)
                        .Where(id => id != -1)
                        .ToList()
                };
                tab.HierarchyGroups.Add(hierarchyGroup);
                
                // Old format group: #editor_group(Name,x:z,...)
                {
                    var keys  = group.CoordinateKeys;
                    var resolved = new List<int>();
                    foreach (var key in keys)
                    {
                        if (key.Contains(':'))
                        {
                            var kparts = key.Split(':');
                            if (kparts.Length == 2 &&
                                int.TryParse(kparts[0], out int kx) &&
                                int.TryParse(kparts[1], out int kz))
                            {
                                var match = tab.ScenePieces.FirstOrDefault(p =>
                                    Math.Abs((int)p.Position.X - kx) <= 1 &&
                                    Math.Abs((int)p.Position.Z - kz) <= 1);
                                if (match != null && !resolved.Contains(match.Id))
                                    resolved.Add(match.Id);
                            }
                        }
                        else if (int.TryParse(key, out int legacyIdx) && legacyIdx >= 0 && legacyIdx < tab.ScenePieces.Count)
                        {
                            var legacyPiece = tab.ScenePieces.ByIndex(legacyIdx);
                            if (!resolved.Contains(legacyPiece.Id))
                                resolved.Add(legacyPiece.Id);
                        }
                    }
                    hierarchyGroup.PieceIds.AddRange(resolved);
                }
            }

            // Store properties in the tab from World (set by Stage constructor)
            tab.TabName = tab.Stage.Name;
            tab.SkyColor = World.Sky;
            tab.FogColor = World.Fog;
            tab.GroundColor = World.GroundColor;
            tab.PolysEnabled = World.HasPolys;
            if (World.HasPolys)
            {
                tab.PolysColor = World.GroundPolysColor;
            }
            else
            {
                // Auto-calculate from ground color (reduce by 10 points)
                tab.PolysColor = new Color3(
                    (short)Math.Max(0, World.GroundColor.R - 10),
                    (short)Math.Max(0, World.GroundColor.G - 10),
                    (short)Math.Max(0, World.GroundColor.B - 10)
                );
            }
            tab.CloudsEnabled = World.HasClouds;
            if (World.HasClouds)
            {
                tab.CloudsColor = new Color3((short)World.Clouds[0], (short)World.Clouds[1], (short)World.Clouds[2]);
                tab.CloudsParam4 = World.Clouds[3];
                tab.CloudsHeight = World.Clouds[4];
                tab.CloudCoverage = World.CloudCoverage;
            }
            tab.MountainsEnabled = World.DrawMountains;
            tab.MountainsSeed = World.MountainSeed;
            tab.SnapA = World.Snap.R;
            tab.SnapB = World.Snap.G;
            tab.SnapC = World.Snap.B;
            tab.FadeFrom = World.FadeFrom;
            
            // Add tab and activate it
            _tabs.Add(tab);
            _activeTabIndex = _tabs.Count - 1;
            
            // Recreate scene at the very end to ensure it has the final clean pieces array
            RecreateScene();
            
            // Rebuild walls AFTER recreating scene
            RebuildAllWalls();
            
            Logging.Info($"Loaded stage: {tab.Stage.Name}");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Error($"Error loading stage: {ex.Message}");
        }
    }

    private static bool ShouldPreserveDirective(string line)
    {
        foreach (var prefix in _preservedDirectivePrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void LoadPreservedDirectives(StageEditorTab tab, string stageFileName)
    {
        tab.PreservedDirectives.Clear();

        var filePath = $"data/stages/user/{stageFileName}.txt";
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (ShouldPreserveDirective(line))
                {
                    tab.PreservedDirectives.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Logging.Warning($"Failed to load preserved directives from '{filePath}': {ex.Message}");
        }
    }
    
}
