using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.UI.Menu;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public class GaragePhase(GraphicsDevice graphicsDevice) : BaseStageRenderingPhase(graphicsDevice)
{
    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Returns the car that was selected.
    /// </summary>
    public event EventHandler<Rad3d>? CarSelected;

    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Indicates no selection was made; retain existing car, if any.
    /// </summary>
    public event EventHandler? CarSelectionCancelled;

    private int _selectedCarIdx = 0;

    private Collection _currentCollection = Collection.NFMM;
    private UnlimitedArray<Rad3d> _cars = BackendGameSparker.cars[Collection.NFMM];
    private BackendCar? _backendCar;
    private ClientCar? _car;
    
    private GarageUiView _garageUiView = new();

    private int _statsBarBaseX = 120;
    private int _statsBarBaseY = 200;
    private int _statsBarXGap = 130;
    private int _statsBarYGap = 75;
    private UnlimitedArray<string> _collections = [];
    private string _searchQuery = "";
    private int _autocompleteIndex = 0;
    private bool _inAutocomplete = false;
    private Rad3d[] _autocompleteMatches = [];
    private bool _openSearchPopup = false;
    private int _searchKbFocus = 0;
    public BackendStage? StageOverride;
    public ClientStageRenderer? ClientStageRendererOverride;
    private bool _loadedStageMusic = false;

    private PerspectiveCamera _camera = new();

    public GaragePhase(GraphicsDevice graphicsDevice, Rad3d currentCar) : this(graphicsDevice)
    {
        _selectedCarIdx = _cars.FindIndex(c =>
        {
            ArgumentNullException.ThrowIfNull(c);
            return c.FileName == currentCar.FileName;
        });

        if (_selectedCarIdx == -1) _selectedCarIdx = 0;

        foreach (var dir in Directory.GetDirectories("data/models"))
        {
            _collections.Add(dir);
        }
    }

    private void SetupCurrentCar()
    {
        if (StageOverride != null)
        {
            CurrentStage = StageOverride;
            clientStageRenderer = ClientStageRendererOverride ?? new ClientStageRenderer(GraphicsDevice, CurrentStage);
        }
        else
        {
            string[] stages = Directory.GetFiles("data/stages", "*.*", SearchOption.AllDirectories);
            string stagePath = "";
            while (string.IsNullOrEmpty(stagePath) || stagePath.Contains("rar2"))
            {
                stagePath = stages[(int)(URandom.Double() * stages.Length)];
            }

            string dir = new FileInfo(stagePath).Directory?.Name ?? "";
            if (dir == "stages") dir = "";
            else dir += "/";

            LoadStage(dir + Path.GetFileNameWithoutExtension(stagePath), false);
        }

        _backendCar = new BackendCar(_cars[_selectedCarIdx], 0, 0, 0, true);
        _car = new ClientCar(GraphicsDevice, _backendCar);
        CarsInRace[0] = _backendCar;

        camera.LookAt = new Vector3(0, 250, 400);
        camera.Position = new Vector3(-750, 50, 750);
        FovOverride = 53;

        RecreateScene();

        if (!_loadedStageMusic)
        {
            LoadStageMusic(true);
            _loadedStageMusic = true;
        }

        // create and position stat bars
        float switsLevel = (_car.Stats.Swits[2] - 220) / 90f;
        switsLevel = Math.Max(0.05f, switsLevel);
        _garageUiView.Bar0.TargetValue = switsLevel;

        float accel = (float)(_car.Stats.Acelf.X * _car.Stats.Acelf.Y * _car.Stats.Acelf.Z * _car.Stats.Grip / 7700);
        _garageUiView.Bar1.TargetValue = accel;

        _garageUiView.Bar2.TargetValue = (float)_car.Stats.Dishandle;

        float powerloss = _car.Stats.Powerloss / 5500000f;
        _garageUiView.Bar3.TargetValue = powerloss;

        float strength = ((float)_car.Stats.Moment + 0.5f) / 2.6f;
        _garageUiView.Bar4.TargetValue = strength;

        float health = (float)_car.Stats.Outdam / 1.05f + _car.Stats.Maxmag / 100000f;
        _garageUiView.Bar5.TargetValue = health;

        float airs = (_car.Stats.Airc * 2 * ((float)_car.Stats.Airs * 0.5f) * (float)_car.Stats.Bounce + 28f) / 100f;
        _garageUiView.Bar6.TargetValue = airs;

        float hglide = ((Math.Abs(_car.Stats.Flipy) + Math.Abs(_car.GroundAt)) / 2f / 70f) + (float)_car.Stats.Airs / 230f;
        _garageUiView.Bar7.TargetValue = hglide;

        float ab = _car.Stats.Airc / 75f;
        _garageUiView.Bar8.TargetValue = ab;
    }



    public override void GameTick()
    {
        _garageUiView.Update();
    }

    public override void Render()
    {
        base.Render();
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        _inAutocomplete = false;

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Collection"))
            {
                foreach (Collection key in BackendGameSparker.cars.Keys)
                {
                    if (BackendGameSparker.cars[key].Count > 0 && ImGui.MenuItem(key.ToString()))
                    {
                        GoToCollection(key);
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
                }
                ;

                ImGui.End();
            }

            _openSearchPopup = open;
            if (!_openSearchPopup)
            {
                _searchQuery = "";
            }
        }

        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Bold, 48));
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringStrokeAligned(_cars[_selectedCarIdx].Stats.Name, 0, 60, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, TextHorizontalAlignment.Center);
        G.SetColor(new Color(255, 255, 255));
        G.DrawStringAligned(_cars[_selectedCarIdx].Stats.Name, 0, 60, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, TextHorizontalAlignment.Center);

        DrawCarStats();
    }

    private bool HandleSearch()
    {
        if (ImGui.InputText($"Search {_currentCollection}", ref _searchQuery, 256, ImGuiInputTextFlags.EscapeClearsAll | ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (_autocompleteMatches.Length > 0)
            {
                _selectedCarIdx = _cars.ToList().FindIndex(c => c.Stats.Name == _autocompleteMatches[_autocompleteIndex].Stats.Name);
                SetupCurrentCar();
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
            _autocompleteMatches = _cars.ToArray();
        }
        else
        {
            _autocompleteMatches = _cars.ToList().FindAll(x => x.Stats.Name.ToLower().StartsWith(_searchQuery.ToLower())).ToArray();
        }
        string[] _autocompleteMatchedNames = _autocompleteMatches.Select(x => x.Stats.Name).ToArray();


        if (_autocompleteMatches.Length > 0)
        {
            _inAutocomplete = true;
            if (ImGui.ListBox("##AutocompleteEntries", ref _autocompleteIndex, _autocompleteMatchedNames, _autocompleteMatches.Length, _autocompleteMatches.Length))
            {
                _selectedCarIdx = _cars.ToList().FindIndex(c => c.Stats.Name == _autocompleteMatches[_autocompleteIndex].Stats.Name);
                SetupCurrentCar();
                _searchQuery = "";
                return true;
            }
            ;
        }

        return false;
    }

    private void DrawCarStats()
    {
        _garageUiView.LayoutAndRender(G.Viewport);
    }

    public override void Enter()
    {
        SetupCurrentCar();
    }

    public override void Exit()
    {
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
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

        if (imguiWantsKeyboard || _inAutocomplete) return;

        if (key == Keys.Right)
        {
            CycleCarRight();
        }
        else if (key == Keys.Left)
        {
            CycleCarLeft();
        }
        else if (key == Keys.Enter)
        {
            SelectedCar();
        }
        else if (key == Keys.Escape)
        {
            SelectionCancelled();
        }
        else if (key == Keys.S)
        {
            _openSearchPopup = true;
        }
    }

    private void SelectedCar()
    {
        if (CarSelected == null) throw new ArgumentNullException("Attempted to invoke CarSelected, but it was null.");
        CarSelected.Invoke(this, _car!.Rad);
    }

    private void SelectionCancelled()
    {
        if (CarSelectionCancelled == null) throw new ArgumentNullException("Attempted to invoke CarSelectionCancelled, but it was null.");
        CarSelectionCancelled.Invoke(this, EventArgs.Empty);
    }

    private void CycleCarRight()
    {
        _selectedCarIdx += 1;
        if (_selectedCarIdx >= _cars.Count) _selectedCarIdx -= _cars.Count;
        SetupCurrentCar();
    }

    private void CycleCarLeft()
    {
        _selectedCarIdx -= 1;
        if (_selectedCarIdx < 0) _selectedCarIdx = _cars.Count - 1;
        SetupCurrentCar();
    }

    private void GoToCollection(Collection collection)
    {
        _cars = BackendGameSparker.cars[collection];
        _selectedCarIdx = 0;
        _currentCollection = collection;
        SetupCurrentCar();
    }

    public override void WindowSizeChanged(int width, int height)
    {

    }
}
