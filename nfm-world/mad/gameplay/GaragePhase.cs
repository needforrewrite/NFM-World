using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Mad;
using NFMWorld.Util;

internal class GarageDynamicStatBar
{
    private readonly float maxSpeed = 100f;
    private readonly float speedUp = 0.1f;
    private readonly int fullBar = 100;

    public int maxWidth = 100;
    public int height = 10;

    private float currentValue = 0f;
    private float targetValue;
    private float speed;

    public int x;
    public int y;

    private string _name;

    private Color[] barColors =
    [
        new Color(255, 0, 0),
        new Color(128, 128, 128),
        new Color(255, 128, 0),
        new Color(128, 128, 128),
        new Color(255, 255, 0),
        new Color(128, 128, 128),
        new Color(128, 255, 0),
        new Color(128, 128, 128),
        new Color(0, 255, 0),
        new Color(128, 128, 128),
        new Color(0, 255, 128),
        new Color(128, 128, 128),
        new Color(0, 255, 255),
        new Color(128, 128, 128),
        new Color(0, 128, 255),
        new Color(128, 128, 128),
        new Color(0, 0, 255),
        new Color(128, 128, 128),
        new Color(128, 0, 255),
        new Color(128, 128, 128),
        new Color(255, 0, 255),
        new Color(128, 128, 128),
        new Color(255, 0, 128),
        new Color(128, 128, 128),
    ];

    public GarageDynamicStatBar(float targetValue, int x, int y, string name)
    {
        this.targetValue = targetValue * 100;
        speed = speedUp;
        this.x = x;
        this.y = y;
        _name = name;
    }

    public void Tick()
    {
        currentValue += speed;
        currentValue = Math.Min(targetValue, currentValue);

        speed += speedUp;
        speed = Math.Min(speed, maxSpeed);
    }

    private int GetColor(int lim, int i)
    {
        if (i < 0)
        {
            return i % lim + lim;
        }
        else
        {
            return i % lim;
        }
    }

    public void Render()
    {
        int multiples = 0;
        float remaining = currentValue;

        while (remaining > fullBar)
        {
            remaining -= fullBar;
            multiples++;
        }

        Color baseBarColorStart = multiples > 0 ? barColors[GetColor(barColors.Length, multiples - 1)] : new Color(0, 0, 0, 0);
        Color baseBarColorEnd = multiples > 0 ? barColors[GetColor(barColors.Length, multiples)] : new Color(0, 0, 0, 0);

        Color barColorStart = barColors[GetColor(barColors.Length, multiples)];
        Color barColorEnd = barColors[GetColor(barColors.Length, multiples + 1)];

        G.SetLinearGradient(x, y, maxWidth, height, [baseBarColorStart, baseBarColorEnd], null);
        G.FillRect(x, y, maxWidth, height);

        int barRatio = (int)(remaining / fullBar * 100);
        barRatio *= maxWidth / fullBar;

        G.SetLinearGradient(x, y, maxWidth, height, [barColorStart, barColorEnd], null);
        G.FillRect(x, y, barRatio, height);

        G.SetFont(new Font("Arial", 1, 20));
        G.SetColor(new Color(0, 0, 0));
        G.DrawString(_name, x, y - 5);

        G.SetFont(new Font("Arial", 1, 12));
        G.DrawString(((int)currentValue).ToString(), x + 5, y + height);

        DrawDividers();
    }

    // Draw the black thing that overlays the stat itself...
    private void DrawDividers()
    {
        G.SetColor(new Color(0, 0, 0));
        G.DrawLine(x, y + height, x + maxWidth, y + height);
        G.DrawLine(x, y, x, y + height);
        G.DrawLine(x + maxWidth, y, x + maxWidth, y + height);
    }
}

public class GaragePhase(GraphicsDevice graphicsDevice) : BasePhase
{
    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Returns the car that was selected.
    /// </summary>
    public event EventHandler<CarInfo>? CarSelected;

    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Indicates no selection was made; retain existing car, if any.
    /// </summary>
    public event EventHandler? CarSelectionCancelled;

    private Scene _garageScene = null!;

    private int _selectedCarIdx = 0;

    private UnlimitedArray<CarInfo> _cars = [
        ..GameSparker.cars,
        ..GameSparker.vendor_cars,
        ..GameSparker.user_cars,
    ];

    private UnlimitedArray<GarageDynamicStatBar> statBars = [];

    private int _statsBarBaseX = 120;
    private int _statsBarBaseY = 200;
    private int _statsBarXGap = 130;
    private int _statsBarYGap = 75;

    private PerspectiveCamera _camera = new();
    private Car _car;

