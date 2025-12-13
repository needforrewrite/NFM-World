using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Car
{
    public Mesh Conto;
    public Mad Mad;
    public Stat Stat;
    public Control Control;

    public Car(Stat stat, int im, Mesh carConto, int x, int z)
    {
        Conto = new Mesh(carConto, new Vector3(x, World.Ground, z), Euler.Identity);
        Mad = new Mad(stat, im);
        Stat = stat;
        Mad.Reseto(im, Conto);
        Control = new Control();
    }

    public void Drive()
    {
        Mad.Drive(Control, Conto);
    }

    public void Render(Camera camera, bool isCreateShadowMap = false)
    {
        Conto.Render(camera, isCreateShadowMap);
    }
}