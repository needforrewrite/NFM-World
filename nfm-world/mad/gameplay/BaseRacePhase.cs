using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Camera;
using NFMWorld.UI;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Mad;

namespace NFMWorld.Gameplay;

public abstract class BaseRacePhase(GraphicsDevice _graphicsDevice) : BaseStageRenderingPhase(_graphicsDevice), IRaceValues
{
    BackendStage IRaceValues.CurrentStage => CurrentStage;

    public RaceState raceState
    {
        get;
        set
        {
            field = value;
            RaceStateChanged?.Invoke(this, value);
        }
    } = RaceState.InProgress;
    
    public event EventHandler<RaceState>? RaceStateChanged;

    protected FollowCamera PlayerFollowCamera = new();
    protected AroundCamera PlayerAroundCamera = new();
    protected AroundStageCamera StageAroundCamera = new();

    public bool spectating = false;
    
    // Track which keys are currently pressed to properly handle meta-bindings
    private HashSet<Keys> _pressedKeys = new();
    
    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    protected ViewMode currentViewMode = ViewMode.Follow;
    
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
        
        // Track pressed keys
        _pressedKeys.Add(key);
        
        // Update control state based on all currently pressed keys
        UpdateControlState();

        // Handle non-movement keys
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
    }
    
    private void UpdateControlState()
    {
        if (spectating) return;
        
        var bindings = SettingsMenu.Bindings;
        var control = CarsInRace[playerCarIndex].Control;
        
        // determine base key states
        bool acceleratePressed = _pressedKeys.Contains(bindings.Accelerate);
        bool brakePressed = _pressedKeys.Contains(bindings.Brake);
        bool turnLeftPressed = _pressedKeys.Contains(bindings.TurnLeft);
        bool turnRightPressed = _pressedKeys.Contains(bindings.TurnRight);
        bool aerialBouncePressed = _pressedKeys.Contains(bindings.AerialBounce);
        bool aerialStrafePressed = _pressedKeys.Contains(bindings.AerialStrafe);
        bool handbrakePressed = _pressedKeys.Contains(bindings.Handbrake);
        
        // apply Up/Down controls
        control.Up = acceleratePressed || aerialBouncePressed;
        control.Down = brakePressed || aerialBouncePressed;
        
        // apply Left/Right controls with AerialStrafe logic
        bool baseLeft = turnLeftPressed;
        bool baseRight = turnRightPressed;
        
        if (aerialStrafePressed)
        {
            // AerialStrafe enables smooth turning by activating both directions
            if (control.Up && control.Down)
            {
                baseLeft = true;
                baseRight = true;
            }
            else if (baseLeft)
            {
                // left is pressed - also enable right for smooth left turn
                baseRight = true;
            }
            else if (baseRight)
            {
                // right is pressed - also enable left for smooth right turn
                baseLeft = true;
            }
        }
        
        control.Left = baseLeft;
        control.Right = baseRight;
        control.Handb = handbrakePressed;
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);

        var bindings = SettingsMenu.Bindings;
        
        // track released keys
        _pressedKeys.Remove(key);
        
        // update control state based on remaining pressed keys
        UpdateControlState();

        // handle special cases
        if (key == Keys.Escape)
        {
            // this seems to be currently unused
            CarsInRace[playerCarIndex].Control.Exit = false;
        }
        if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 0;
        }
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
    }

    public override void Render()
    {
        base.Render();

        if(DebugDisplay) {
            RenderMessages();
            G.SetColor(new Color(0, 0, 0));
            G.DrawString($"Render: {WorldGame._lastFrameTime}ms", 100, 100);
            G.DrawString($"Tick: {WorldGame._lastTickTime}μs", 100, 120);
            G.DrawString($"Power: {CarsInRace[0]?.Mad?.Power:0.00}", 100, 140);
            G.DrawString($"Ticks executed last frame: {WorldGame._lastTickCount}", 100, 160);
        }
    }

    private static void RenderMessages()
    {
        if (!FrameTrace.IsEnabled) return;
        
        var y = 0f;
        const float x = 250;
        const float increment = 20;
        
        G.SetColor(new Color(0, 0, 0));
        foreach (var message in FrameTrace.GetMessages())
        {
            y += increment;
            G.DrawString(message, (int)x, (int)y);
        }
    }

}