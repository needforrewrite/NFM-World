using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Mad;
using NFMWorld.Util;

internal class GarageDynamicStatBar
{
    private readonly float maxSpeed = 35f;
    private readonly float speedUp = 0.1f;
    private readonly float maxValue = 1000f;
    private readonly int fullBar = 100;

    private readonly int maxWidth = 100;
    private readonly int height = 10;

    private float currentValue = 0f;
    private float targetValue;
    private float speed;

    private int x;
    private int y;

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

    /*
    private Color[] barColors =
    [
        new Color(255, 0, 0),
        new Color(255, 255, 0),
        new Color(90, 255, 0),
        new Color(0, 128, 255),
        new Color(0, 0, 255),
        new Color(255, 0, 255),
        new Color(255, 0, 0),
        new Color(255, 255, 0),
        new Color(90, 255, 0),
        new Color(0, 128, 255),
        new Color(0, 0, 255),
        new Color(255, 0, 255),
    ];
    */

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
        if (currentValue >= Math.Min(targetValue, maxValue))
        {
            currentValue = Math.Min(targetValue, maxValue);
        }

        speed += speedUp;
        speed = Math.Min(speed, maxSpeed);
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

        Color baseBarColorStart = multiples > 0 ? barColors[multiples - 1] : new Color(0, 0, 0, 0);
        Color baseBarColorEnd = multiples > 0 ? barColors[multiples] : new Color(0, 0, 0, 0);

        Color barColorStart = barColors[multiples];
        Color barColorEnd = barColors[multiples + 1];

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
        G.DrawString(((int)currentValue).ToString(), x, y + height);
    }
}

public class GaragePhase(GraphicsDevice graphicsDevice) : BasePhase
{
    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Returns the car that was selected.
    /// </summary>
    public event EventHandler<Car>? CarSelected;

    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Indicates no selection was made; retain existing car, if any.
    /// </summary>
    public event EventHandler? CarSelectionCancelled;

    private Scene _garageScene = null!;

    private int _selectedCarIdx = 0;

    private UnlimitedArray<Car> _cars = [
        ..GameSparker.cars,
        ..GameSparker.vendor_cars,
        ..GameSparker.user_cars,
    ];

    private UnlimitedArray<GarageDynamicStatBar> statBars = [];

    private PerspectiveCamera _camera = new();
    public GaragePhase(GraphicsDevice graphicsDevice, Car currentCar) : this(graphicsDevice)
    {
        _selectedCarIdx = _cars.FindIndex(c =>
        {
            ArgumentNullException.ThrowIfNull(c);
            return c.FileName == currentCar.FileName;
        });

        if (_selectedCarIdx == -1) _selectedCarIdx = 0;
    }

    private void CreateScene()
    {
        _garageScene = new Scene(
            graphicsDevice,
            [_cars[_selectedCarIdx]],
            _camera,
            []
        );

        // create and position stat bars
        Car car = _cars[_selectedCarIdx];
        float switsLevel = (car.Stats.Swits[2] - 220) / 90f;
        statBars[0] = new GarageDynamicStatBar(Math.Max(0.2f, switsLevel), 100, 200, "Top Speed");

        float accel = car.Stats.Acelf.X * car.Stats.Acelf.Y * car.Stats.Acelf.Z * (float)car.Stats.Grip / 7700f;
        statBars[1] = new GarageDynamicStatBar(accel, 230, 200, "Acceleration");

        statBars[2] = new GarageDynamicStatBar((float)car.Stats.Dishandle, 360, 200, "Handling");

        float airs = (car.Stats.Airc * (float)car.Stats.Airs * (float)car.Stats.Bounce + 28f) / 139f;
        statBars[3] = new GarageDynamicStatBar(airs, 100, 300, "Stunting");

        float strength = ((float)car.Stats.Moment + 0.5f) / 2.6f;
        statBars[4] = new GarageDynamicStatBar(strength, 230, 300, "Strength");

        statBars[5] = new GarageDynamicStatBar((float)car.Stats.Outdam, 360, 300, "Endurance");
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

        Car car = _cars[_selectedCarIdx];
        car.Position = new Vector3(-500, -100 - car.GroundAt, 200);
        car.Rotation = Euler.Identity;
        car.Rotation = new Euler(AngleSingle.FromDegrees(-30), car.Rotation.Pitch, car.Rotation.Roll);
        _camera.LookAt = new Vector3(0, 0, 0);
        _camera.Position = new Vector3(-600, -300, 1000);

        _garageScene.Render(false);
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        G.SetFont(new Font("Arial", 1, 48));
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringAligned(_cars[_selectedCarIdx].Stats.Name, graphicsDevice.Viewport.Width, 120, TextHorizontalAlignment.Center);

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

        CreateScene();
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
        CarSelectionCancelled.Invoke(this, new EventArgs());
    }

    private void CycleCarRight()
    {
        _selectedCarIdx += 1;
        if (_selectedCarIdx >= _cars.Count) _selectedCarIdx -= _cars.Count;
        CreateScene();
    }

    private void CycleCarLeft()
    {
        _selectedCarIdx -= 1;
        if (_selectedCarIdx < 0) _selectedCarIdx = _cars.Count - 1;
        CreateScene();
    }
}