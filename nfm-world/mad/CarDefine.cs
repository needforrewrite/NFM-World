using System;
using System.Text;

namespace NFMWorld.Mad;

class CarDefine
{
    public static readonly int ThirtySix = 36; // MAX CUSTOM CARS - NCARS
    public static readonly int Sixteen = 16; // NCARS
    public static readonly int FiftySix = 56; // NCARS + MAX CUSTOM CARS
    public static readonly int Forty = 40; // MAX CUSTOM CARS LOAD

    public static DevConsoleWriter Writer = null!;

    internal static readonly int[,] Swits =
    {
        {
            50, 185, 282
        },
        {
            100, 200, 310
        },
        {
            60, 180, 275
        },
        {
            76, 195, 298
        },
        {
            70, 170, 275
        },
        {
            70, 202, 293
        },
        {
            60, 170, 289
        },
        {
            70, 206, 291
        },
        {
            90, 210, 295
        },
        {
            90, 190, 276
        },
        {
            70, 200, 295
        },
        {
            50, 160, 270
        },
        {
            90, 200, 305
        },
        {
            50, 130, 210
        },
        {
            80, 200, 300
        },
        {
            70, 210, 290
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        },
        {
            0, 0, 0
        }
    };

    internal static readonly float[,] Acelf =
    {
        {
            11.0F, 5.0F, 3.0F
        },
        {
            14.0F, 7.0F, 5.0F
        },
        {
            10.0F, 5.0F, 3.5F
        },
        {
            11.0F, 6.0F, 3.5F
        },
        {
            10.0F, 5.0F, 3.5F
        },
        {
            12.0F, 6.0F, 3.0F
        },
        {
            7.0F, 9.0F, 4.0F
        },
        {
            11.0F, 5.0F, 3.0F
        },
        {
            12.0F, 7.0F, 4.0F
        },
        {
            12.0F, 7.0F, 3.5F
        },
        {
            11.5F, 6.5F, 3.5F
        },
        {
            9.0F, 5.0F, 3.0F
        },
        {
            13.0F, 7.0F, 4.5F
        },
        {
            7.5F, 3.5F, 3.0F
        },
        {
            11.0F, 7.5F, 4.0F
        },
        {
            12.0F, 6.0F, 3.5F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        },
        {
            0.0F, 0.0F, 0.0F
        }
    };

