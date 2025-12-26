using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;
using SoftFloat;
using Stride.Core.Mathematics;

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

    internal static void LoadTrackers(IReadOnlyList<GameObject> elements, int sx, int ncx, int sz, int ncz)
    {
        foreach (var element in elements)
        {
            if (element is CollisionObject obj)
                LoadTracker(obj);
        }
        
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

    internal static void LoadTracker(CollisionObject element)
    {
        var xz = (int)fix64.Round(element.Rotation.Xz.DegreesSFloat);
        for (var i = 0; i < element.Boxes.Length; i++)
        {
            Xy[Nt] = (int) fix64.Round(element.Boxes[i].Xy * Mad.Cos(xz) - element.Boxes[i].Zy * Mad.Sin(xz));
            Zy[Nt] = (int) fix64.Round(element.Boxes[i].Zy * Mad.Cos(xz) + element.Boxes[i].Xy * Mad.Sin(xz));
            for (var c = 0; c < 3; c++)
            {
                C[Nt][c] = (int) (element.Boxes[i].Color[c] + element.Boxes[i].Color[c] * (World.Snap[c] / 100.0F));
                if (C[Nt][c] > 255)
                {
                    C[Nt][c] = 255;
                }
                if (C[Nt][c] < 0)
                {
                    C[Nt][c] = 0;
                }
            }
            X[Nt] = (int) fix64.Round((fix64)element.Position.X + element.Boxes[i].Translation.X * Mad.Cos(xz) - element.Boxes[i].Translation.Z * Mad.Sin(xz));
            Z[Nt] = (int) fix64.Round((fix64)element.Position.Z + element.Boxes[i].Translation.Z * Mad.Cos(xz) + element.Boxes[i].Translation.X * Mad.Sin(xz));
            Y[Nt] = (int) fix64.Round((fix64)element.Position.Y + element.Boxes[i].Translation.Y);
            Skd[Nt] = element.Boxes[i].Skid;
            Dam[Nt] = element.Boxes[i].Damage;
            Notwall[Nt] = element.Boxes[i].NotWall;
            Decor[Nt] = false;
            var xzAbs = Math.Abs(xz);
            if (xzAbs == 180)
            {
                xzAbs = 0;
            }
            Radx[Nt] = (int) fix64.Round(fix64.Abs(element.Boxes[i].Radius.X * Mad.Cos(xzAbs) + element.Boxes[i].Radius.Z * Mad.Sin(xzAbs)));
            Radz[Nt] = (int) fix64.Round(fix64.Abs(element.Boxes[i].Radius.X * Mad.Sin(xzAbs) + element.Boxes[i].Radius.Z * Mad.Cos(xzAbs)));
            Rady[Nt] = (int) fix64.Round(element.Boxes[i].Radius.Y);
            Nt++;
        }
    }
}