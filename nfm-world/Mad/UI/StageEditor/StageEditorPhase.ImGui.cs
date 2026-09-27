using System.Collections.ObjectModel;
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
    public override void RenderImgui()
    {
        if (!_isOpen) return;
        
        RenderImGuiUI();
    }
    
    private void RenderImGuiUI()
    {
        var screenWidth = GameSparker.Game.Window.Width;
        var screenHeight = GameSparker.Game.Window.Height;
        
        // Menu bar at the top
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Stage"))
                {
                    _showNewStageDialog = true;
                    _newStageName = "";
                }
                
                if (ImGui.MenuItem("Load Stage"))
                {
                    _showLoadStageDialog = true;
                    RefreshAvailableStages();
                    _selectedStageIndex = -1;
                }
                
                if (ImGui.MenuItem("Save Stage", "", false, ActiveTab?.Stage != null))
                {
                    SaveStage();
                }

                if (ImGui.MenuItem("Export Top-Down Image...", "", false, ActiveTab?.Stage != null))
                {
                    _exportResultMessage = "";
                    _showExportDialog = true;
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("Exit to Main Menu"))
                {
                    // Check all tabs for unsaved changes
                    bool hasAnyUnsavedChanges = _tabs.Any(t => t.HasUnsavedChanges);
                    if (hasAnyUnsavedChanges)
                    {
                        _showExitWarningDialog = true;
                    }
                    else
                    {
                        GameSparker.ReturnToMainMenu();
                    }
                }
                
                ImGui.EndMenu();
            }
            
            if (ImGui.BeginMenu("Edit"))
            {
                ImGui.Separator();
                bool autoPolys = _autoGeneratePolys;
                if (ImGui.MenuItem("Auto-Generate Ground Polys", "", ref autoPolys, true))
                {
                    _autoGeneratePolys = autoPolys;
                    if (_autoGeneratePolys)
                        RecreateEnvironment();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("When disabled, ground polys are not regenerated during piece placement.\nThis significantly speeds up editing for large stages.\nRe-enable to refresh the polys mesh.");

                ImGui.Separator();
                if (ImGui.MenuItem("Properties", "", false, ActiveTab?.Stage != null))
                {
                    // Initialize dialog values from active tab's stored values
                    _editStageName = ActiveTab!.TabName;
                    _editSkyColor = ActiveTab.SkyColor;
                    _editFogColor = ActiveTab.FogColor;
                    _editGroundColor = ActiveTab.GroundColor;
                    _editPolysEnabled = ActiveTab.PolysEnabled;
                    if (_editPolysEnabled)
                    {
                        _editPolysColor = ActiveTab.PolysColor;
                    }
                    else
                    {
                        // Auto-calculate from ground color (reduce by 10 points)
                        _editPolysColor = new Color3(
                            (short)Math.Max(0, ActiveTab.GroundColor.R - 10),
                            (short)Math.Max(0, ActiveTab.GroundColor.G - 10),
                            (short)Math.Max(0, ActiveTab.GroundColor.B - 10)
                        );
                    }
                    _editCloudsEnabled = ActiveTab.CloudsEnabled;
                    _editCloudsColor = ActiveTab.CloudsColor;
                    _editCloudsParam4 = ActiveTab.CloudsParam4;
                    _editCloudsHeight = ActiveTab.CloudsHeight;
                    _editCloudCoverage = ActiveTab.CloudCoverage;
                    _editMountainsEnabled = ActiveTab.MountainsEnabled;
                    _editMountainsSeed = ActiveTab.MountainsSeed;
                    _editSnapA = ActiveTab.SnapA;
                    _editSnapB = ActiveTab.SnapB;
                    _editSnapC = ActiveTab.SnapC;
                    _editFadeFrom = ActiveTab.FadeFrom;
                    
                    _showPropertiesDialog = true;
                }
                
                ImGui.EndMenu();
            }
            
            if (ImGui.BeginMenu("View"))
            {
                if (ActiveTab == null)
                {
                    ImGui.TextDisabled("No stage loaded");
                }
                else if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
                {
                    if (ImGui.MenuItem("Orthographic", "", ActiveTab.TopDownOrtho))
                    {
                        ActiveTab.TopDownOrtho = !ActiveTab.TopDownOrtho;
                        UpdateCameraPosition();
                    }
                }
                else
                {
                    ImGui.TextDisabled("Switch to Top Down View for options");
                }
                
                ImGui.EndMenu();
            }
            
            // Display camera info and stage name
            if (ActiveTab?.Stage != null)
            {
                ImGui.SetNextItemWidth(200);
                ImGui.Text($"  |  Stage: {ActiveTab.TabName}");
                if (ActiveTab.HasUnsavedChanges)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.1f, 1.0f), "(unsaved)");
                }
                ImGui.SameLine();
                ImGui.TextDisabled($"  Yaw={ActiveTab.CameraYaw:F1}°  Pitch={ActiveTab.CameraPitch:F1}°  |  {ActiveTab.ScenePieces.Count} pieces");
                if (!_autoGeneratePolys)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.1f, 1.0f), "  |  Polys: OFF");
                }
            }
            else
            {
                ImGui.TextDisabled("  |  No stage loaded — File > New Stage or Load Stage");
            }
            
            ImGui.EndMainMenuBar();
        }
        
        // Draw tabs below menu bar (full width for stage file tabs)
        float menuBarHeight = ImGui.GetFrameHeight();
        ImGui.SetNextWindowPos(new Vector2(0, menuBarHeight));
        ImGui.SetNextWindowSize(new Vector2(screenWidth, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4, 4));
        ImGui.Begin("StageTabsWindow", 
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav);
        
        if (ImGui.BeginTabBar("StageTabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs))
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool open = true;
                string tabLabel = tab.TabName + (tab.HasUnsavedChanges ? "*" : "");
                
                if (ImGui.BeginTabItem(tabLabel, ref open))
                {
                    if (_activeTabIndex != i)
                    {
                        // Switch to new tab: tab stored values are authoritative — never read World back into them.
                        // Just restore this tab's World properties and rebuild environment.
                        _activeTabIndex = i;
                        UpdateCameraPosition();
                        ApplyTabWorldValuesToWorld();
                        RecreateEnvironment();
                        RebuildAllWalls();
                        RecreateScene();
                    }
                    ImGui.EndTabItem();
                }
                
                if (!open)
                {
                    CloseTab(i);
                    break; // Exit loop after closing a tab to avoid index issues
                }
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        ImGui.PopStyleVar();
        
        float tabBarHeight = ImGui.GetFrameHeight();
        float totalHeaderHeight = menuBarHeight + tabBarHeight + 12; // Add 4px spacing
        
        // New Stage Dialog
        if (_showNewStageDialog)
        {
            ImGui.OpenPopup("New Stage");
        }
        
        if (ImGui.BeginPopupModal("New Stage", ref _showNewStageDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Enter stage name:");
            ImGui.Separator();
            
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("##stagename", ref _newStageName, 100);
            
            if (!string.IsNullOrWhiteSpace(_newStageName))
            {
                var filename = ConvertStageNameToFilename(_newStageName);
                ImGui.Text($"Filename: {filename}.txt");
            }
            
            ImGui.Spacing();
            ImGui.Text("Start piece (placed at 0, 0, 0):");
            ImGui.SetNextItemWidth(300);
            if (ImGui.BeginCombo("##starpiece", _newStageStartPartOptions[_newStageStartPartIndex]))
            {
                for (int si = 0; si < _newStageStartPartOptions.Length; si++)
                {
                    bool sel = si == _newStageStartPartIndex;
                    if (ImGui.Selectable(_newStageStartPartOptions[si], sel))
                        _newStageStartPartIndex = si;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This part will be automatically placed at the origin (0, 0, 0)\nwhen the stage is created.");
            
            ImGui.Separator();
            
            if (ImGui.Button("Create", new Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newStageName))
                {
                    string? startPart = _newStageStartPartIndex > 0 ? _newStageStartPartOptions[_newStageStartPartIndex] : null;
                    CreateEmptyStage(_newStageName, startPart);
                    _showNewStageDialog = false;
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showNewStageDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Load Stage Dialog
        if (_showLoadStageDialog)
        {
            ImGui.OpenPopup("Load Stage");
        }
        
        if (ImGui.BeginPopupModal("Load Stage", ref _showLoadStageDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Select a stage to load:");
            ImGui.Separator();
            
            ImGui.BeginChild("StageList", new Vector2(300, 200), (ImGuiChildFlags)1);
            
            for (int i = 0; i < _availableStages.Count; i++)
            {
                if (ImGui.Selectable(_availableStages[i], _selectedStageIndex == i))
                {
                    _selectedStageIndex = i;
                }
            }
            
            ImGui.EndChild();
            
            if (_availableStages.Count == 0)
            {
                ImGui.TextDisabled("No user stages found in data/stages/user/");
            }
            
            ImGui.Separator();
            
            if (ImGui.Button("Load", new Vector2(120, 0)))
            {
                if (_selectedStageIndex >= 0 && _selectedStageIndex < _availableStages.Count)
                {
                    LoadStage(_availableStages[_selectedStageIndex]);
                    _showLoadStageDialog = false;
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showLoadStageDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Properties Dialog
        if (_showPropertiesDialog)
        {
            ImGui.OpenPopup("Stage Properties");
        }
        
        if (ImGui.BeginPopupModal("Stage Properties", ref _showPropertiesDialog, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav))
        {
            ImGui.Text("Configure stage properties (changes preview live):");
            ImGui.Separator();
            
            ImGui.Text("Stage Name:");
            ImGui.SetNextItemWidth(300);
            ImGui.InputText("##stagename_edit", ref _editStageName, 100);
            
            ImGui.Separator();
            
            ImGui.Text("Sky Color:");
            if (ImGui.ColorEdit3("##skycolor", ref _editSkyColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
            {
                // Live preview
                World.Sky = _editSkyColor;
                if (ActiveTab?.StageRenderer != null) ActiveTab.StageRenderer.sky = new Sky(GameSparker.GraphicsDevice);
            }
            
            ImGui.Text("Fog Color:");
            if (ImGui.ColorEdit3("##fogcolor", ref _editFogColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
            {
                // Live preview
                World.Fog = _editFogColor;
            }
            
            ImGui.Text("Ground Color:");
            if (ImGui.ColorEdit3("##groundcolor", ref _editGroundColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
            {
                // Live preview
                World.GroundColor = _editGroundColor;
                ActiveTab?.StageRenderer?.ground = new Ground(GameSparker.GraphicsDevice);
            }
            
            ImGui.Separator();
            
            if (ImGui.Checkbox("Enable Ground Polys", ref _editPolysEnabled))
            {
                // Live preview
                World.HasPolys = _editPolysEnabled;
                World.DrawPolys = _editPolysEnabled;
                if (_editPolysEnabled && ActiveTab?.StageRenderer != null && ActiveTab?.Stage != null)
                {
                    World.GroundPolysColor = _editPolysColor;
                    ActiveTab.StageRenderer.polys = Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count);
                }
                else if (!_editPolysEnabled && ActiveTab?.StageRenderer != null)
                {
                    ActiveTab.StageRenderer.polys = null;
                }
            }
            if (_editPolysEnabled)
            {
                ImGui.Text("Polys Color:");
                if (ImGui.ColorEdit3("##polyscolor", ref _editPolysColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
                {
                    // Live preview
                    World.GroundPolysColor = _editPolysColor;
                    ActiveTab?.StageRenderer?.polys = Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count);
                }
            }
            
            ImGui.Separator();
            
            if (ImGui.Checkbox("Enable Clouds", ref _editCloudsEnabled))
            {
                // Live preview
                World.HasClouds = _editCloudsEnabled;
                World.DrawClouds = _editCloudsEnabled;
                if (_editCloudsEnabled && ActiveTab?.StageRenderer != null)
                {
                    World.Clouds =
                    [
                        _editCloudsColor.R, 
                        _editCloudsColor.G, 
                        _editCloudsColor.B, 
                        _editCloudsParam4, 
                        _editCloudsHeight
                    ];
                    World.CloudCoverage = _editCloudCoverage;
                    ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000);
                }
                else if (!_editCloudsEnabled && ActiveTab?.StageRenderer != null)
                {
                    ActiveTab.StageRenderer.clouds = null;
                }
            }
            if (_editCloudsEnabled)
            {
                ImGui.Text("Clouds Color:");
                if (ImGui.ColorEdit3("##cloudscolor", ref _editCloudsColor, ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.DisplayRgb))
                {
                    // Live preview
                    World.Clouds[0] = _editCloudsColor.R;
                    World.Clouds[1] = _editCloudsColor.G;
                    World.Clouds[2] = _editCloudsColor.B;
                    ActiveTab?.StageRenderer?.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000);
                }
                
                ImGui.Text("Clouds Height:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragInt("##cloudsheight", ref _editCloudsHeight, 10f, -10000, 10000))
                {
                    // Live preview
                    World.Clouds[4] = _editCloudsHeight;
                    ActiveTab?.StageRenderer?.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000);
                }
                
                ImGui.Text("Clouds Parameter 4:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##cloudsparam4", ref _editCloudsParam4))
                {
                    // Live preview
                    World.Clouds[3] = _editCloudsParam4;
                    ActiveTab?.StageRenderer?.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000);
                }
                
                ImGui.Text("Cloud Coverage:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("##cloudcoverage", ref _editCloudCoverage, 0.0f, 10.0f))
                {
                    // Live preview
                    World.CloudCoverage = _editCloudCoverage;
                    ActiveTab?.StageRenderer?.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000);
                }
            }
            
            if (ImGui.Checkbox("Enable Mountains", ref _editMountainsEnabled))
            {
                // Live preview
                World.DrawMountains = _editMountainsEnabled;
                if (_editMountainsEnabled && ActiveTab?.StageRenderer != null)
                {
                    World.MountainSeed = _editMountainsSeed;
                    ActiveTab.StageRenderer.mountains = Environment.MakeMountains(-10000, 10000, -10000, 10000);
                }
                else if (!_editMountainsEnabled && ActiveTab?.StageRenderer != null)
                {
                    ActiveTab.StageRenderer.mountains = null;
                }
            }
            if (_editMountainsEnabled)
            {
                ImGui.Text("Mountains Seed:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##mountainsseed", ref _editMountainsSeed))
                {
                    // Live preview
                    World.MountainSeed = _editMountainsSeed;
                    if (ActiveTab?.StageRenderer != null)
                    {
                        ActiveTab.StageRenderer.mountains = Environment.MakeMountains(-10000, 10000, -10000, 10000);
                    }
                }
            }
            
            ImGui.Separator();
            
            ImGui.Text("Environment Lighting (Snap):");
            ImGui.Text("Brightness adjustment for each RGB channel (-100 to 100):");
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderInt("A (Red)", ref _editSnapA, -100, 100))
            {
                // Live preview
                World.Snap = new Color3((short)_editSnapA, (short)_editSnapB, (short)_editSnapC);
            }
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderInt("B (Green)", ref _editSnapB, -100, 100))
            {
                // Live preview
                World.Snap = new Color3((short)_editSnapA, (short)_editSnapB, (short)_editSnapC);
            }
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderInt("C (Blue)", ref _editSnapC, -100, 100))
            {
                // Live preview
                World.Snap = new Color3((short)_editSnapA, (short)_editSnapB, (short)_editSnapC);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Brightness values that affect environment lighting.\nHigher values = brighter environment.");
            }
            
            ImGui.Separator();
            
            ImGui.Text("Fade From Distance:");
            ImGui.SetNextItemWidth(200);
            if (ImGui.DragInt("##fadefrom", ref _editFadeFrom, 100f, 1000, 50000))
            {
                // Live preview
                World.FadeFrom = _editFadeFrom;
            }
            
            ImGui.Separator();
            
            if (ImGui.Button("Apply", new Vector2(120, 0)))
            {
                if (ActiveTab != null)
                {
                    // Update the tab name
                    ActiveTab.TabName = _editStageName;
                    
                    // Store all properties in the active tab
                    ActiveTab.SkyColor = _editSkyColor;
                    ActiveTab.FogColor = _editFogColor;
                    ActiveTab.GroundColor = _editGroundColor;
                    ActiveTab.PolysColor = _editPolysColor;
                    ActiveTab.PolysEnabled = _editPolysEnabled;
                    ActiveTab.CloudsEnabled = _editCloudsEnabled;
                    ActiveTab.CloudsColor = _editCloudsColor;
                    ActiveTab.CloudsParam4 = _editCloudsParam4;
                    ActiveTab.CloudsHeight = _editCloudsHeight;
                    ActiveTab.CloudCoverage = _editCloudCoverage;
                    ActiveTab.MountainsEnabled = _editMountainsEnabled;
                    ActiveTab.MountainsSeed = _editMountainsSeed;
                    ActiveTab.SnapA = _editSnapA;
                    ActiveTab.SnapB = _editSnapB;
                    ActiveTab.SnapC = _editSnapC;
                    ActiveTab.FadeFrom = _editFadeFrom;
                    
                    ApplyTabWorldValuesToWorld();
                    RecreateEnvironment();
                    RecreateScene();
                    ActiveTab.HasUnsavedChanges = true;
                }
                
                _showPropertiesDialog = false;
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                // Undo any live-preview changes to World by restoring from the tab's stored values
                ApplyTabWorldValuesToWorld();
                RecreateEnvironment();
                _showPropertiesDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Export Top-Down Image Dialog
        if (_showExportDialog)
        {
            ImGui.OpenPopup("Export Top-Down Image");
        }

        if (ImGui.BeginPopupModal("Export Top-Down Image", ref _showExportDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Render the stage from directly above and save as a PNG.");
            ImGui.Separator();

            ImGui.Text("Image Width (px):");
            ImGui.SetNextItemWidth(200);
            ImGui.DragInt("##expW", ref _exportWidth, 32f, 256, 8192);
            _exportWidth = (int)MathF.Round(_exportWidth / 32f) * 32;

            ImGui.Text("Image Height (px):");
            ImGui.SetNextItemWidth(200);
            ImGui.DragInt("##expH", ref _exportHeight, 32f, 256, 8192);
            _exportHeight = (int)MathF.Round(_exportHeight / 32f) * 32;

            ImGui.Text("Padding (world units):");
            ImGui.SetNextItemWidth(200);
            ImGui.DragInt("##expPad", ref _exportPadding, 50f, 0, 10000);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Extra space around the stage bounding box.");

            if (ActiveTab?.StageFileName != null)
                ImGui.TextDisabled($"Output: data/stages/user/{ActiveTab.StageFileName}_topdown.png");

            ImGui.Separator();

            if (ImGui.Button("Export", new Vector2(120, 0)))
            {
                // Deferred: the export binds a render target, which needs the frame's command
                // buffer - it runs at the top of the next Render(cb, ...). Keep dialog open to
                // show result message.
                _exportRequested = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Close", new Vector2(120, 0)))
            {
                _showExportDialog = false;
                ImGui.CloseCurrentPopup();
            }

            if (!string.IsNullOrEmpty(_exportResultMessage))
            {
                ImGui.Spacing();
                bool isError = _exportResultMessage.StartsWith("Error");
                if (isError)
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), _exportResultMessage);
                else
                    ImGui.TextColored(new Vector4(0.3f, 1f, 0.3f, 1f), _exportResultMessage);
            }

            ImGui.EndPopup();
        }

        // Exit Warning Dialog
        if (_showExitWarningDialog)
        {
            ImGui.OpenPopup("Unsaved Changes");
        }
        
        if (ImGui.BeginPopupModal("Unsaved Changes", ref _showExitWarningDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("You have unsaved changes in one or more stages.");
            ImGui.Text("Are you sure you want to exit without saving?");
            ImGui.Separator();
            
            if (ImGui.Button("Exit Without Saving", new Vector2(150, 0)))
            {
                _showExitWarningDialog = false;
                GameSparker.ReturnToMainMenu();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showExitWarningDialog = false;
            }
            
            ImGui.EndPopup();
        }
        
        // Close Tab Warning Dialog
        if (_showCloseTabWarningDialog)
        {
            ImGui.OpenPopup("Close Tab?");
        }
        
        if (ImGui.BeginPopupModal("Close Tab?", ref _showCloseTabWarningDialog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_tabToClose >= 0 && _tabToClose < _tabs.Count)
            {
                var tab = _tabs[_tabToClose];
                ImGui.Text($"Stage '{tab.TabName}' has unsaved changes.");
                ImGui.Text("Are you sure you want to close it without saving?");
                ImGui.Separator();
                
                if (ImGui.Button("Close Without Saving", new Vector2(170, 0)))
                {
                    PerformCloseTab(_tabToClose);
                    _showCloseTabWarningDialog = false;
                    _tabToClose = -1;
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    _showCloseTabWarningDialog = false;
                    _tabToClose = -1;
                }
            }
            
            ImGui.EndPopup();
        }
        
        // If no stage is loaded, show a message in the center
        if (ActiveTab?.Stage == null)
        {
            ImGui.SetNextWindowPos(new Vector2(screenWidth / 2 - 200, screenHeight / 2 - 50));
            ImGui.SetNextWindowSize(new Vector2(400, 100));
            ImGui.Begin("No Stage Loaded", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
            ImGui.Text("No stage is currently loaded.");
            ImGui.Text("Use File > New Stage to create a new stage,");
            ImGui.Text("or File > Load Stage to load an existing one.");
            ImGui.End();
            return;
        }
        
        // LEFT PANEL - Hierarchy
        ImGui.SetNextWindowPos(new Vector2(0, totalHeaderHeight));
        ImGui.SetNextWindowSize(new Vector2(_hierarchyWidth, screenHeight - totalHeaderHeight - _partsLibraryHeight));
        
        ImGui.Begin("Hierarchy", 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoCollapse);
        
        RenderHierarchy();
        ImGui.End();
        
        // RIGHT PANEL - Inspector
        ImGui.SetNextWindowPos(new Vector2(screenWidth - _inspectorWidth, totalHeaderHeight));
        ImGui.SetNextWindowSize(new Vector2(_inspectorWidth, screenHeight - totalHeaderHeight - _partsLibraryHeight));
        
        ImGui.Begin("Inspector", 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoCollapse);
        
        RenderInspector();
        ImGui.End();
        
        // BOTTOM PANEL - Parts Library
        ImGui.SetNextWindowPos(new Vector2(0, screenHeight - _partsLibraryHeight));
        ImGui.SetNextWindowSize(new Vector2(screenWidth, _partsLibraryHeight));
        
        ImGui.Begin("Stage Parts Library", 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoCollapse);
        
        RenderPartsLibrary();
        ImGui.End();
        
        // Draw viewport tabs overlay (spans full width of viewport at the top)
        ImGui.SetNextWindowPos(new Vector2(_hierarchyWidth, totalHeaderHeight));
        ImGui.SetNextWindowSize(new Vector2(screenWidth - _hierarchyWidth - _inspectorWidth, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4, 4));
        ImGui.Begin("ViewportTabs", 
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav);
        
        if (ImGui.BeginTabBar("ViewModeTabs", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("3D Scene View"))
            {
                if (ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.Scene)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.Scene;
                    UpdateCameraPosition();
                }
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Top Down View"))
            {
                if (ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.TopDown)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.TopDown;
                    UpdateCameraPosition();
                }
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        ImGui.PopStyleVar();
        
        float viewportTabsHeight = ImGui.GetFrameHeight();
        
        // Calculate viewport bounds (center area minus the UI panels, accounting for all header bars)
        _viewportMin = new Vector2(_hierarchyWidth, totalHeaderHeight + viewportTabsHeight);
        _viewportMax = new Vector2(screenWidth - _inspectorWidth, screenHeight - _partsLibraryHeight);
        if (IsMouseInViewport(_mouseX, _mouseY))
        {
            // Calculate 3D world position at ground level (Y = 250)
            var (rayOrigin, rayDirection) = GetPickRay(_mouseX, _mouseY);
            
            // Intersect with ground plane (Y = 250)
            float groundY = 250f;
            float t = (groundY - rayOrigin.Y) / rayDirection.Y;
            
            if (t > 0)
            {
                var groundPos = rayOrigin + rayDirection * t;
                
                // Show tooltip at bottom center of viewport
                var tooltipPos = new Vector2(
                    _viewportMin.X + (_viewportMax.X - _viewportMin.X) / 2 - 150,
                    _viewportMax.Y - 30
                );
                
                ImGui.SetNextWindowPos(tooltipPos);
                ImGui.SetNextWindowBgAlpha(0.8f);
                ImGui.Begin("CursorPos",
                    ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoFocusOnAppearing |
                    ImGuiWindowFlags.NoNav);
                
                if (_pendingPlacementPartIndex >= 0)
                {
                    var placingPart = _availableParts[_pendingPlacementPartIndex];
                    string placingName = placingPart.FileName.Contains('/') ? placingPart.FileName[(placingPart.FileName.LastIndexOf('/') + 1)..] : placingPart.FileName;
                    ImGui.TextColored(new Vector4(0.1f, 0.9f, 1.0f, 1.0f), $"Placing: {placingName}");
                    ImGui.SameLine();
                    string snapInfo = _snapEnabled ? $"Snap:ON" : "Snap:OFF";
                    string gridSnapInfo = _gridSnapEnabled ? $"Grid:{_gridSnapSize:F0}" : "Grid:OFF";
                    ImGui.TextDisabled($"  X:{groundPos.X:F0}  Y:{groundPos.Y + _pendingPlacementYOff:F0}  Z:{groundPos.Z:F0}  Yaw:{_pendingPlacementYaw:F0}°  [{snapInfo}]  [{gridSnapInfo}]   [Q/E] Rotate  [LMB] Place  [Esc] Cancel");
                }
                else
                {
                    ImGui.Text($"X: {groundPos.X:F0}    Y: 0 ({groundY:F0})    Z: {groundPos.Z:F0}");
                }
                
                ImGui.End();
            }
        }
        
        // Draw selection rectangle overlay on viewport
        if (_isRectSelecting)
        {
            var dl = ImGui.GetForegroundDrawList();
            var ra = new Vector2(_rectSelectStartX, _rectSelectStartY);
            var rb = new Vector2(_rectSelectEndX, _rectSelectEndY);
            dl.AddRectFilled(ra, rb, ImGui.ColorConvertFloat4ToU32(new Vector4(0.26f, 0.59f, 0.98f, 0.15f)));
            dl.AddRect(ra, rb, ImGui.ColorConvertFloat4ToU32(new Vector4(0.26f, 0.59f, 0.98f, 0.9f)), 0f, ImDrawFlags.None, 1.5f);
        }
        
        if (!_isOpen)
        {
            GameSparker.ReturnToMainMenu();
        }
    }
    
    // ── Undo / Redo ──────────────────────────────────────────────────────────

    private static WallSnapshot CreateWallSnapshot(EditorStageWall wall)
    {
        return new WallSnapshot(wall.Id, wall.Direction, wall.Count, wall.Position, wall.Offset);
    }

}