    internal static readonly int[] Handb =
    {
        7, 10, 7, 15, 12, 8, 9, 10, 5, 7, 8, 10, 8, 12, 7, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly float[] Airs =
    {
        1.0F, 1.2F, 0.95F, 1.0F, 2.2F, 1.0F, 0.9F, 0.8F, 1.0F, 0.9F, 1.15F, 0.8F, 1.0F, 0.3F, 1.3F, 1.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F
    };

    internal static readonly int[] Airc =
    {
        70, 30, 40, 40, 30, 50, 40, 90, 40, 50, 75, 10, 50, 0, 100, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Turn =
    {
        6, 9, 5, 7, 8, 7, 5, 5, 9, 7, 7, 4, 6, 5, 7, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly float[] Grip =
    {
        20.0F, 27.0F, 18.0F, 22.0F, 19.0F, 20.0F, 25.0F, 20.0F, 19.0F, 24.0F, 22.5F, 25.0F, 30.0F, 27.0F, 25.0F,
        27.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F
    };

    internal static readonly float[] Bounce =
    {
        1.2F, 1.05F, 1.3F, 1.15F, 1.3F, 1.2F, 1.15F, 1.1F, 1.2F, 1.1F, 1.15F, 0.8F, 1.05F, 0.8F, 1.1F, 1.15F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F
    };

    internal static readonly float[] Simag =
    {
        0.9F, 0.85F, 1.05F, 0.9F, 0.85F, 0.9F, 1.05F, 0.9F, 1.0F, 1.05F, 0.9F, 1.1F, 0.9F, 1.3F, 0.9F, 1.15F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F
    };

    internal static readonly float[] Moment =
    {
        1.3F, 0.75F, 1.4F, 1.2F, 1.1F, 1.38F, 1.43F, 1.48F, 1.35F, 1.7F, 1.42F, 2.0F, 1.26F, 3.0F, 1.5F, 2.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F
    };

    internal static readonly float[] Comprad =
    {
        0.5F, 0.4F, 0.8F, 0.5F, 0.4F, 0.5F, 0.5F, 0.5F, 0.5F, 0.8F, 0.5F, 1.5F, 0.5F, 0.8F, 0.5F, 0.8F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F
    };

    internal static readonly int[] Push =
    {
        2, 2, 3, 3, 2, 2, 2, 4, 2, 2, 2, 4, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Revpush =
    {
        2, 3, 2, 2, 2, 2, 2, 1, 2, 1, 2, 1, 2, 2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Lift =
    {
        0, 30, 0, 20, 0, 30, 0, 0, 20, 0, 0, 0, 10, 0, 30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Revlift =
    {
        0, 0, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Powerloss =
    {
        2500000, 2500000, 3500000, 2500000, 4000000, 2500000, 3200000, 3200000, 2750000, 5500000, 2750000, 4500000,
        3500000, 16700000, 3000000, 5500000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Flipy =
    {
        -50, -60, -92, -44, -60, -57, -54, -60, -77, -57, -82, -85, -28, -100, -63, -127, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Msquash =
    {
        7, 4, 7, 2, 8, 4, 6, 4, 3, 8, 4, 10, 3, 20, 3, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly int[] Clrad =
    {
        3300, 1700, 4700, 3000, 2000, 4500, 3500, 5000, 10000, 15000, 4000, 7000, 10000, 15000, 5500, 5000, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0
    };

    internal static readonly float[] Dammult =
    {
        0.75F, 0.8F, 0.45F, 0.8F, 0.42F, 0.7F, 0.72F, 0.6F, 0.58F, 0.41F, 0.67F, 0.45F, 0.61F, 0.25F, 0.38F, 0.52F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F
    };

    internal static readonly int[] Maxmag =
    {
        7600, 4200, 7200, 6000, 6000, 15000, 17200, 17000, 18000, 11000, 19000, 10700, 13000, 45000, 5800, 18000, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0
    };

    internal static readonly float[] Dishandle =
    {
        0.65F, 0.6F, 0.55F, 0.77F, 0.62F, 0.9F, 0.6F, 0.72F, 0.45F, 0.8F, 0.95F, 0.4F, 0.87F, 0.42F, 1.0F, 0.95F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F
    };

    internal static readonly float[] Outdam =
    {
        0.68F, 0.35F, 0.8F, 0.5F, 0.42F, 0.76F, 0.82F, 0.76F, 0.72F, 0.62F, 0.79F, 0.95F, 0.77F, 1.0F, 0.85F, 1.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F,
        0.0F, 0.0F, 0.0F, 0.0F
    };

    internal static readonly int[] Cclass =
    {
        0, 0, 0, 0, 0, 1, 2, 2, 2, 2, 3, 4, 4, 4, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static readonly string[] Names =
    {
        "Tornado Shark", "Formula 7", "Wow Caninaro", "La Vita Crab", "Nimi", "MAX Revenge", "Lead Oxide",
        "Kool Kat", "Drifter X", "Sword of Justice", "High Rider", "EL KING", "Mighty Eight", "M A S H E E N",
        "Radical One", "DR Monstaa", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "",
        "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ""
    };

    internal static readonly int[] Enginsignature =
    {
        0, 1, 2, 1, 0, 3, 2, 2, 1, 0, 3, 4, 1, 4, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static int Lastload = 0;
    private static int _nlcars = 0;
    internal static int Nlocars = 0;
    private static int _xnlocars = 0;
    internal static readonly bool[] Include = new bool[Forty];
    internal static readonly string[] Createdby = new string[Forty];
    internal static readonly int[] Publish = new int[Forty];
    internal static readonly string[] Loadnames = new string[20];
    internal static int Nl = 0;
    internal static int Action = 0;
    private static bool _carlon = false;
    internal static int Reco = -2;

    internal static readonly int[] Lcardate =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static int Haltload = 0;
    internal static int Ac = -1;
    private static string _acname = "Radical One";
    private static string _fails = "";
    internal static string Tnickey = "";
    internal static string Tclan = "";
    internal static string Tclankey = "";

    private static readonly int[] Adds =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    internal static string Viewname = "";
    internal static int Staction = 0;
    internal static string Onstage = "";
    private static int _roundslot = 0;
    internal static string Lastcar = "";
    internal static int Msloaded = 0;
    private static CarDefine _thread;
    internal static readonly int[] Top20Adds = new int[20];

    private CarDefine()
    {
    }

    private static void Loadstat(byte[] _is, string str, int i, int i0, int i1, int i2)
    {
        Names[i2] = str;
        var abool = false;
        var bool3 = false;
        int[] statValues =
        {
            128, 128, 128, 128, 128
        };
        var i6 = 640;
        int[] physicsValues =
        {
            50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50
        };
        int[] is8 =
        {
            50, 50, 50
        };
        Enginsignature[i2] = 0;
        var f = 0.0F;
        Publish[i2 - Sixteen] = 0;
        Createdby[i2 - Sixteen] = "Unkown User";
        try
        {
            foreach (var aline in Encoding.Default.GetString(_is).Split('\n'))
            {
                var line = aline.Trim();
                if (line.StartsWith("stat("))
                {
                    try
                    {
                        i6 = 0;
                        for (var i9 = 0; i9 < 5; i9++)
                        {
                            statValues[i9] = Getvalue("stat", line, i9);
                            if (statValues[i9] > 200)
                            {
                                statValues[i9] = 200;
                            }
                            if (statValues[i9] < 16)
                            {
                                statValues[i9] = 16;
                            }
                            i6 += statValues[i9];
                        }
                        abool = true;
                    }
                    catch (Exception exception)
                    {
                        abool = false;
                    }
                }
                if (line.StartsWith("physics("))
                {
                    try
                    {
                        for (var i10 = 0; i10 < 11; i10++)
                        {
                            physicsValues[i10] = Getvalue("physics", line, i10);
                            if (physicsValues[i10] > 100)
                            {
                                physicsValues[i10] = 100;
                            }
                            if (physicsValues[i10] < 0)
                            {
                                physicsValues[i10] = 0;
                            }
                        }
                        for (var i11 = 0; i11 < 3; i11++)
                        {
                            is8[i11] = Getvalue("physics", line, i11 + 11);
                            if (i11 != 0 && is8[i11] > 100)
                            {
                                is8[i11] = 100;
                            }
                            if (is8[i11] < 0)
                            {
                                is8[i11] = 0;
                            }
                        }
                        Enginsignature[i2] = Getvalue("physics", line, 14);
                        if (Enginsignature[i2] > 4)
                        {
                            Enginsignature[i2] = 0;
                        }
                        if (Enginsignature[i2] < 0)
                        {
                            Enginsignature[i2] = 0;
                        }
                        f = Getvalue("physics", line, 15);
                        if (f > 0.0F)
                        {
                            bool3 = true;
                        }
                    }
                    catch (Exception exception)
                    {
                        bool3 = false;
                    }
                }
                if (line.StartsWith("handling("))
                {
                    try
                    {
                        var i12 = Getvalue("handling", line, 0);
                        if (i12 > 200)
                        {
                            i12 = 200;
                        }
                        if (i12 < 50)
                        {
                            i12 = 50;
                        }
                        Dishandle[i2] = i12 / 200.0F;
                    }
                    catch (Exception ignored)
                    {
                        Writer.WriteLine(ignored);
                    }
                }
                if (line.StartsWith("carmaker("))
                {
                    Createdby[i2 - Sixteen] = GetSvalue("carmaker", line, 0);
                }
                if (line.StartsWith("publish("))
                {
                    Publish[i2 - Sixteen] = Getvalue("publish", line, 0);
                }
            }
        }
        catch (Exception exception)
        {
            Writer.WriteLine("Error Loading Car Stat: " + exception);
        }
        if (abool && bool3)
        {
            var i13 = 0;
            if (i6 > 680)
            {
                i13 = 680 - i6;
            }
            if (i6 > 640 && i6 < 680)
            {
                i13 = 640 - i6;
            }
            if (i6 > 600 && i6 < 640)
            {
                i13 = 600 - i6;
            }
            if (i6 > 560 && i6 < 600)
            {
                i13 = 560 - i6;
            }
            if (i6 > 520 && i6 < 560)
            {
                i13 = 520 - i6;
            }
            if (i6 < 520)
            {
                i13 = 520 - i6;
            }
            while (i13 != 0)
            {
                for (var i14 = 0; i14 < 5; i14++)
                {
                    if (i13 > 0 && statValues[i14] < 200)
                    {
                        statValues[i14]++;
                        i13--;
                    }
                    if (i13 < 0 && statValues[i14] > 16)
                    {
                        statValues[i14]--;
                        i13++;
                    }
                }
            }
            i6 = 0;
            for (var i15 = 0; i15 < 5; i15++)
            {
                i6 += statValues[i15];
            }
            if (i6 == 520)
            {
                Cclass[i2] = 0;
            }
            if (i6 == 560)
            {
                Cclass[i2] = 1;
            }
            if (i6 == 600)
            {
                Cclass[i2] = 2;
            }
            if (i6 == 640)
            {
                Cclass[i2] = 3;
            }
            if (i6 == 680)
            {
                Cclass[i2] = 4;
            }
            var i16 = 0;
            var i17 = 0;
            var f18 = 0.5F;
            if (statValues[0] == 200)
            {
                i16 = 1;
                i17 = 1;
            }
            if (statValues[0] > 192 && statValues[0] < 200)
            {
                i16 = 12;
                i17 = 1;
                f18 = (statValues[0] - 192) / 8.0F;
            }
            if (statValues[0] == 192)
            {
                i16 = 12;
                i17 = 12;
            }
            if (statValues[0] > 148 && statValues[0] < 192)
            {
                i16 = 14;
                i17 = 12;
                f18 = (statValues[0] - 148) / 44.0F;
            }
            if (statValues[0] == 148)
            {
                i16 = 14;
                i17 = 14;
            }
            if (statValues[0] > 133 && statValues[0] < 148)
            {
                i16 = 10;
                i17 = 14;
                f18 = (statValues[0] - 133) / 15.0F;
            }
            if (statValues[0] == 133)
            {
                i16 = 10;
                i17 = 10;
            }
            if (statValues[0] > 112 && statValues[0] < 133)
            {
                i16 = 15;
                i17 = 10;
                f18 = (statValues[0] - 112) / 21.0F;
            }
            if (statValues[0] == 112)
            {
                i16 = 15;
                i17 = 15;
            }
            if (statValues[0] > 107 && statValues[0] < 112)
            {
                i16 = 11;
                i17 = 15;
                f18 = (statValues[0] - 107) / 5.0F;
            }
            if (statValues[0] == 107)
            {
                i16 = 11;
                i17 = 11;
            }
            if (statValues[0] > 88 && statValues[0] < 107)
            {
                i16 = 13;
                i17 = 11;
                f18 = (statValues[0] - 88) / 19.0F;
            }
            if (statValues[0] == 88)
            {
                i16 = 13;
                i17 = 13;
            }
            if (statValues[0] > 88)
            {
                Swits[i2, 0] = (int) ((Swits[i17, 0] - Swits[i16, 0]) * f18 + Swits[i16, 0]);
                Swits[i2, 1] = (int) ((Swits[i17, 1] - Swits[i16, 1]) * f18 + Swits[i16, 1]);
                Swits[i2, 2] = (int) ((Swits[i17, 2] - Swits[i16, 2]) * f18 + Swits[i16, 2]);
            }
            else
            {
                f18 = statValues[0] / 88.0F;
                if (f18 < 0.76)
                {
                    f18 = 0.76F;
                }
                Swits[i2, 0] = (int) (50.0F * f18);
                Swits[i2, 1] = (int) (130.0F * f18);
                Swits[i2, 2] = (int) (210.0F * f18);
            }
            i16 = 0;
            i17 = 0;
            f18 = 0.5F;
            if (statValues[1] == 200)
            {
                i16 = 1;
                i17 = 1;
            }
            if (statValues[1] > 150 && statValues[1] < 200)
            {
                i16 = 14;
                i17 = 1;
                f18 = (statValues[1] - 150) / 50.0F;
            }
            if (statValues[1] == 150)
            {
                i16 = 14;
                i17 = 14;
            }
            if (statValues[1] > 144 && statValues[1] < 150)
            {
                i16 = 9;
                i17 = 14;
                f18 = (statValues[1] - 144) / 6.0F;
            }
            if (statValues[1] == 144)
            {
                i16 = 9;
                i17 = 9;
            }
            if (statValues[1] > 139 && statValues[1] < 144)
            {
                i16 = 6;
                i17 = 9;
                f18 = (statValues[1] - 139) / 5.0F;
            }
            if (statValues[1] == 139)
            {
                i16 = 6;
                i17 = 6;
            }
            if (statValues[1] > 128 && statValues[1] < 139)
            {
                i16 = 15;
                i17 = 6;
                f18 = (statValues[1] - 128) / 11.0F;
            }
            if (statValues[1] == 128)
            {
                i16 = 15;
                i17 = 15;
            }
            if (statValues[1] > 122 && statValues[1] < 128)
            {
                i16 = 10;
                i17 = 15;
                f18 = (statValues[1] - 122) / 6.0F;
            }
            if (statValues[1] == 122)
            {
                i16 = 10;
                i17 = 10;
            }
            if (statValues[1] > 119 && statValues[1] < 122)
            {
                i16 = 3;
                i17 = 10;
                f18 = (statValues[1] - 119) / 3.0F;
            }
            if (statValues[1] == 119)
            {
                i16 = 3;
                i17 = 3;
            }
            if (statValues[1] > 98 && statValues[1] < 119)
            {
                i16 = 5;
                i17 = 3;
                f18 = (statValues[1] - 98) / 21.0F;
            }
            if (statValues[1] == 98)
            {
                i16 = 5;
                i17 = 5;
            }
            if (statValues[1] > 81 && statValues[1] < 98)
            {
                i16 = 0;
                i17 = 5;
                f18 = (statValues[1] - 81) / 17.0F;
            }
            if (statValues[1] == 81)
            {
                i16 = 0;
                i17 = 0;
            }
            if (statValues[1] <= 80)
            {
                i16 = 2;
                i17 = 2;
            }
            if (statValues[0] <= 88)
            {
                i16 = 13;
                i17 = 13;
            }
            Acelf[i2, 0] = (Acelf[i17, 0] - Acelf[i16, 0]) * f18 + Acelf[i16, 0];
            Acelf[i2, 1] = (Acelf[i17, 1] - Acelf[i16, 1]) * f18 + Acelf[i16, 1];
            Acelf[i2, 2] = (Acelf[i17, 2] - Acelf[i16, 2]) * f18 + Acelf[i16, 2];
            if (statValues[1] <= 70 && statValues[0] > 88)
            {
                Acelf[i2, 0] = 9.0F;
                Acelf[i2, 1] = 4.0F;
                Acelf[i2, 2] = 3.0F;
            }
            f18 = (statValues[2] - 88) / 109.0F;
            if (f18 > 1.0F)
            {
                f18 = 1.0F;
            }
            if (f18 < -0.55)
            {
                f18 = -0.55F;
            }
            Airs[i2] = 0.55F + 0.45F * f18 + 0.4F * (physicsValues[9] / 100.0F);
            if (Airs[i2] < 0.3)
            {
                Airs[i2] = 0.3F;
            }
            Airc[i2] = (int) (10.0F + 70.0F * f18 + 30.0F * (physicsValues[10] / 100.0F));
            if (Airc[i2] < 0)
            {
                Airc[i2] = 0;
            }
            var i19 = (int) (670.0F - (physicsValues[9] + physicsValues[10]) / 200.0F * 420.0F);
            if (statValues[0] <= 88)
            {
                i19 = (int) (1670.0F - (physicsValues[9] + physicsValues[10]) / 200.0F * 1420.0F);
            }
            if (statValues[2] > 190 && i19 < 300)
            {
                i19 = 300;
            }
            Powerloss[i2] = i19 * 10000;
            Moment[i2] = 0.7F + (statValues[3] - 16) / 184.0F * 1.0F;
            if (statValues[0] < 110)
            {
                Moment[i2] = 0.75F + (statValues[3] - 16) / 184.0F * 1.25F;
            }
            if (statValues[3] == 200 && statValues[4] == 200 && statValues[0] <= 88)
            {
                Moment[i2] = 3.0F;
            }
            var f20 = 0.9F + (statValues[4] - 90) * 0.01F;
            if (f20 < 0.6)
            {
                f20 = 0.6F;
            }
            if (statValues[4] == 200 && statValues[0] <= 88)
            {
                f20 = 3.0F;
            }
            Maxmag[i2] = (int) (f * f20);
            Outdam[i2] = 0.35F + (f20 - 0.6F) * 0.5F;
            if (Outdam[i2] < 0.35)
            {
                Outdam[i2] = 0.35F;
            }
            if (Outdam[i2] > 1.0F)
            {
                Outdam[i2] = 1.0F;
            }
            Clrad[i2] = (int) (is8[0] * is8[0] * 1.5);
            if (Clrad[i2] < 1000)
            {
                Clrad[i2] = 1000;
            }
            Dammult[i2] = 0.3F + is8[1] * 0.005F;
            Msquash[i2] = (int) (2.0 + is8[2] / 7.6);
            Flipy[i2] = i0;
            Handb[i2] = (int) (7.0F + physicsValues[0] / 100.0F * 8.0F);
            Turn[i2] = (int) (4.0F + physicsValues[1] / 100.0F * 6.0F);
            Grip[i2] = 16.0F + physicsValues[2] / 100.0F * 14.0F;
            if (Grip[i2] < 21.0F)
            {
                Swits[i2, 0] += (int) (40.0F * ((21.0F - Grip[i2]) / 5.0F));
                if (Swits[i2, 0] > 100)
                {
                    Swits[i2, 0] = 100;
                }
            }
            Bounce[i2] = 0.8F + physicsValues[3] / 100.0F * 0.6F;
            if (physicsValues[3] > 67)
            {
                Airs[i2] *= 0.76F + (1.0F - physicsValues[3] / 100.0F) * 0.24F;
                Airc[i2] = (int) (Airc[i2] * 0.76F + (1.0F - physicsValues[3] / 100.0F) * 0.24F);
            }
            Lift[i2] = (int) (physicsValues[5] * (float) physicsValues[5] / 10000.0F * 30.0F);
            Revlift[i2] = (int) (physicsValues[6] / 100.0F * 32.0F);
            Push[i2] = (int) (2.0F + physicsValues[7] / 100.0F * 2.0F * ((30 - Lift[i2]) / 30f));
            Revpush[i2] = (int) (1.0F + physicsValues[8] / 100.0F * 2.0F);
            Comprad[i2] = i / 400.0F + (statValues[3] - 16) / 184.0F * 0.2F;
            if (Comprad[i2] < 0.4)
            {
                Comprad[i2] = 0.4F;
            }
            Simag[i2] = (i1 - 17) * 0.0167F + 0.85F;
        }
        else
        {
            Names[i2] = "";
        }
    }

    private static int Getvalue(string str, string string21, int i)
    {
        var i22 = 0;
        var string23 = "";
        for (var i24 = str.Length + 1; i24 < string21.Length; i24++)
        {
            var string25 = $"{string21[i24]}";
            if (string25.Equals(",") || string25.Equals(")"))
            {
                i22++;
                i24++;
            }
            if (i22 == i)
            {
                string23 = "" + string23 + string21[i24];
            }
        }
        return (int) float.Parse(string23);
    }

    private static string GetSvalue(string str, string string26, int i)
    {
        var string27 = "";
        var i28 = 0;
        for (var i29 = str.Length + 1; i29 < string26.Length && i28 <= i; i29++)
        {
            var string30 = $"{string26[i29]}";
            if (string30.Equals(",") || string30.Equals(")"))
            {
                i28++;
            }
            else if (i28 == i)
            {
                string27 = "" + string27 + string30;
            }
        }
        return string27;
    }
}