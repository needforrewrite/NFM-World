using NFMWorld.Util;

namespace NFMWorld.Mad;

internal class Trackers
{
    //private Trackers() {}
    internal static readonly int[][] C = ArrayExt.New<int>(75000, 3);

    internal static readonly int[] Dam = new int[75000];
    internal static readonly bool[] Decor = new bool[75000];
    internal static int Ncx;
    internal static int Ncz;
    internal static readonly bool[] Notwall = new bool[75000];
    internal static int Nt = 0;
    internal static readonly int[] Radx = new int[75000];
    internal static readonly int[] Rady = new int[75000];
    internal static readonly int[] Radz = new int[75000];
    internal static int[,][] Sect = new int[1000, 1000][];
    internal static readonly int[] Skd = new int[75000];
    internal static int Sx;
    internal static int Sz;
    internal static readonly int[] X = new int[75000];
    internal static readonly int[] Xy = new int[75000];
    internal static readonly int[] Y = new int[75000];
    internal static readonly int[] Z = new int[75000];
    internal static readonly int[] Zy = new int[75000];

    internal static void Devidetrackers(int i, int i0, int i1, int i2)
    {
        Sect = null;
        Sx = i;
        Sz = i1;
        Ncx = i0 / 3000;
        if (Ncx <= 0)
        {
            Ncx = 1;
        }
        Ncz = i2 / 3000;
        if (Ncz <= 0)
        {
            Ncz = 1;
        }
        Sect = new int[Ncx, Ncz][];
        for (var i3 = 0; i3 < Ncx; i3++)
        {
            for (var i4 = 0; i4 < Ncz; i4++)
            {
                var i5 = Sx + i3 * 3000 + 1500;
                var i6 = Sz + i4 * 3000 + 1500;
                var ais = new int[6700];
                var i7 = 0;
                for (var i8 = 0; i8 < Nt; i8++)
                {
                    var i9 = Py(i5, X[i8], i6, Z[i8]);
                    if (i9 < 20250000 && i9 > 0 && Dam[i8] != 167)
                    {
                        ais[i7] = i8;
                        i7++;
                    }
                }
                if (i3 == 0 || i4 == 0 || i3 == Ncx - 1 || i4 == Ncz - 1)
                {
                    for (var i10 = 0; i10 < Nt; i10++)
                    {
                        if (Dam[i10] == 167)
                        {
                            ais[i7] = i10;
                            i7++;
                        }
                    }
                }
                if (i7 == 0)
                {
                    ais[i7] = 0;
                    i7++;
                }
                Sect[i3, i4] = new int[i7];
                Sys.ArrayCopy(ais, 0, Sect[i3, i4], 0, i7);
            }
        }
        for (var i12 = 0; i12 < Nt; i12++)
        {
            if (Dam[i12] == 167)
            {
                Dam[i12] = 1;
            }
        }

        Ncx--;
        Ncz--;
    }

    private static int Py(int i, int i13, int i14, int i15)
    {
        return (i - i13) * (i - i13) + (i14 - i15) * (i14 - i15);
    }
}