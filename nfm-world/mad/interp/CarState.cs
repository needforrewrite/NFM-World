namespace NFMWorld.Mad.Interp;

public class CarState
{
    public int X;
    public int Y;
    public int Z;
    public float Xz;
    public float Xy;
    public float Zy;

    public CarState(ContO conto)
    {
        X = conto.X;
        Y = conto.Y;
        Z = conto.Z;
        Xz = conto.Xz;
        Xy = conto.Xy;
        Zy = conto.Zy;
    }

    public CarState()
    {
        X = 0;
        Y = 0;
        Z = 0;
        Xz = 0f;
        Xy = 0f;
        Zy = 0f;
    }

    public void Apply(ContO conto)
    {
        conto.X = X;
        conto.Y = Y;
        conto.Z = Z;
        conto.Xz = Xz;
        conto.Xy = Xy;
        conto.Zy = Zy;
    }


    /**
    *   <summary> Produces a new state which is the interpolated value between this (current) and a previous state. </summary>
    */
    public CarState InterpWith(CarState prev, float interp_ratio)
    {
        CarState interp = new CarState();

        interp.X = Interpolation.InterpolateCoord(X, prev.X, interp_ratio);
        interp.Y = Interpolation.InterpolateCoord(Y, prev.Y, interp_ratio);
        interp.Z = Interpolation.InterpolateCoord(Z, prev.Z, interp_ratio);

        interp.Xz = Interpolation.InterpolateAngle(Xz, prev.Xz, interp_ratio);
        interp.Xy = Interpolation.InterpolateAngle(Xy, prev.Xy, interp_ratio);
        interp.Zy = Interpolation.InterpolateAngle(Zy, prev.Zy, interp_ratio);

        return interp;
    }
}