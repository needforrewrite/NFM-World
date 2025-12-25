using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class InGameCar : GameObject
{
    public Car CarRef;
    public Mad Mad;
    public Control Control;
    public MadSfx Sfx;

    public CarStats Stats => CarRef.Stats;

    public InGameCar(InGameCar copy, int im, bool isClientPlayer)
    {
        CarRef = new Car(copy.CarRef.Mesh, new Vector3(0f, World.Ground - copy.CarRef.GroundAt, 0f), Euler.Identity)
        {
            Parent = this
        };
        Mad = new Mad(copy.Stats, im, isClientPlayer);
        Mad.Reseto(im, CarRef);
        Control = new Control();
        Sfx = new MadSfx(Mad);
    }

    public InGameCar(int im, Mesh mesh, int x, int z, bool isClientPlayer)
    {
        CarRef = new Car(mesh, new Vector3(x, World.Ground - mesh.GroundAt, z), Euler.Identity)
        {
            Parent = this
        };
        Mad = new Mad(CarRef.Stats, im, isClientPlayer);
        Mad.Reseto(im, CarRef);
        Control = new Control();
        Sfx = new MadSfx(Mad);
    }

    public void Drive()
    {
        CarRef.GameTick();
        Mad.Drive(Control, CarRef);
        Sfx.Tick(Control, Mad, CarRef.Stats);
    }
    
    public void Collide(InGameCar otherCar)
    {
        Mad.Colide(CarRef, otherCar.Mad, otherCar.CarRef);
    }

    public override void OnBeforeRender()
    {
        base.OnBeforeRender();
        CarRef.OnBeforeRender();
    }

    public override IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        foreach (var renderData in base.GetRenderData(lighting))
        {
            yield return renderData;
        }
        foreach (var renderData in CarRef.GetRenderData(lighting))
        {
            yield return renderData;
        }
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        base.Render(camera, lighting);
        CarRef.Render(camera, lighting);
    }

    public void ResetPosition()
    {
        Mad.Reseto(Mad.Im, CarRef);
        CarRef.Position = new Vector3(0f, World.Ground - CarRef.GroundAt, 0f);
        CarRef.Rotation = Euler.Identity;
    }
}