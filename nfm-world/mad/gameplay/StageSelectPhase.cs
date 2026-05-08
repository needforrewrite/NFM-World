using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Camera;
using NFMWorld.DriverInterface;
using NFMWorld.Util;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public class StageSelectPhase(GraphicsDevice graphicsDevice) : BaseStageRenderingPhase(graphicsDevice)
{
    public event EventHandler<BackendStage>? StageSelected;

    private UnlimitedArray<string> _stageCollections = [];
    private string _selectedCollection = "";
    private UnlimitedArray<string> _stagesInCollection = [];
    private int _selectedStageIndex = 0;
    private string _searchQuery = "";
    private bool _inAutocomplete = false;
    private int _autocompleteIndex = 0;
    private string[] _autocompleteMatches = [];
    private int _searchKbFocus = 0;
    private bool _openSearchPopup = false;

    private AroundStageCamera _aroundStageCamera = new();

    public override void Enter()
    {
        base.Enter();

        var directories = Directory.GetDirectories("data/stages");
        foreach (var dir in directories)
        {
            var entries = Directory.GetFiles(dir);
            if (entries.Length == 0) continue;

            var stageName = Path.GetFileName(dir);
            _stageCollections.Add(stageName);
        }
        // randomly select collection
        _selectedCollection = _stageCollections[new System.Random().Next(0, _stageCollections.Count)];
        LoadStagesInCollection(_selectedCollection);
        LoadStageInCollection();

        GameSparker.CurrentMusic?.Unload();
        GameSparker.CurrentMusic = IBackend.Backend.LoadMusic("data/music/nfm1/stageselectremastered.mp3", 0f);
        GameSparker.CurrentMusic?.Play();
    }

    private void LoadStagesInCollection(string collection)
    {
        _stagesInCollection.Clear();

        var dirPath = Path.Combine("data/stages", collection);
        var entries = Directory.GetFiles(dirPath, "*.txt");
        // first sort alphabetically
        Array.Sort(entries, NaturalCompare.CompareNatural);

        foreach (var entry in entries)
        {
            var stageFilename = new FileInfo(entry).Name.Replace(".txt", "");
            _stagesInCollection.Add(stageFilename);
        }
    }

    private void LoadStageInCollection()
    {
        World.DrawClouds = false;
        LoadStage(_selectedCollection + "/" + _stagesInCollection[_selectedStageIndex], false);
        World.FadeFrom = 9999999;
        _aroundStageCamera = new AroundStageCamera();
    }

    private void CycleStageInCollection(int direction)
    {
        _selectedStageIndex += direction;

        if (_selectedStageIndex < 0)
        {
            _selectedStageIndex = _stagesInCollection.Count - 1;
        }
        else if (_selectedStageIndex >= _stagesInCollection.Count)
        {
            _selectedStageIndex = 0;
        }

        LoadStageInCollection();
    }

    public override void GameTick()
    {
        base.GameTick();

        _aroundStageCamera.AroundStage(camera, CurrentStage);
    }

    public override void Render()
    {
        base.Render();
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Collection"))
            {
                foreach (string key in _stageCollections)
                {
                    if (ImGui.MenuItem(key.ToString()))
                    {
                        LoadStagesInCollection(key);
                        _selectedCollection = key;
                        _selectedStageIndex = 0;
                        LoadStageInCollection();
                    }
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Search", !_openSearchPopup))
            {
                _searchKbFocus++;
                if (HandleSearch())
                {
                    ImGui.CloseCurrentPopup();
                }
                

                ImGui.EndMenu();
            }
            else
            {
                if (!_openSearchPopup) _searchKbFocus = 0;
            }

            ImGui.EndMainMenuBar();
        }

        if (_openSearchPopup)
        {
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0));

            bool open = _openSearchPopup;

            if (ImGui.Begin("Search", ref open, ImGuiWindowFlags.NoResize))
            {
                _searchKbFocus++;
                if (HandleSearch())
                {
                    _openSearchPopup = false;
                    open = false;
                    ImGui.CloseCurrentPopup();
                };

                ImGui.End();
            }

            _openSearchPopup = open;
            if(!_openSearchPopup)
            {
                _searchQuery = "";
            }
        }

        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Bold, 48));
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringStrokeAligned(CurrentStage.Name, 0, 60, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, TextHorizontalAlignment.Center);
        G.SetColor(new Color(255, 255, 255));
        G.DrawStringAligned(CurrentStage.Name, 0, 60, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, TextHorizontalAlignment.Center);
    }

    private bool HandleSearch()
    {
        if (ImGui.InputText($"Search {_selectedCollection}", ref _searchQuery, 256, ImGuiInputTextFlags.EscapeClearsAll | ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (_autocompleteMatches.Length > 0)
            {
                _selectedStageIndex = _stagesInCollection.ToList().FindIndex(c => c == _autocompleteMatches[_autocompleteIndex]);
                LoadStageInCollection();
                _searchQuery = "";
                return true;
            }
        }

        if (_searchKbFocus == 1)
        {
            ImGui.SetKeyboardFocusHere(-1);
        }

        if (string.IsNullOrEmpty(_searchQuery))
        {
            _autocompleteMatches = _stagesInCollection.ToArray();
        }
        else
        {
            _autocompleteMatches = _stagesInCollection.ToList().FindAll(x => x.ToLower().StartsWith(_searchQuery.ToLower())).ToArray();
        }
        string[] _autocompleteMatchedNames = _autocompleteMatches.ToArray();


        if (_autocompleteMatches.Length > 0)
        {
            _inAutocomplete = true;
            if(ImGui.ListBox("##AutocompleteEntries", ref _autocompleteIndex, _autocompleteMatchedNames, _autocompleteMatches.Length, _autocompleteMatches.Length))
            {
                _selectedStageIndex = _stagesInCollection.ToList().FindIndex(c => c == _autocompleteMatches[_autocompleteIndex]);
                LoadStageInCollection();
                _searchQuery = "";
                return true;
            };
        }

        return false;
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (key == Keys.Down && _inAutocomplete)
        {
            _autocompleteIndex++;
            if (_autocompleteIndex >= _autocompleteMatches.Length)
            {
                _autocompleteIndex = 0;
            }
        }
        else if (key == Keys.Up && _inAutocomplete)
        {
            _autocompleteIndex--;
            if (_autocompleteIndex < 0)
            {
                _autocompleteIndex = _autocompleteMatches.Length - 1;
            }
        }

        if (imguiWantsKeyboard) return;

        if (key == Keys.Left)
        {
            CycleStageInCollection(-1);
        }
        else if (key == Keys.Right)
        {
            CycleStageInCollection(1);
        }
        else if (key == Keys.Enter)
        {
            StageSelected?.Invoke(this, CurrentStage);
        } else if (key == Keys.S)
        {
            _openSearchPopup = true;
        }
    }
}