    public GaragePhase(GraphicsDevice graphicsDevice, Car currentCar) : this(graphicsDevice)
    {
        _selectedCarIdx = _cars.FindIndex(c =>
        {
            ArgumentNullException.ThrowIfNull(c);
            return c.FileName == currentCar.FileName;
        });

        if (_selectedCarIdx == -1) _selectedCarIdx = 0;
    }

    private void SetupCurrentCar()
    {
        _car = new Car(_cars[_selectedCarIdx]);
        _garageScene = new Scene(
            graphicsDevice,
            [_car],
            _camera,
            []
        );

        // create and position stat bars
        float switsLevel = (_car.Stats.Swits[2] - 220) / 90f;
        switsLevel = Math.Max(0.05f, switsLevel);
        statBars[0] = new GarageDynamicStatBar(switsLevel, _statsBarBaseX, _statsBarBaseY, "Top Speed");

        float accel = _car.Stats.Acelf.X * _car.Stats.Acelf.Y * _car.Stats.Acelf.Z * (float)_car.Stats.Grip / 7700f;
        statBars[1] = new GarageDynamicStatBar(accel, _statsBarBaseX + _statsBarXGap, _statsBarBaseY, "Acceleration");

        statBars[2] = new GarageDynamicStatBar((float)_car.Stats.Dishandle, _statsBarBaseX + _statsBarXGap * 2, _statsBarBaseY, "Handling");

        float powerloss = _car.Stats.Powerloss / 4500000f;
        statBars[3] = new GarageDynamicStatBar(powerloss, _statsBarBaseX, _statsBarBaseY + _statsBarYGap, "Power Save");

        float strength = ((float)_car.Stats.Moment + 0.5f) / 2.6f;
        statBars[4] = new GarageDynamicStatBar(strength, _statsBarBaseX + _statsBarXGap, _statsBarBaseY + _statsBarYGap, "Strength");

        float health = (float)_car.Stats.Outdam * 1.35f;
        statBars[5] = new GarageDynamicStatBar(health, _statsBarBaseX + _statsBarXGap * 2, _statsBarBaseY + _statsBarYGap, "Max Health");

        float airs = (_car.Stats.Airc * 2 * ((float)_car.Stats.Airs * 0.5f) * (float)_car.Stats.Bounce + 28f) / 100f;
        statBars[6] = new GarageDynamicStatBar(airs, _statsBarBaseX, _statsBarBaseY + _statsBarYGap * 2, "Stunting");

        float hglide = (Math.Abs(_car.Stats.Flipy) + Math.Abs(_car.GroundAt)) / 2f / 60f;
        statBars[7] = new GarageDynamicStatBar(hglide, _statsBarBaseX + _statsBarXGap, _statsBarBaseY + _statsBarYGap * 2, "Hypergliding");

        float ab = Math.Max(0.05f, _car.Stats.Airc / 75f);
        statBars[8] = new GarageDynamicStatBar(ab, _statsBarBaseX + _statsBarXGap * 2, _statsBarBaseY + _statsBarYGap * 2, "AB'ing");
    }



    public override void GameTick()
    {
        foreach (GarageDynamicStatBar gb in statBars)
        {
            gb.Tick();
        }
    }

    public override void Render()
    {
        base.Render();

        graphicsDevice.BlendState = BlendState.Opaque;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;

        _car.Position = new Vector3(-500, -100 - _car.GroundAt, 200);
        _car.Rotation = new Euler(AngleSingle.FromDegrees(-30), _car.Rotation.Pitch, _car.Rotation.Roll);
        _camera.LookAt = new Vector3(0, 0, 0);
        _camera.Position = new Vector3(-600, -300, 1000);

        _garageScene.Render(false);
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        G.SetFont(new Font("Arial", 1, 48));
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringAligned(_car.Stats.Name, graphicsDevice.Viewport.Width, 120, TextHorizontalAlignment.Center);

        DrawCarStats();
    }

    private void DrawCarStats()
    {
        foreach (GarageDynamicStatBar gb in statBars)
        {
            gb.Render();
        }
    }

    public override void Enter()
    {
        World.ResetValues();
        World.Snap = new Color3(15, 0, 35);

        SetupCurrentCar();
    }

    public override void Exit()
    {
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
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
    }

    private void SelectedCar()
    {
        if (CarSelected == null) throw new ArgumentNullException("Attempted to invoke CarSelected, but it was null.");
        CarSelected.Invoke(this, _cars[_selectedCarIdx]);
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

    public override void WindowSizeChanged(int width, int height)
    {

    }
}