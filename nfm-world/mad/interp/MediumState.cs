namespace NFMWorld.Mad.Interp;

public struct MediumState
{
    public int X;
    public int Y;
    public int Z;
    public int Xz;
    public int Zy;
    public int FocusPoint;
    public int Vxz;

    public MediumState()
    {
        X = Medium.X;
        Y = Medium.Y;
        Z = Medium.Z;
        Xz = Medium.Xz;
        Zy = Medium.Zy;
        FocusPoint = Medium.FocusPoint;
        Vxz = Medium.Vxz;
    }

    public void Apply()
    {
        Medium.X = X;
        Medium.Y = Y;
        Medium.Z = Z;
        Medium.Xz = Xz;
        Medium.Zy = Zy;
        Medium.FocusPoint = FocusPoint;
        Medium.Vxz = Vxz;
    }

    /**
    *   <summary> Produces a new state which is the interpolated value between this (current) and a previous state. </summary>
    */
    public MediumState InterpWith(MediumState prev, float interp_ratio)
    {
        MediumState interp = new MediumState();

        interp.X = Interpolation.InterpolateCoord(X, prev.X, interp_ratio);
        interp.Y = Interpolation.InterpolateCoord(Y, prev.Y, interp_ratio);
        interp.Z = Interpolation.InterpolateCoord(Z, prev.Z, interp_ratio);
        interp.FocusPoint = Interpolation.InterpolateCoord(FocusPoint, prev.FocusPoint, interp_ratio);

        interp.Xz = Interpolation.InterpolateAngle(Xz, prev.Xz, interp_ratio);
        interp.Zy = Interpolation.InterpolateAngle(Zy, prev.Zy, interp_ratio);
        interp.Vxz = Interpolation.InterpolateAngle(Vxz, prev.Vxz, interp_ratio);

        return interp;
    }
}