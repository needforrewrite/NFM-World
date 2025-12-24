using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using NFMWorld.Util;

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

    private int _selectedCarIdx = 0;

    private UnlimitedArray<Car> _cars = [
        ..GameSparker.vendor_cars,
        ..GameSparker.cars,
        ..GameSparker.user_cars,
    ];

    public GaragePhase(GraphicsDevice graphicsDevice, Car currentCar) : this(graphicsDevice)
    {
        _selectedCarIdx = _cars.FindIndex(c =>
        {
            ArgumentNullException.ThrowIfNull(c);
            return c.FileName == currentCar.FileName;
        });

        if (_selectedCarIdx == -1) _selectedCarIdx = 0;
    }

    public override void GameTick()
    {
    }

    public override void Render()
    {
    }

    public override void RenderImgui()
    {
        base.RenderImgui();
        
        G.SetColor(new Color(15, 0, 35));
        G.FillRect(0, 0, 1920, 1080);

        // Draw title
        G.SetFont(new Font("Arial", 1, 48));
        G.SetColor(new Color(255, 140, 0)); // Orange

        // Draw "NEED FOR MADNESS?" with styling similar to the image
        G.DrawString(_cars[_selectedCarIdx].Stats.Name, 90, 290);
    }

    public override void Enter()
    {
    }

    public override void Exit()
    {
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        if (key == Keys.Right)
        {
            CycleCarRight();
        } else if(key == Keys.Left)
        {
            CycleCarLeft();
        } else if(key == Keys.Enter)
        {
            SelectedCar();
        } else if(key == Keys.Escape)
        {
            SelectionCancelled();
        }
    }

    private void SelectedCar()
    {
        if(CarSelected == null) throw new ArgumentNullException("Attempted to invoke CarSelected, but it was null.");
        CarSelected.Invoke(this, _cars[_selectedCarIdx]);
    }

    private void SelectionCancelled()
    {
        if(CarSelectionCancelled == null) throw new ArgumentNullException("Attempted to invoke CarSelectionCancelled, but it was null.");
        CarSelectionCancelled.Invoke(this, new EventArgs());
    }

    private void CycleCarRight()
    {
        _selectedCarIdx += 1;
        if (_selectedCarIdx >= _cars.Count) _selectedCarIdx -= _cars.Count;
    }

    private void CycleCarLeft()
    {
        _selectedCarIdx -= 1;
        if (_selectedCarIdx < 0) _selectedCarIdx = _cars.Count - 1;
    }
}