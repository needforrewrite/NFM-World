using NFMWorld.Mad.UI;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public abstract class BaseRacePhase : BasePhase
{
    public PerspectiveCamera camera = new();
    public Camera[] lightCameras = [
        new OrthoCamera()
        {
            Width = 3000,
            Height = 3000
        },
        new OrthoCamera()
        {
            Width = 16384,
            Height = 16384
        },
        new OrthoCamera()
        {
            Width = 65536,
            Height = 65536
        }
    ];

    public Stage CurrentStage = null!;
    public Scene current_scene;

    public UnlimitedArray<InGameCar> CarsInRace = [];
    public int playerCarIndex = 0;
    public FollowCamera PlayerFollowCamera = new();
    
    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    protected ViewMode currentViewMode = ViewMode.Follow;

    public abstract GameModes gamemode { get; set; }
    protected BaseGamemode? gamemodeInstance { get; set; }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;
        
        if (key == bindings.Accelerate)
        {
            CarsInRace[playerCarIndex].Control.Up = true;
        }
        if (key == bindings.Brake)
        {
            CarsInRace[playerCarIndex].Control.Down = true;
        }
        if (key == bindings.TurnRight)
        {
            CarsInRace[playerCarIndex].Control.Right = true;
        }
        if (key == bindings.TurnLeft)
        {
            CarsInRace[playerCarIndex].Control.Left = true;
        }
        if (key == bindings.Handbrake)
        {
            CarsInRace[playerCarIndex].Control.Handb = true;
        }
        if (key == bindings.Enter)
        {
            CarsInRace[playerCarIndex].Control.Enter = true;
        }
        if (key == bindings.LookBack)
        {
            CarsInRace[playerCarIndex].Control.Lookback = -1;
        }
        if (key == bindings.LookLeft)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 3;
        }
        if (key == bindings.LookRight)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 2;
        }
        if (key == bindings.ToggleMusic)
        {
            CarsInRace[playerCarIndex].Control.Mutem = !CarsInRace[playerCarIndex].Control.Mutem;
        }

        if (key == bindings.ToggleSFX)
        {
            CarsInRace[playerCarIndex].Control.Mutes = !CarsInRace[playerCarIndex].Control.Mutes;
        }

        if (key == bindings.ToggleArrace)
        {
            CarsInRace[playerCarIndex].Control.Arrace = !CarsInRace[playerCarIndex].Control.Arrace;
        }

        if (key == bindings.ToggleRadar)
        {
            CarsInRace[playerCarIndex].Control.Radar = !CarsInRace[playerCarIndex].Control.Radar;
        }
        if (key == bindings.CycleView)
        {
            currentViewMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
        }

        gamemodeInstance?.KeyPressed(key);
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);

        var bindings = SettingsMenu.Bindings;
        
        if (CarsInRace[playerCarIndex].Control.Multion < 2)
        {
            if (key == bindings.Accelerate)
            {
                CarsInRace[playerCarIndex].Control.Up = false;
            }
            if (key == bindings.Brake)
            {
                CarsInRace[playerCarIndex].Control.Down = false;
            }
            if (key == bindings.TurnRight)
            {
                CarsInRace[playerCarIndex].Control.Right = false;
            }
            if (key == bindings.TurnLeft)
            {
                CarsInRace[playerCarIndex].Control.Left = false;
            }
            if (key == bindings.Handbrake)
            {
                CarsInRace[playerCarIndex].Control.Handb = false;
            }
        }
        if (key == Keys.Escape)
        {
            CarsInRace[playerCarIndex].Control.Exit = false;
        }
        if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 0;
        }

        gamemodeInstance?.KeyReleased(key);
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
    }

    public abstract void ReloadGamemode();
}