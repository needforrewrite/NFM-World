using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Mad.UI;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public abstract class BaseRacePhase(GraphicsDevice _graphicsDevice) : BaseStageRenderingPhase(_graphicsDevice)
{
    protected FollowCamera PlayerFollowCamera = new();

    public bool spectating = false;
    
    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    protected ViewMode currentViewMode = ViewMode.Follow;
    
    public virtual GameModes gamemode
    {
        get;
        set
        {
            field = value;
            gamemodeInstance = CreateGameMode();
            gamemodeInstance.Enter();
        }
    }

    protected BaseGamemode? gamemodeInstance { get; set; }

    public override void Exit()
    {
        base.Exit();
        GameSparker.CurrentMusic?.Unload();
    }


    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;

        if (!spectating)
        {
            if (key == bindings.Accelerate)
            {
                CarsInRace[playerCarIndex].Control.Up = true;
            }
            if (key == bindings.AerialBounce)
            {
                CarsInRace[playerCarIndex].Control.Up = true;
                CarsInRace[playerCarIndex].Control.Down = true;
            }
            if (key == bindings.AerialStrafe)
            {
                if (CarsInRace[playerCarIndex].Control.Up && CarsInRace[playerCarIndex].Control.Down)
                {
                    CarsInRace[playerCarIndex].Control.Left = true;
                    CarsInRace[playerCarIndex].Control.Right = true;
                }
                if (CarsInRace[playerCarIndex].Control.Left)
                    CarsInRace[playerCarIndex].Control.Right = true;
                else if (CarsInRace[playerCarIndex].Control.Right)
                    CarsInRace[playerCarIndex].Control.Left = true;
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
        
        if (!spectating)
        {
            if (key == bindings.Accelerate)
            {
                CarsInRace[playerCarIndex].Control.Up = false;
            }
            if (key == bindings.AerialStrafe)
            {
                if (CarsInRace[playerCarIndex].Control.Up && CarsInRace[playerCarIndex].Control.Down)
                {
                    CarsInRace[playerCarIndex].Control.Left = false;
                    CarsInRace[playerCarIndex].Control.Right = false;
                }
                if (CarsInRace[playerCarIndex].Control.Left)
                    CarsInRace[playerCarIndex].Control.Right = false;
                else if (CarsInRace[playerCarIndex].Control.Right)
                    CarsInRace[playerCarIndex].Control.Left = false;
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

    public virtual void ReloadGamemode()
    {
        gamemodeInstance = CreateGameMode();
        gamemodeInstance.Enter();
    }
    protected abstract BaseGamemode CreateGameMode();

    public override void Render()
    {
        base.Render();

        if(DebugDisplay) {
            FrameTrace.RenderMessages();
            G.SetColor(new Color(0, 0, 0));
            G.DrawString($"Render: {Program._lastFrameTime}ms", 100, 100);
            G.DrawString($"Tick: {Program._lastTickTime}μs", 100, 120);
            G.DrawString($"Power: {CarsInRace[0]?.Mad?.Power:0.00}", 100, 140);
            G.DrawString($"Ticks executed last frame: {Program._lastTickCount}", 100, 160);
        }

        gamemodeInstance!.Render();
    }
}