using System.Numerics;

namespace HoleyDiver.UnitTest;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    /// <summary>
    /// Test cases for triangulation. Input is an array of Vector3 representing the polygon with holes,
    /// and output is an array of triangles (each triangle is an array of 3 Vector3).
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<object> TriangulatorTestCases()
    {
        #region 2000tornados poly with holes

        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(34, -5, 17),
                new Vector3(34, -5, 4),
                new Vector3(34, -11, 4),
                new Vector3(34, -11, 17),
                new Vector3(34, -5, 17), // Returns to start - closes outer loop
                new Vector3(34, -5, 16),
                new Vector3(34, -11, 16),
                new Vector3(34, -11, 13),
                new Vector3(34, -5, 13),
                new Vector3(34, -5, 12),
                new Vector3(34, -11, 12),
                new Vector3(34, -11, 9),
                new Vector3(34, -5, 9),
                new Vector3(34, -5, 8),
                new Vector3(34, -11, 8),
                new Vector3(34, -11, 5),
                new Vector3(34, -5, 5),
                new Vector3(34, -5, 4),
                new Vector3(34, -11, 4),
                new Vector3(34, -5, 4)
            ],
            (Vector3[][])
            [
                [
                    new Vector3(34, -11, 4),
                    new Vector3(34, -5, 4),
                    new Vector3(34, -5, 5),
                ],
                [
                    new Vector3(34, -11, 4),
                    new Vector3(34, -5, 5),
                    new Vector3(34, -11, 5),
                ],
                [
                    new Vector3(34, -11, 8),
                    new Vector3(34, -5, 8),
                    new Vector3(34, -5, 9),
                ],
                [
                    new Vector3(34, -11, 8),
                    new Vector3(34, -5, 9),
                    new Vector3(34, -11, 9),
                ],
                [
                    new Vector3(34, -11, 12),
                    new Vector3(34, -5, 12),
                    new Vector3(34, -5, 13),
                ],
                [
                    new Vector3(34, -11, 12),
                    new Vector3(34, -5, 13),
                    new Vector3(34, -11, 13),
                ],
                [
                    new Vector3(34, -11, 16),
                    new Vector3(34, -5, 16),
                    new Vector3(34, -5, 17),
                ],
                [
                    new Vector3(34, -11, 16),
                    new Vector3(34, -5, 17),
                    new Vector3(34, -11, 17),
                ]
            ],
            new Vector3(1, 0, 0),
            1
        ];

        #endregion

        #region 2000tornados

        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, -10, 55),
                new Vector3(-34, -14, 0),
                new Vector3(-15, -14, 5),
                new Vector3(-15, -10, 52),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, -10, 55),
                    new Vector3(-15, -10, 52),
                    new Vector3(-15, -14, 5),
                ],

                [
                    new Vector3(-32, -10, 55),
                    new Vector3(-15, -14, 5),
                    new Vector3(-34, -14, 0),
                ],
            ],
            new Vector3(0.0051955315f, 0.9969531f, -0.07782953f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-15, -14, 5),
                new Vector3(-5, -14, 5),
                new Vector3(-5, -12, 52),
                new Vector3(-15, -10, 52),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-15, -14, 5),
                    new Vector3(-15, -10, 52),
                    new Vector3(-5, -12, 52),
                ],

                [
                    new Vector3(-15, -14, 5),
                    new Vector3(-5, -12, 52),
                    new Vector3(-5, -14, 5),
                ],
            ],
            new Vector3(0.099303626f, 0.99303627f, -0.06338529f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(32, -10, 55),
                new Vector3(34, -14, 0),
                new Vector3(15, -14, 5),
                new Vector3(15, -10, 52),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(15, -10, 52),
                    new Vector3(32, -10, 55),
                    new Vector3(34, -14, 0),
                ],

                [
                    new Vector3(15, -10, 52),
                    new Vector3(34, -14, 0),
                    new Vector3(15, -14, 5),
                ],
            ],
            new Vector3(-0.0051955315f, 0.9969531f, -0.07782953f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(15, -14, 5),
                new Vector3(5, -14, 5),
                new Vector3(5, -12, 52),
                new Vector3(15, -10, 52),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(15, -10, 52),
                    new Vector3(15, -14, 5),
                    new Vector3(5, -14, 5),
                ],

                [
                    new Vector3(15, -10, 52),
                    new Vector3(5, -14, 5),
                    new Vector3(5, -12, 52),
                ],
            ],
            new Vector3(-0.099303626f, 0.99303627f, -0.06338529f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-5, -14, 5),
                new Vector3(-5, -12, 52),
                new Vector3(5, -12, 52),
                new Vector3(5, -14, 5),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(5, -14, 5),
                    new Vector3(-5, -14, 5),
                    new Vector3(-5, -12, 52),
                ],

                [
                    new Vector3(5, -14, 5),
                    new Vector3(-5, -12, 52),
                    new Vector3(5, -12, 52),
                ],
            ],
            new Vector3(0f, 0.99909586f, -0.04251472f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-31, -14, 0),
                new Vector3(-34, -14, 0),
                new Vector3(-25, -26, -17),
                new Vector3(-22, -26, -17),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-31, -14, 0),
                    new Vector3(-22, -26, -17),
                    new Vector3(-25, -26, -17),
                ],

                [
                    new Vector3(-31, -14, 0),
                    new Vector3(-25, -26, -17),
                    new Vector3(-34, -14, 0),
                ],
            ],
            new Vector3(0f, 0.81696784f, -0.57668316f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(31, -14, 0),
                new Vector3(34, -14, 0),
                new Vector3(25, -26, -17),
                new Vector3(22, -26, -17),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(22, -26, -17),
                    new Vector3(31, -14, 0),
                    new Vector3(34, -14, 0),
                ],

                [
                    new Vector3(22, -26, -17),
                    new Vector3(34, -14, 0),
                    new Vector3(25, -26, -17),
                ],
            ],
            new Vector3(0f, 0.81696784f, -0.57668316f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-15, -14, 5),
                new Vector3(-31, -14, 0),
                new Vector3(-22, -26, -17),
                new Vector3(0, -26, -13),
                new Vector3(22, -26, -17),
                new Vector3(31, -14, 0),
                new Vector3(15, -14, 5),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-15, -14, 5),
                    new Vector3(15, -14, 5),
                    new Vector3(31, -14, 0),
                ],

                [
                    new Vector3(-15, -14, 5),
                    new Vector3(31, -14, 0),
                    new Vector3(22, -26, -17),
                ],

                [
                    new Vector3(-15, -14, 5),
                    new Vector3(22, -26, -17),
                    new Vector3(0, -26, -13),
                ],

                [
                    new Vector3(-15, -14, 5),
                    new Vector3(0, -26, -13),
                    new Vector3(-22, -26, -17),
                ],

                [
                    new Vector3(-15, -14, 5),
                    new Vector3(-22, -26, -17),
                    new Vector3(-31, -14, 0),
                ],
            ],
            new Vector3(2.4111844E-09f, 0.8493782f, -0.5277847f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-23, -26, -44),
                new Vector3(-25, -26, -17),
                new Vector3(0, -26, -13),
                new Vector3(25, -26, -17),
                new Vector3(23, -26, -44),
                new Vector3(18, -26, -49),
                new Vector3(0, -26, -52),
                new Vector3(-18, -26, -49),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-18, -26, -49),
                    new Vector3(-23, -26, -44),
                    new Vector3(-25, -26, -17),
                ],

                [
                    new Vector3(-18, -26, -49),
                    new Vector3(-25, -26, -17),
                    new Vector3(0, -26, -13),
                ],

                [
                    new Vector3(-18, -26, -49),
                    new Vector3(0, -26, -13),
                    new Vector3(25, -26, -17),
                ],

                [
                    new Vector3(-18, -26, -49),
                    new Vector3(25, -26, -17),
                    new Vector3(23, -26, -44),
                ],

                [
                    new Vector3(-18, -26, -49),
                    new Vector3(23, -26, -44),
                    new Vector3(18, -26, -49),
                ],

                [
                    new Vector3(-18, -26, -49),
                    new Vector3(18, -26, -49),
                    new Vector3(0, -26, -52),
                ],
            ],
            new Vector3(0f, 1f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-25, -26, -17),
                new Vector3(-25, -26, -22),
                new Vector3(-34, -14, -5),
                new Vector3(-34, -14, 0),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-34, -14, 0),
                    new Vector3(-25, -26, -17),
                    new Vector3(-25, -26, -22),
                ],

                [
                    new Vector3(-34, -14, 0),
                    new Vector3(-25, -26, -22),
                    new Vector3(-34, -14, -5),
                ],
            ],
            new Vector3(0.8f, 0.6f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(25, -26, -17),
                new Vector3(25, -26, -22),
                new Vector3(34, -14, -5),
                new Vector3(34, -14, 0),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(34, -14, 0),
                    new Vector3(25, -26, -17),
                    new Vector3(25, -26, -22),
                ],

                [
                    new Vector3(34, -14, 0),
                    new Vector3(25, -26, -22),
                    new Vector3(34, -14, -5),
                ],
            ],
            new Vector3(0.8f, -0.6f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-25, -26, -22),
                new Vector3(-23, -26, -44),
                new Vector3(-32, -14, -55),
                new Vector3(-34, -14, -5),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-34, -14, -5),
                    new Vector3(-25, -26, -22),
                    new Vector3(-23, -26, -44),
                ],

                [
                    new Vector3(-34, -14, -5),
                    new Vector3(-23, -26, -44),
                    new Vector3(-32, -14, -55),
                ],
            ],
            new Vector3(0.8040295f, 0.59332204f, 0.03880035f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(25, -26, -22),
                new Vector3(23, -26, -44),
                new Vector3(32, -14, -55),
                new Vector3(34, -14, -5),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(34, -14, -5),
                    new Vector3(25, -26, -22),
                    new Vector3(23, -26, -44),
                ],

                [
                    new Vector3(34, -14, -5),
                    new Vector3(23, -26, -44),
                    new Vector3(32, -14, -55),
                ],
            ],
            new Vector3(0.8040295f, -0.59332204f, -0.03880035f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-23, -26, -44),
                new Vector3(-18, -26, -49),
                new Vector3(-25, -14, -66),
                new Vector3(-32, -14, -55),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, -14, -55),
                    new Vector3(-23, -26, -44),
                    new Vector3(-18, -26, -49),
                ],

                [
                    new Vector3(-32, -14, -55),
                    new Vector3(-18, -26, -49),
                    new Vector3(-25, -14, -66),
                ],
            ],
            new Vector3(0.5146744f, 0.77478725f, 0.36717144f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(23, -26, -44),
                new Vector3(18, -26, -49),
                new Vector3(25, -14, -66),
                new Vector3(32, -14, -55),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(23, -26, -44),
                    new Vector3(32, -14, -55),
                    new Vector3(25, -14, -66),
                ],

                [
                    new Vector3(23, -26, -44),
                    new Vector3(25, -14, -66),
                    new Vector3(18, -26, -49),
                ],
            ],
            new Vector3(-0.5146744f, 0.77478725f, 0.36717144f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-25, -14, -66),
                new Vector3(-18, -26, -49),
                new Vector3(0, -26, -50),
                new Vector3(18, -26, -49),
                new Vector3(25, -14, -66),
                new Vector3(0, -14, -67),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(0, -14, -67),
                    new Vector3(-25, -14, -66),
                    new Vector3(-18, -26, -49),
                ],

                [
                    new Vector3(0, -14, -67),
                    new Vector3(-18, -26, -49),
                    new Vector3(0, -26, -50),
                ],

                [
                    new Vector3(0, -14, -67),
                    new Vector3(0, -26, -50),
                    new Vector3(18, -26, -49),
                ],

                [
                    new Vector3(0, -14, -67),
                    new Vector3(18, -26, -49),
                    new Vector3(25, -14, -66),
                ],
            ],
            new Vector3(-0f, 0.81780094f, 0.57550114f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, -14, -55),
                new Vector3(-35, -20, -110),
                new Vector3(-10, -14, -105),
                new Vector3(-10, -14, -67),
                new Vector3(-25, -14, -66),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-25, -14, -66),
                    new Vector3(-10, -14, -67),
                    new Vector3(-10, -14, -105),
                ],

                [
                    new Vector3(-25, -14, -66),
                    new Vector3(-10, -14, -105),
                    new Vector3(-35, -20, -110),
                ],

                [
                    new Vector3(-25, -14, -66),
                    new Vector3(-35, -20, -110),
                    new Vector3(-32, -14, -55),
                ],
            ],
            new Vector3(-0.13813256f, 0.98779845f, -0.07192723f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(32, -14, -55),
                new Vector3(35, -20, -110),
                new Vector3(10, -14, -105),
                new Vector3(10, -14, -67),
                new Vector3(25, -14, -66),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(25, -14, -66),
                    new Vector3(32, -14, -55),
                    new Vector3(35, -20, -110),
                ],

                [
                    new Vector3(25, -14, -66),
                    new Vector3(35, -20, -110),
                    new Vector3(10, -14, -105),
                ],

                [
                    new Vector3(25, -14, -66),
                    new Vector3(10, -14, -105),
                    new Vector3(10, -14, -67),
                ],
            ],
            new Vector3(0.13813256f, 0.98779845f, -0.07192723f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(10, -14, -67),
                new Vector3(10, -14, -105),
                new Vector3(-10, -14, -105),
                new Vector3(-10, -14, -67),
                new Vector3(0, -14, -67),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(0, -14, -67),
                    new Vector3(10, -14, -67),
                    new Vector3(10, -14, -105),
                ],

                [
                    new Vector3(0, -14, -67),
                    new Vector3(10, -14, -105),
                    new Vector3(-10, -14, -105),
                ],

                [
                    new Vector3(0, -14, -67),
                    new Vector3(-10, -14, -105),
                    new Vector3(-10, -14, -67),
                ],
            ],
            new Vector3(0f, 1f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-34, 7, 12),
                new Vector3(-34, 7, 0),
                new Vector3(-34, -14, 0),
                new Vector3(-32, -10, 55),
                new Vector3(-32, -3, 50),
                new Vector3(-32, 5, 50),
                new Vector3(-33, 7, 42),
                new Vector3(-33, -3, 37),
                new Vector3(-34, -3, 17),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-33, -3, 37),
                    new Vector3(-33, 7, 42),
                    new Vector3(-32, 5, 50),
                ],

                [
                    new Vector3(-33, -3, 37),
                    new Vector3(-32, 5, 50),
                    new Vector3(-32, -3, 50),
                ],

                [
                    new Vector3(-33, -3, 37),
                    new Vector3(-32, -3, 50),
                    new Vector3(-32, -10, 55),
                ],

                [
                    new Vector3(-33, -3, 37),
                    new Vector3(-32, -10, 55),
                    new Vector3(-34, -14, 0),
                ],

                [
                    new Vector3(-34, -14, 0),
                    new Vector3(-34, 7, 0),
                    new Vector3(-34, 7, 12),
                ],

                [
                    new Vector3(-34, -14, 0),
                    new Vector3(-34, 7, 12),
                    new Vector3(-34, -3, 17),
                ],

                [
                    new Vector3(-34, -14, 0),
                    new Vector3(-34, -3, 17),
                    new Vector3(-33, -3, 37),
                ],
            ],
            new Vector3(0.99916977f, 0.009408129f, -0.03964003f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-34, -5, 17),
                new Vector3(-34, -5, 4),
                new Vector3(-34, -11, 4),
                new Vector3(-34, -11, 17),
                new Vector3(-34, -5, 17),
                new Vector3(-34, -5, 16),
                new Vector3(-34, -11, 16),
                new Vector3(-34, -11, 13),
                new Vector3(-34, -5, 13),
                new Vector3(-34, -5, 12),
                new Vector3(-34, -11, 12),
                new Vector3(-34, -11, 9),
                new Vector3(-34, -5, 9),
                new Vector3(-34, -5, 8),
                new Vector3(-34, -11, 8),
                new Vector3(-34, -11, 5),
                new Vector3(-34, -5, 5),
                new Vector3(-34, -5, 4),
                new Vector3(-34, -11, 4),
                new Vector3(-34, -5, 4),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-34, -11, 4),
                    new Vector3(-34, -5, 4),
                    new Vector3(-34, -5, 5),
                ],

                [
                    new Vector3(-34, -11, 4),
                    new Vector3(-34, -5, 5),
                    new Vector3(-34, -11, 5),
                ],

                [
                    new Vector3(-34, -11, 8),
                    new Vector3(-34, -5, 8),
                    new Vector3(-34, -5, 9),
                ],

                [
                    new Vector3(-34, -11, 8),
                    new Vector3(-34, -5, 9),
                    new Vector3(-34, -11, 9),
                ],

                [
                    new Vector3(-34, -11, 12),
                    new Vector3(-34, -5, 12),
                    new Vector3(-34, -5, 13),
                ],

                [
                    new Vector3(-34, -11, 12),
                    new Vector3(-34, -5, 13),
                    new Vector3(-34, -11, 13),
                ],

                [
                    new Vector3(-34, -11, 16),
                    new Vector3(-34, -5, 16),
                    new Vector3(-34, -5, 17),
                ],

                [
                    new Vector3(-34, -11, 16),
                    new Vector3(-34, -5, 17),
                    new Vector3(-34, -11, 17),
                ],
            ],
            new Vector3(1f, 0f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, -14, -55),
                new Vector3(-34, -14, 0),
                new Vector3(-34, 7, 0),
                new Vector3(-32, 7, -50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, -14, -55),
                    new Vector3(-32, 7, -50),
                    new Vector3(-34, 7, 0),
                ],

                [
                    new Vector3(-32, -14, -55),
                    new Vector3(-34, 7, 0),
                    new Vector3(-34, -14, 0),
                ],
            ],
            new Vector3(0.99926823f, -0.0045215758f, 0.03798124f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-35, -20, -110),
                new Vector3(-32, -14, -55),
                new Vector3(-32, 7, -50),
                new Vector3(-31, 6, -58),
                new Vector3(-31, -3, -62),
                new Vector3(-32, -3, -82),
                new Vector3(-32, 2, -85),
                new Vector3(-35, 0, -100),
                new Vector3(-35, -10, -100),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-35, -10, -100),
                    new Vector3(-35, 0, -100),
                    new Vector3(-32, 2, -85),
                ],

                [
                    new Vector3(-35, -10, -100),
                    new Vector3(-32, 2, -85),
                    new Vector3(-32, -3, -82),
                ],

                [
                    new Vector3(-35, -10, -100),
                    new Vector3(-32, -3, -82),
                    new Vector3(-31, -3, -62),
                ],

                [
                    new Vector3(-31, -3, -62),
                    new Vector3(-31, 6, -58),
                    new Vector3(-32, 7, -50),
                ],

                [
                    new Vector3(-31, -3, -62),
                    new Vector3(-32, 7, -50),
                    new Vector3(-32, -14, -55),
                ],

                [
                    new Vector3(-31, -3, -62),
                    new Vector3(-32, -14, -55),
                    new Vector3(-35, -20, -110),
                ],

                [
                    new Vector3(-31, -3, -62),
                    new Vector3(-35, -20, -110),
                    new Vector3(-35, -10, -100),
                ],
            ],
            new Vector3(0.997849f, -0.025731081f, -0.060294174f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, 5, 50),
                new Vector3(-33, 7, 42),
                new Vector3(-33, 11, 42),
                new Vector3(-32, 9, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, 9, 50),
                    new Vector3(-32, 5, 50),
                    new Vector3(-33, 7, 42),
                ],

                [
                    new Vector3(-32, 9, 50),
                    new Vector3(-33, 7, 42),
                    new Vector3(-33, 11, 42),
                ],
            ],
            new Vector3(0.99227786f, 0f, -0.12403473f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-34, 11, 12),
                new Vector3(-34, 7, 12),
                new Vector3(-34, 7, 0),
                new Vector3(-32, 7, -50),
                new Vector3(-32, 11, -50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, 11, -50),
                    new Vector3(-34, 11, 12),
                    new Vector3(-34, 7, 12),
                ],

                [
                    new Vector3(-32, 11, -50),
                    new Vector3(-34, 7, 12),
                    new Vector3(-34, 7, 0),
                ],

                [
                    new Vector3(-32, 11, -50),
                    new Vector3(-34, 7, 0),
                    new Vector3(-32, 7, -50),
                ],
            ],
            new Vector3(0.99898106f, -0.030326517f, 0.033424374f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, 7, -50),
                new Vector3(-31, 6, -58),
                new Vector3(-31, 11, -58),
                new Vector3(-32, 11, -50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, 11, -50),
                    new Vector3(-32, 7, -50),
                    new Vector3(-31, 6, -58),
                ],

                [
                    new Vector3(-32, 11, -50),
                    new Vector3(-31, 6, -58),
                    new Vector3(-31, 11, -58),
                ],
            ],
            new Vector3(0.99227786f, 0f, 0.12403473f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-35, 0, -100),
                new Vector3(-32, 2, -85),
                new Vector3(-32, 7, -87),
                new Vector3(-32, 11, -87),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-35, 0, -100),
                    new Vector3(-32, 11, -87),
                    new Vector3(-32, 7, -87),
                ],

                [
                    new Vector3(-35, 0, -100),
                    new Vector3(-32, 7, -87),
                    new Vector3(-32, 2, -85),
                ],
            ],
            new Vector3(0.9802618f, -0.04525882f, -0.1924538f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(34, 7, 12),
                new Vector3(34, 7, 0),
                new Vector3(34, -14, 0),
                new Vector3(32, -10, 55),
                new Vector3(32, -3, 50),
                new Vector3(32, 5, 50),
                new Vector3(33, 7, 42),
                new Vector3(33, -3, 37),
                new Vector3(34, -3, 17),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(33, -3, 37),
                    new Vector3(33, 7, 42),
                    new Vector3(32, 5, 50),
                ],

                [
                    new Vector3(33, -3, 37),
                    new Vector3(32, 5, 50),
                    new Vector3(32, -3, 50),
                ],

                [
                    new Vector3(33, -3, 37),
                    new Vector3(32, -3, 50),
                    new Vector3(32, -10, 55),
                ],

                [
                    new Vector3(33, -3, 37),
                    new Vector3(32, -10, 55),
                    new Vector3(34, -14, 0),
                ],

                [
                    new Vector3(34, -14, 0),
                    new Vector3(34, 7, 0),
                    new Vector3(34, 7, 12),
                ],

                [
                    new Vector3(34, -14, 0),
                    new Vector3(34, 7, 12),
                    new Vector3(34, -3, 17),
                ],

                [
                    new Vector3(34, -14, 0),
                    new Vector3(34, -3, 17),
                    new Vector3(33, -3, 37),
                ],
            ],
            new Vector3(0.99916977f, -0.009408129f, 0.03964003f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(34, -5, 17),
                new Vector3(34, -5, 4),
                new Vector3(34, -11, 4),
                new Vector3(34, -11, 17),
                new Vector3(34, -5, 17),
                new Vector3(34, -5, 16),
                new Vector3(34, -11, 16),
                new Vector3(34, -11, 13),
                new Vector3(34, -5, 13),
                new Vector3(34, -5, 12),
                new Vector3(34, -11, 12),
                new Vector3(34, -11, 9),
                new Vector3(34, -5, 9),
                new Vector3(34, -5, 8),
                new Vector3(34, -11, 8),
                new Vector3(34, -11, 5),
                new Vector3(34, -5, 5),
                new Vector3(34, -5, 4),
                new Vector3(34, -11, 4),
                new Vector3(34, -5, 4),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(34, -11, 4),
                    new Vector3(34, -5, 4),
                    new Vector3(34, -5, 5),
                ],

                [
                    new Vector3(34, -11, 4),
                    new Vector3(34, -5, 5),
                    new Vector3(34, -11, 5),
                ],

                [
                    new Vector3(34, -11, 8),
                    new Vector3(34, -5, 8),
                    new Vector3(34, -5, 9),
                ],

                [
                    new Vector3(34, -11, 8),
                    new Vector3(34, -5, 9),
                    new Vector3(34, -11, 9),
                ],

                [
                    new Vector3(34, -11, 12),
                    new Vector3(34, -5, 12),
                    new Vector3(34, -5, 13),
                ],

                [
                    new Vector3(34, -11, 12),
                    new Vector3(34, -5, 13),
                    new Vector3(34, -11, 13),
                ],

                [
                    new Vector3(34, -11, 16),
                    new Vector3(34, -5, 16),
                    new Vector3(34, -5, 17),
                ],

                [
                    new Vector3(34, -11, 16),
                    new Vector3(34, -5, 17),
                    new Vector3(34, -11, 17),
                ],
            ],
            new Vector3(1f, 0f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(32, -14, -55),
                new Vector3(34, -14, 0),
                new Vector3(34, 7, 0),
                new Vector3(32, 7, -50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(32, -14, -55),
                    new Vector3(32, 7, -50),
                    new Vector3(34, 7, 0),
                ],

                [
                    new Vector3(32, -14, -55),
                    new Vector3(34, 7, 0),
                    new Vector3(34, -14, 0),
                ],
            ],
            new Vector3(0.99926823f, 0.0045215758f, -0.03798124f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(35, -20, -110),
                new Vector3(32, -14, -55),
                new Vector3(32, 7, -50),
                new Vector3(31, 6, -58),
                new Vector3(31, -3, -62),
                new Vector3(32, -3, -82),
                new Vector3(32, 2, -85),
                new Vector3(35, 0, -100),
                new Vector3(35, -10, -100),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(35, -10, -100),
                    new Vector3(35, 0, -100),
                    new Vector3(32, 2, -85),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(32, 2, -85),
                    new Vector3(32, -3, -82),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(32, -3, -82),
                    new Vector3(31, -3, -62),
                ],

                [
                    new Vector3(31, -3, -62),
                    new Vector3(31, 6, -58),
                    new Vector3(32, 7, -50),
                ],

                [
                    new Vector3(31, -3, -62),
                    new Vector3(32, 7, -50),
                    new Vector3(32, -14, -55),
                ],

                [
                    new Vector3(31, -3, -62),
                    new Vector3(32, -14, -55),
                    new Vector3(35, -20, -110),
                ],

                [
                    new Vector3(31, -3, -62),
                    new Vector3(35, -20, -110),
                    new Vector3(35, -10, -100),
                ],
            ],
            new Vector3(0.997849f, 0.025731081f, 0.060294174f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(32, 5, 50),
                new Vector3(33, 7, 42),
                new Vector3(33, 11, 42),
                new Vector3(32, 9, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(32, 9, 50),
                    new Vector3(32, 5, 50),
                    new Vector3(33, 7, 42),
                ],

                [
                    new Vector3(32, 9, 50),
                    new Vector3(33, 7, 42),
                    new Vector3(33, 11, 42),
                ],
            ],
            new Vector3(0.99227786f, 0f, 0.12403473f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(34, 11, 12),
                new Vector3(34, 7, 12),
                new Vector3(34, 7, 0),
                new Vector3(32, 7, -50),
                new Vector3(32, 11, -50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(32, 11, -50),
                    new Vector3(34, 11, 12),
                    new Vector3(34, 7, 12),
                ],

                [
                    new Vector3(32, 11, -50),
                    new Vector3(34, 7, 12),
                    new Vector3(34, 7, 0),
                ],

                [
                    new Vector3(32, 11, -50),
                    new Vector3(34, 7, 0),
                    new Vector3(32, 7, -50),
                ],
            ],
            new Vector3(0.99898106f, 0.030326517f, -0.033424374f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(32, 7, -50),
                new Vector3(31, 6, -58),
                new Vector3(31, 11, -58),
                new Vector3(32, 11, -50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(32, 11, -50),
                    new Vector3(32, 7, -50),
                    new Vector3(31, 6, -58),
                ],

                [
                    new Vector3(32, 11, -50),
                    new Vector3(31, 6, -58),
                    new Vector3(31, 11, -58),
                ],
            ],
            new Vector3(0.99227786f, 0f, -0.12403473f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(35, 0, -100),
                new Vector3(32, 2, -85),
                new Vector3(32, 7, -87),
                new Vector3(32, 11, -87),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(35, 0, -100),
                    new Vector3(32, 11, -87),
                    new Vector3(32, 7, -87),
                ],

                [
                    new Vector3(35, 0, -100),
                    new Vector3(32, 7, -87),
                    new Vector3(32, 2, -85),
                ],
            ],
            new Vector3(0.9802618f, 0.04525882f, 0.1924538f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-31, -8, 50),
                new Vector3(-31, -1, 50),
                new Vector3(-15, -2, 50),
                new Vector3(-16, -6, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-31, -8, 50),
                    new Vector3(-16, -6, 50),
                    new Vector3(-15, -2, 50),
                ],

                [
                    new Vector3(-31, -8, 50),
                    new Vector3(-15, -2, 50),
                    new Vector3(-31, -1, 50),
                ],
            ],
            new Vector3(0f, 0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(31, -8, 50),
                new Vector3(31, -1, 50),
                new Vector3(15, -2, 50),
                new Vector3(16, -6, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(16, -6, 50),
                    new Vector3(31, -8, 50),
                    new Vector3(31, -1, 50),
                ],

                [
                    new Vector3(16, -6, 50),
                    new Vector3(31, -1, 50),
                    new Vector3(15, -2, 50),
                ],
            ],
            new Vector3(-0f, -0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-5, -12, 52),
                new Vector3(-15, -10, 52),
                new Vector3(-13, -2, 50),
                new Vector3(13, -2, 50),
                new Vector3(15, -10, 52),
                new Vector3(5, -12, 52),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-5, -12, 52),
                    new Vector3(5, -12, 52),
                    new Vector3(15, -10, 52),
                ],

                [
                    new Vector3(-5, -12, 52),
                    new Vector3(15, -10, 52),
                    new Vector3(13, -2, 50),
                ],

                [
                    new Vector3(-5, -12, 52),
                    new Vector3(13, -2, 50),
                    new Vector3(-13, -2, 50),
                ],

                [
                    new Vector3(-5, -12, 52),
                    new Vector3(-13, -2, 50),
                    new Vector3(-15, -10, 52),
                ],
            ],
            new Vector3(-0f, 0.20952909f, 0.9778024f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-15, -10, 52),
                new Vector3(-32, -10, 55),
                new Vector3(-32, -3, 50),
                new Vector3(-31, -1, 50),
                new Vector3(-31, -8, 50),
                new Vector3(-16, -6, 50),
                new Vector3(-15, -2, 50),
                new Vector3(-13, -2, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-15, -10, 52),
                    new Vector3(-13, -2, 50),
                    new Vector3(-15, -2, 50),
                ],

                [
                    new Vector3(-15, -10, 52),
                    new Vector3(-15, -2, 50),
                    new Vector3(-16, -6, 50),
                ],

                [
                    new Vector3(-15, -10, 52),
                    new Vector3(-16, -6, 50),
                    new Vector3(-31, -8, 50),
                ],

                [
                    new Vector3(-31, -8, 50),
                    new Vector3(-31, -1, 50),
                    new Vector3(-32, -3, 50),
                ],

                [
                    new Vector3(-31, -8, 50),
                    new Vector3(-32, -3, 50),
                    new Vector3(-32, -10, 55),
                ],

                [
                    new Vector3(-31, -8, 50),
                    new Vector3(-32, -10, 55),
                    new Vector3(-15, -10, 52),
                ],
            ],
            new Vector3(0.032761615f, 0.31483766f, 0.94857997f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(15, -10, 52),
                new Vector3(32, -10, 55),
                new Vector3(32, -3, 50),
                new Vector3(31, -1, 50),
                new Vector3(31, -8, 50),
                new Vector3(16, -6, 50),
                new Vector3(15, -2, 50),
                new Vector3(13, -2, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(32, -10, 55),
                    new Vector3(32, -3, 50),
                    new Vector3(31, -1, 50),
                ],

                [
                    new Vector3(32, -10, 55),
                    new Vector3(31, -1, 50),
                    new Vector3(31, -8, 50),
                ],

                [
                    new Vector3(32, -10, 55),
                    new Vector3(31, -8, 50),
                    new Vector3(16, -6, 50),
                ],

                [
                    new Vector3(16, -6, 50),
                    new Vector3(15, -2, 50),
                    new Vector3(13, -2, 50),
                ],

                [
                    new Vector3(16, -6, 50),
                    new Vector3(13, -2, 50),
                    new Vector3(15, -10, 52),
                ],

                [
                    new Vector3(16, -6, 50),
                    new Vector3(15, -10, 52),
                    new Vector3(32, -10, 55),
                ],
            ],
            new Vector3(-0.032761615f, 0.31483766f, 0.94857997f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, 5, 50),
                new Vector3(32, 5, 50),
                new Vector3(32, -3, 50),
                new Vector3(31, -1, 50),
                new Vector3(15, -2, 50),
                new Vector3(13, -2, 50),
                new Vector3(-13, -2, 50),
                new Vector3(-15, -2, 50),
                new Vector3(-31, -1, 50),
                new Vector3(-32, -3, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, 5, 50),
                    new Vector3(-32, -3, 50),
                    new Vector3(-31, -1, 50),
                ],

                [
                    new Vector3(-32, 5, 50),
                    new Vector3(-31, -1, 50),
                    new Vector3(-15, -2, 50),
                ],

                [
                    new Vector3(-32, 5, 50),
                    new Vector3(-15, -2, 50),
                    new Vector3(-13, -2, 50),
                ],

                [
                    new Vector3(-32, 5, 50),
                    new Vector3(-13, -2, 50),
                    new Vector3(13, -2, 50),
                ],

                [
                    new Vector3(-32, 5, 50),
                    new Vector3(13, -2, 50),
                    new Vector3(15, -2, 50),
                ],

                [
                    new Vector3(-32, 5, 50),
                    new Vector3(15, -2, 50),
                    new Vector3(31, -1, 50),
                ],

                [
                    new Vector3(31, -1, 50),
                    new Vector3(32, -3, 50),
                    new Vector3(32, 5, 50),
                ],

                [
                    new Vector3(31, -1, 50),
                    new Vector3(32, 5, 50),
                    new Vector3(-32, 5, 50),
                ],
            ],
            new Vector3(-0f, -0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-32, 5, 50),
                new Vector3(-32, 9, 50),
                new Vector3(32, 9, 50),
                new Vector3(32, 5, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, 5, 50),
                    new Vector3(32, 5, 50),
                    new Vector3(32, 9, 50),
                ],

                [
                    new Vector3(-32, 5, 50),
                    new Vector3(32, 9, 50),
                    new Vector3(-32, 9, 50),
                ],
            ],
            new Vector3(0f, 0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-35, -20, -110),
                new Vector3(-35, -10, -100),
                new Vector3(-10, -10, -100),
                new Vector3(-10, -14, -105),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-10, -14, -105),
                    new Vector3(-35, -20, -110),
                    new Vector3(-35, -10, -100),
                ],

                [
                    new Vector3(-10, -14, -105),
                    new Vector3(-35, -10, -100),
                    new Vector3(-10, -10, -100),
                ],
            ],
            new Vector3(-0.017310701f, 0.72127926f, -0.69242805f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(35, -20, -110),
                new Vector3(35, -10, -100),
                new Vector3(10, -10, -100),
                new Vector3(10, -14, -105),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(35, -20, -110),
                    new Vector3(10, -14, -105),
                    new Vector3(10, -10, -100),
                ],

                [
                    new Vector3(35, -20, -110),
                    new Vector3(10, -10, -100),
                    new Vector3(35, -10, -100),
                ],
            ],
            new Vector3(0.017310701f, 0.72127926f, -0.69242805f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-10, -14, -105),
                new Vector3(-10, -10, -100),
                new Vector3(10, -10, -100),
                new Vector3(10, -14, -105),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(10, -14, -105),
                    new Vector3(-10, -14, -105),
                    new Vector3(-10, -10, -100),
                ],

                [
                    new Vector3(10, -14, -105),
                    new Vector3(-10, -10, -100),
                    new Vector3(10, -10, -100),
                ],
            ],
            new Vector3(0f, 0.7808688f, -0.62469506f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-33, -9, -100),
                new Vector3(-33, -4, -100),
                new Vector3(-17, -6, -100),
                new Vector3(-17, -9, -100),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-33, -9, -100),
                    new Vector3(-17, -9, -100),
                    new Vector3(-17, -6, -100),
                ],

                [
                    new Vector3(-33, -9, -100),
                    new Vector3(-17, -6, -100),
                    new Vector3(-33, -4, -100),
                ],
            ],
            new Vector3(-0f, -0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(33, -9, -100),
                new Vector3(33, -4, -100),
                new Vector3(17, -6, -100),
                new Vector3(17, -9, -100),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(17, -9, -100),
                    new Vector3(33, -9, -100),
                    new Vector3(33, -4, -100),
                ],

                [
                    new Vector3(17, -9, -100),
                    new Vector3(33, -4, -100),
                    new Vector3(17, -6, -100),
                ],
            ],
            new Vector3(0f, 0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(17, -6, -100),
                new Vector3(33, -4, -100),
                new Vector3(35, 0, -100),
                new Vector3(-35, 0, -100),
                new Vector3(-33, -4, -100),
                new Vector3(-17, -6, -100),
                new Vector3(-17, -9, -100),
                new Vector3(17, -9, -100),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(17, -6, -100),
                    new Vector3(33, -4, -100),
                    new Vector3(35, 0, -100),
                ],

                [
                    new Vector3(17, -6, -100),
                    new Vector3(35, 0, -100),
                    new Vector3(-35, 0, -100),
                ],

                [
                    new Vector3(17, -6, -100),
                    new Vector3(-35, 0, -100),
                    new Vector3(-33, -4, -100),
                ],

                [
                    new Vector3(17, -6, -100),
                    new Vector3(-33, -4, -100),
                    new Vector3(-17, -6, -100),
                ],

                [
                    new Vector3(17, -6, -100),
                    new Vector3(-17, -6, -100),
                    new Vector3(-17, -9, -100),
                ],

                [
                    new Vector3(17, -6, -100),
                    new Vector3(-17, -9, -100),
                    new Vector3(17, -9, -100),
                ],
            ],
            new Vector3(0f, 0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-35, 0, -100),
                new Vector3(-35, -10, -100),
                new Vector3(35, -10, -100),
                new Vector3(35, 0, -100),
                new Vector3(33, -4, -100),
                new Vector3(33, -9, -100),
                new Vector3(17, -9, -100),
                new Vector3(-17, -9, -100),
                new Vector3(-33, -9, -100),
                new Vector3(-33, -4, -100),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-33, -4, -100),
                    new Vector3(-35, 0, -100),
                    new Vector3(-35, -10, -100),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(35, 0, -100),
                    new Vector3(33, -4, -100),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(33, -4, -100),
                    new Vector3(33, -9, -100),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(33, -9, -100),
                    new Vector3(17, -9, -100),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(17, -9, -100),
                    new Vector3(-17, -9, -100),
                ],

                [
                    new Vector3(35, -10, -100),
                    new Vector3(-17, -9, -100),
                    new Vector3(-33, -9, -100),
                ],

                [
                    new Vector3(-33, -9, -100),
                    new Vector3(-33, -4, -100),
                    new Vector3(-35, -10, -100),
                ],

                [
                    new Vector3(-33, -9, -100),
                    new Vector3(-35, -10, -100),
                    new Vector3(35, -10, -100),
                ],
            ],
            new Vector3(0f, 0f, 1f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-35, 0, -100),
                new Vector3(35, 0, -100),
                new Vector3(32, 11, -87),
                new Vector3(-32, 11, -87),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-35, 0, -100),
                    new Vector3(-32, 11, -87),
                    new Vector3(32, 11, -87),
                ],

                [
                    new Vector3(-35, 0, -100),
                    new Vector3(32, 11, -87),
                    new Vector3(35, 0, -100),
                ],
            ],
            new Vector3(0f, 0.7633863f, -0.6459423f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(32, 9, 50),
                new Vector3(33, 11, 42),
                new Vector3(-33, 11, 42),
                new Vector3(-32, 9, 50),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-32, 9, 50),
                    new Vector3(32, 9, 50),
                    new Vector3(33, 11, 42),
                ],

                [
                    new Vector3(-32, 9, 50),
                    new Vector3(33, 11, 42),
                    new Vector3(-33, 11, 42),
                ],
            ],
            new Vector3(-0f, 0.9701425f, 0.24253562f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(34, 11, 12),
                new Vector3(33, 11, 42),
                new Vector3(-33, 11, 42),
                new Vector3(-34, 11, 12),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(34, 11, 12),
                    new Vector3(-34, 11, 12),
                    new Vector3(-33, 11, 42),
                ],

                [
                    new Vector3(34, 11, 12),
                    new Vector3(-33, 11, 42),
                    new Vector3(33, 11, 42),
                ],
            ],
            new Vector3(0f, 1f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(34, 11, 12),
                new Vector3(32, 11, -50),
                new Vector3(-32, 11, -50),
                new Vector3(-34, 11, 12),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-34, 11, 12),
                    new Vector3(34, 11, 12),
                    new Vector3(32, 11, -50),
                ],

                [
                    new Vector3(-34, 11, 12),
                    new Vector3(32, 11, -50),
                    new Vector3(-32, 11, -50),
                ],
            ],
            new Vector3(0f, 1f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(31, 11, -58),
                new Vector3(32, 11, -50),
                new Vector3(-32, 11, -50),
                new Vector3(-31, 11, -58),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(31, 11, -58),
                    new Vector3(-31, 11, -58),
                    new Vector3(-32, 11, -50),
                ],

                [
                    new Vector3(31, 11, -58),
                    new Vector3(-32, 11, -50),
                    new Vector3(32, 11, -50),
                ],
            ],
            new Vector3(0f, 1f, 0f),
            1
        ];
        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(31, 11, -58),
                new Vector3(32, 11, -87),
                new Vector3(-32, 11, -87),
                new Vector3(-31, 11, -58),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-31, 11, -58),
                    new Vector3(31, 11, -58),
                    new Vector3(32, 11, -87),
                ],

                [
                    new Vector3(-31, 11, -58),
                    new Vector3(32, 11, -87),
                    new Vector3(-32, 11, -87),
                ],
            ],
            new Vector3(0f, 1f, 0f),
            1
        ];

        #endregion
    }

    [Test]
    [TestCaseSource(nameof(TriangulatorTestCases))]
    public void TestTriangulation(Vector3[] expectedInput, Vector3[][] expectedOutput, Vector3 expectedNormal,
        int expectedRegionCount)
    {
        var result = PolygonTriangulator.Triangulate(expectedInput);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Triangles.Count / 3, Is.EqualTo(expectedOutput.Length), "Unexpected triangle count.");
            Assert.That(result.RegionCount, Is.EqualTo(expectedRegionCount), "Unexpected region count.");
            Assert.That(result.PlaneNormal, Is.EqualTo(expectedNormal), "Unexpected plane normal.");

            // Assert that each triangle exists in output, and that there are no extra triangles or duplicates, however
            // ignore the order of vertices in each triangle or the order of triangles in the list.

            // Convert result triangles (indices) to array of triangle arrays (Vector3)
            var actualTriangles = new List<Vector3[]>();
            for (int i = 0; i < result.Triangles.Count; i += 3)
            {
                actualTriangles.Add([
                    expectedInput[result.Triangles[i]],
                    expectedInput[result.Triangles[i + 1]],
                    expectedInput[result.Triangles[i + 2]]
                ]);
            }

            // Check that each expected triangle exists in actual triangles
            foreach (var expectedTriangle in expectedOutput)
            {
                var found = actualTriangles.Any(actualTriangle => TrianglesEqual(expectedTriangle, actualTriangle));
                Assert.That(found, Is.True,
                    $"Expected triangle [{expectedTriangle[0]}, {expectedTriangle[1]}, {expectedTriangle[2]}] not found in output.");
            }

            // Check that each actual triangle exists in expected triangles (no extras)
            foreach (var actualTriangle in actualTriangles)
            {
                var found = expectedOutput.Any(expectedTriangle => TrianglesEqual(expectedTriangle, actualTriangle));
                Assert.That(found, Is.True,
                    $"Unexpected triangle [{actualTriangle[0]}, {actualTriangle[1]}, {actualTriangle[2]}] found in output.");
            }
        }

        Assert.Pass();
    }

    /// <summary>
    /// Compares two triangles for equality, ignoring the order of vertices (cyclic permutations).
    /// </summary>
    private static bool TrianglesEqual(Vector3[] triangle1, Vector3[] triangle2)
    {
        if (triangle1.Length != 3 || triangle2.Length != 3)
            return false;

        // Check all cyclic permutations of triangle1 against triangle2
        for (int offset = 0; offset < 3; offset++)
        {
            if (triangle1[offset] == triangle2[0] &&
                triangle1[(offset + 1) % 3] == triangle2[1] &&
                triangle1[(offset + 2) % 3] == triangle2[2])
            {
                return true;
            }
        }

        // Also check reversed order (in case winding order is reversed)
        for (int offset = 0; offset < 3; offset++)
        {
            if (triangle1[offset] == triangle2[0] &&
                triangle1[(offset + 2) % 3] == triangle2[1] &&
                triangle1[(offset + 1) % 3] == triangle2[2])
            {
                return true;
            }
        }

        return false;
    }
}