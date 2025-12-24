using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NFMWorld.Mad;

public class GaragePhase(GraphicsDevice graphicsDevice) : BasePhase
{
    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Returns the car that was selected.
    /// </summary>
    public event EventHandler<Car> CarSelected;

    /// <summary>
    /// This should be hooked onto by the calling phase, so that the calling phase can be restored upon car selection.
    /// Indicates no selection was made; retain existing car, if any.
    /// </summary>
    public event EventHandler CarSelectionCancelled;

    public virtual void GameTick()
    {
    }

    public virtual void Render()
    {
    }

    public virtual void RenderImgui()
    {
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
    }

    public virtual void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
    }
}