using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld;

public class AroundCamera
{
    public float MaxHeight = 600f;
    public float MinHeight = 400f;

    private bool _rising = true;

    public float CurrentXz = 0f;
    public float Distance = 1000f;
    public float Height = 500f;
    

    // On each tick change camera position to be around the object
    public void AroundConstantHeight(PerspectiveCamera camera, Transform obj, float xzspeed = 1f)
    {
        CurrentXz += xzspeed * Physics.PHYSICS_MULTIPLIER;
        if (CurrentXz >= 360)
        {
            CurrentXz -= 360;
        }

        SinCosFloat sincos = new SinCosFloat(CurrentXz);
        float camX = (float)obj.Position.X - Distance * sincos.Cos;
        float camY = (float)obj.Position.Y - Height;
        float camZ = (float)obj.Position.Z - Distance * sincos.Sin;

        camera.Position = new Vector3(camX, camY, camZ);
        camera.LookAt = (Vector3)obj.Position;
    }

    public void Around(PerspectiveCamera camera, ITransform obj, float xzspeed = 1f, float yspeed = 2f)
    {
        CurrentXz += xzspeed * Physics.PHYSICS_MULTIPLIER;
        if (CurrentXz >= 360)
        {
            CurrentXz -= 360;
        }
        
        if (_rising)
        {
            Height += yspeed * Physics.PHYSICS_MULTIPLIER;
            if (Height >= MaxHeight)
            {
                _rising = false;
            }
        }
        else
        {
            Height -= yspeed * Physics.PHYSICS_MULTIPLIER;
            if (Height <= MinHeight)
            {
                _rising = true;
            }
        }

        SinCosFloat sincos = new SinCosFloat(CurrentXz);
        float camX = (float)obj.Position.X - Distance * sincos.Cos;
        float camY = (float)obj.Position.Y - Height;
        float camZ = (float)obj.Position.Z - Distance * sincos.Sin;

        camera.Position = new Vector3(camX, camY, camZ);
        camera.LookAt = (Vector3)obj.Position;
    }
}