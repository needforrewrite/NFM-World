using NFMWorld.Util;

namespace NFMWorld.Mad;

internal class Trackers
{
    //private Trackers() {}
    internal static readonly int[][] C = ArrayExt.New<int>(75000, 3);

    internal static readonly UnlimitedArray<int> Dam = [];
    internal static readonly UnlimitedArray<bool> Decor = [];
    internal static int Ncx;
    internal static int Ncz;
    internal static readonly UnlimitedArray<bool> Notwall = [];
    internal static int Nt = 0;
    internal static readonly UnlimitedArray<int> Radx = [];
    internal static readonly UnlimitedArray<int> Rady = [];
    internal static readonly UnlimitedArray<int> Radz = [];
    internal static readonly UnlimitedArray<int> Skd = [];
    internal static int Sx;
    internal static int Sz;
    internal static readonly UnlimitedArray<int> X = [];
    internal static readonly UnlimitedArray<int> Xy = [];
    internal static readonly UnlimitedArray<int> Y = [];
    internal static readonly UnlimitedArray<int> Z = [];
    internal static readonly UnlimitedArray<int> Zy = [];

    internal static void LoadTrackers(IReadOnlyList<Mesh> elements, int sx, int ncx, int sz, int ncz)
    {
        foreach (var element in elements)
        {
            var i81 = (int)element.Rotation.Xz.Degrees;
            for (var i84 = 0; i84 < element.Boxes.Length; i84++)
            {
                Xy[Nt] = (int) (element.Boxes[i84].Xy * UMath.Cos(i81) - element.Boxes[i84].Zy * UMath.Sin(i81));
                Zy[Nt] = (int) (element.Boxes[i84].Zy * UMath.Cos(i81) + element.Boxes[i84].Xy * UMath.Sin(i81));
                for (var i85 = 0; i85 < 3; i85++)
                {
                    C[Nt][i85] = (int) (element.Boxes[i84].Color[i85] + element.Boxes[i84].Color[i85] * (World.Snap[i85] / 100.0F));
                    if (C[Nt][i85] > 255)
                    {
                        C[Nt][i85] = 255;
                    }
                    if (C[Nt][i85] < 0)
                    {
                        C[Nt][i85] = 0;
                    }
                }
                X[Nt] = (int) (element.Position.X + element.Boxes[i84].Translation.X * UMath.Cos(i81) - element.Boxes[i84].Translation.Z * UMath.Sin(i81));
                Z[Nt] = (int) (element.Position.Z + element.Boxes[i84].Translation.Z * UMath.Cos(i81) + element.Boxes[i84].Translation.X * UMath.Sin(i81));
                Y[Nt] = (int)(element.Position.Y + element.Boxes[i84].Translation.Y);
                Skd[Nt] = element.Boxes[i84].Skid;
                Dam[Nt] = element.Boxes[i84].Damage;
                Notwall[Nt] = element.Boxes[i84].NotWall;
                Decor[Nt] = false;
                var i86 = Math.Abs(i81);
                if (i86 == 180)
                {
                    i86 = 0;
                }
                Radx[Nt] = (int) Math.Abs(element.Boxes[i84].Radius.X * UMath.Cos(i86) + element.Boxes[i84].Radius.Z * UMath.Sin(i86));
                Radz[Nt] = (int) Math.Abs(element.Boxes[i84].Radius.X * UMath.Sin(i86) + element.Boxes[i84].Radius.Z * UMath.Cos(i86));
                Rady[Nt] = (int) element.Boxes[i84].Radius.Y;
                Nt++;
            }
        }
        
        Console.WriteLine(Nt);
        
        Sx = sx;
        Sz = sz;
        Ncx = ncx / 3000;
        if (Ncx <= 0)
        {
            Ncx = 1;
        }
        Ncz = ncz / 3000;
        if (Ncz <= 0)
        {
            Ncz = 1;
        }
        
        // maxine: remove trackers.sect which was assigned here. it's not used anymore.
        
        for (var i = 0; i < Nt; i++)
        {
            if (Dam[i] == 167)
            {
                Dam[i] = 1;
            }
        }

        Ncx--;
        Ncz--;
    }
}