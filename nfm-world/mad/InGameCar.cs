using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class InGameCar : IRenderable
{
    public Car CarRef;
    public Mad Mad;
    public Control Control;

    public CarStats Stats
    {
        get { return CarRef.Stats; }
    }

    public InGameCar(int im, Car car, int x, int z)
    {
        CarRef = car;
        Mad = new Mad(car.Stats, im);
        Mad.Reseto(im, CarRef);
        Control = new Control();
    }

    public void Drive()
    {
        CarRef.GameTick();
        Mad.Drive(Control, CarRef);
    }

    public void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        CarRef.Render(camera, lightCamera, isCreateShadowMap);
    }
}