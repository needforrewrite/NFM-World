using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class InGameCar : IRenderable
{
    public Car CarRef;
    public Mad Mad;
    public Control Control;
    public MadSfx Sfx;

    public CarStats Stats
    {
        get { return CarRef.Stats; }
    }

    public InGameCar(int im, Car car, int x, int z)
    {
        CarRef = new Car(car, new Vector3(0f, 250f, 0f), Euler.Identity);
        Mad = new Mad(car.Stats, im);
        Mad.Reseto(im, CarRef);
        Control = new Control();
        Sfx = new MadSfx(Mad);
    }

    public void Drive()
    {
        CarRef.GameTick();
        Mad.Drive(Control, CarRef);
        Sfx.Tick();
    }

    public void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        CarRef.Render(camera, lightCamera, isCreateShadowMap);
    }
}