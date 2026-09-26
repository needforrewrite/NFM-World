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

        #region Complex poly that it fell apart on before

        yield return (object[])
        [
            (Vector3[])
            [
                new Vector3(-47.6000023f, -45.9000015f, 5.10000038f) * 10,
                new Vector3(-47.6000023f, -42.5f, 5.10000038f) * 10,
                new Vector3(-37.4000015f, -42.5f, 6.80000019f) * 10,
                new Vector3(0f, -42.5f, 11.9000006f) * 10,
                new Vector3(37.4000015f, -42.5f, 6.80000019f) * 10,
                new Vector3(47.6000023f, -42.5f, 5.10000038f) * 10,
                new Vector3(47.6000023f, -45.9000015f, 5.10000038f) * 10,
                new Vector3(37.4000015f, -45.9000015f, 6.80000019f) * 10,
                new Vector3(0f, -45.9000015f, 11.9000006f) * 10,
                new Vector3(-37.4000015f, -45.9000015f, 6.80000019f) * 10,
            ],
            (Vector3[][])
            [
                [
                    new Vector3(374f, -459f, 68f),
                    new Vector3(476.00003f, -459f, 51.000004f),
                    new Vector3(476.00003f, -425f, 51.000004f),
                ],
                [

                    new Vector3(374f, -459f, 68f),
                    new Vector3(476.00003f, -425f, 51.000004f),
                    new Vector3(374f, -425f, 68f),
                ],
                [

                    new Vector3(374f, -459f, 68f),
                    new Vector3(374f, -425f, 68f),
                    new Vector3(0f, -425f, 119.00001f),
                ],
                [

                    new Vector3(374f, -459f, 68f),
                    new Vector3(0f, -425f, 119.00001f),
                    new Vector3(-374f, -425f, 68f),
                ],
                [

                    new Vector3(374f, -459f, 68f),
                    new Vector3(-374f, -425f, 68f),
                    new Vector3(-476.00003f, -425f, 51.000004f),
                ],
                [

                    new Vector3(-476.00003f, -425f, 51.000004f),
                    new Vector3(-476.00003f, -459f, 51.000004f),
                    new Vector3(-374f, -459f, 68f),
                ],
                [

                    new Vector3(-476.00003f, -425f, 51.000004f),
                    new Vector3(-374f, -459f, 68f),
                    new Vector3(0f, -459f, 119.00001f),
                ],
                [

                    new Vector3(-476.00003f, -425f, 51.000004f),
                    new Vector3(0f, -459f, 119.00001f),
                    new Vector3(374f, -459f, 68f),
                ],
            ],
            new Vector3(5.4962326E-18f, 1f, 3.6665675E-09f),
            1,
        ];

        #endregion

        #region radicalone headlight fixture (very hard!)

        yield return (object[])
        [
            (Vector3[]) [
                new Vector3(-30,-18,85),
                new Vector3(-44,-15,79),
                new Vector3(-44,-7,87),

                new Vector3(-17,-7,104),
                new Vector3(-15,-8,103),

                new Vector3(-21,-9,100),
                new Vector3(-39,-10,87),
                new Vector3(-39,-14,83),
                new Vector3(-33,-15,85),
                new Vector3(-20,-10,99),
                new Vector3(-21,-9,100),

                new Vector3(-15,-8,103),
            ],
            (Vector3[][]) [

                [
                    new Vector3(-17, -7, 104),
                    new Vector3(-21, -9, 100),
                    new Vector3(-44, -7, 87),
                ],

                [
                    new Vector3(-44, -7, 87),
                    new Vector3(-21, -9, 100),
                    new Vector3(-39, -10, 87),
                ],

                [
                    new Vector3(-44, -7, 87),
                    new Vector3(-39, -10, 87),
                    new Vector3(-39, -14, 83),
                ],

                [
                    new Vector3(-44, -7, 87),
                    new Vector3(-39, -14, 83),
                    new Vector3(-44, -15, 79),
                ],

                [
                    new Vector3(-39, -14, 83),
                    new Vector3(-30, -18, 85),
                    new Vector3(-44, -15, 79),
                ],

                [
                    new Vector3(-39, -14, 83),
                    new Vector3(-33, -15, 85),
                    new Vector3(-30, -18, 85),
                ],

                [
                    new Vector3(-33, -15, 85),
                    new Vector3(-20, -10, 99),
                    new Vector3(-30, -18, 85),
                ],

                [
                    new Vector3(-30, -18, 85),
                    new Vector3(-20, -10, 99),
                    new Vector3(-15, -8, 103),
                ],

                [
                    new Vector3(-17, -7, 104),
                    new Vector3(-15, -8, 103),
                    new Vector3(-20, -10, 99),
                ],

                [
                    new Vector3(-21, -9, 100),
                    new Vector3(-17, -7, 104),
                    new Vector3(-20, -10, 99),
                ],
            ],
            new Vector3(0.39011782f, 0.6627052f, -0.63924164f),
            2
        ];

        #endregion

        #region drmonster rear part (this one was a nightmare)

        yield return (object[])
        [
            (Vector3[]) [
                new Vector3(-40,-54,-103),
                new Vector3(-40,-27,-103),
                new Vector3(40,-27,-103),
                new Vector3(40,-54,-103),
                new Vector3(38,-43,-103),
                new Vector3(33,-42,-104),
                new Vector3(33,-34,-104),
                new Vector3(38,-33,-103),
                new Vector3(38,-43,-103),
                new Vector3(40,-54,-103),
                new Vector3(19,-43,-103),
                new Vector3(0,-45,-103),
                new Vector3(-19,-43,-103),
                new Vector3(-40,-54,-103),
                new Vector3(-38,-43,-103),
                new Vector3(-33,-42,-104),
                new Vector3(-33,-34,-104),
                new Vector3(-38,-33,-103),
                new Vector3(-38,-43,-103),
            ],
            (Vector3[][]) [
                [
                    new Vector3(-40, -54, -103),
                    new Vector3(-38, -43, -103),
                    new Vector3(-40, -27, -103),
                ],

                [
                    new Vector3(-38, -43, -103),
                    new Vector3(-38, -33, -103),
                    new Vector3(-40, -27, -103),
                ],

                [
                    new Vector3(-40, -27, -103),
                    new Vector3(-38, -33, -103),
                    new Vector3(-33, -34, -104),
                ],

                [
                    new Vector3(-40, -27, -103),
                    new Vector3(-33, -34, -104),
                    new Vector3(40, -27, -103),
                ],

                [
                    new Vector3(40, -27, -103),
                    new Vector3(-33, -34, -104),
                    new Vector3(33, -34, -104),
                ],

                [
                    new Vector3(-33, -34, -104),
                    new Vector3(-19, -43, -103),
                    new Vector3(33, -34, -104),
                ],

                [
                    new Vector3(-19, -43, -103),
                    new Vector3(0, -45, -103),
                    new Vector3(33, -34, -104),
                ],

                [
                    new Vector3(19, -43, -103),
                    new Vector3(33, -34, -104),
                    new Vector3(0, -45, -103),
                ],

                [
                    new Vector3(33, -42, -104),
                    new Vector3(33, -34, -104),
                    new Vector3(19, -43, -103),
                ],

                [
                    new Vector3(19, -43, -103),
                    new Vector3(40, -54, -103),
                    new Vector3(33, -42, -104),
                ],

                [
                    new Vector3(33, -42, -104),
                    new Vector3(40, -54, -103),
                    new Vector3(38, -43, -103),
                ],

                [
                    new Vector3(38, -43, -103),
                    new Vector3(40, -54, -103),
                    new Vector3(40, -27, -103),
                ],

                [
                    new Vector3(38, -33, -103),
                    new Vector3(38, -43, -103),
                    new Vector3(40, -27, -103),
                ],

                [
                    new Vector3(38, -33, -103),
                    new Vector3(40, -27, -103),
                    new Vector3(33, -34, -104),
                ],

                [
                    new Vector3(-33, -34, -104),
                    new Vector3(-33, -42, -104),
                    new Vector3(-19, -43, -103),
                ],

                [
                    new Vector3(-33, -42, -104),
                    new Vector3(-40, -54, -103),
                    new Vector3(-19, -43, -103),
                ],

                [
                    new Vector3(-33, -42, -104),
                    new Vector3(-38, -43, -103),
                    new Vector3(-40, -54, -103),
                ],
            ],
            new Vector3(-3.573928E-12f, 0.010934371f, 0.9999402f),
            3
        ];

        #endregion

        #region polygon that was previously returning a triangle count not divisible by 3

        yield return (object[])
        [
            (Vector3[])
            [

                new Vector3(42.5f, 23.800001f, 207.40001f),
                new Vector3(42.5f, -8.5f, 207.40001f),
                new Vector3(27.2f, -20.400002f, 207.40001f),
                new Vector3(13.6f, -23.800001f, 207.40001f),
                new Vector3(-13.6f, -23.800001f, 207.40001f),
                new Vector3(-27.2f, -20.400002f, 207.40001f),
                new Vector3(-42.5f, -8.5f, 207.40001f),
                new Vector3(-42.5f, 23.800001f, 207.40001f),
                new Vector3(-35.7f, 23.800001f, 207.40001f),
                new Vector3(35.7f, 23.800001f, 207.40001f),
                new Vector3(35.7f, 11.900001f, 207.40001f),
                new Vector3(-35.7f, 11.900001f, 207.40001f),
                new Vector3(-35.7f, -5.1000004f, 207.40001f),
                new Vector3(-23.800001f, -15.3f, 207.40001f),
                new Vector3(-13.6f, -17f, 207.40001f),
                new Vector3(13.6f, -17f, 207.40001f),
                new Vector3(23.800001f, -15.3f, 207.40001f),
                new Vector3(35.7f, -5.1000004f, 207.40001f),
                new Vector3(35.7f, 23.800001f, 207.40001f),
            ],
            (Vector3[][])
            [
                [
                    new Vector3(-23.800001f, -15.3f, 207.40001f),
                    new Vector3(-27.2f, -20.400002f, 207.40001f),
                    new Vector3(-13.6f, -23.800001f, 207.40001f),
                ],

                [
                    new Vector3(-23.800001f, -15.3f, 207.40001f),
                    new Vector3(-13.6f, -23.800001f, 207.40001f),
                    new Vector3(-13.6f, -17f, 207.40001f),
                ],

                [
                    new Vector3(-13.6f, -17f, 207.40001f),
                    new Vector3(-13.6f, -23.800001f, 207.40001f),
                    new Vector3(13.6f, -23.800001f, 207.40001f),
                ],

                [
                    new Vector3(-13.6f, -17f, 207.40001f),
                    new Vector3(13.6f, -23.800001f, 207.40001f),
                    new Vector3(13.6f, -17f, 207.40001f),
                ],

                [
                    new Vector3(13.6f, -17f, 207.40001f),
                    new Vector3(13.6f, -23.800001f, 207.40001f),
                    new Vector3(23.800001f, -15.3f, 207.40001f),
                ],

                [
                    new Vector3(13.6f, -23.800001f, 207.40001f),
                    new Vector3(27.2f, -20.400002f, 207.40001f),
                    new Vector3(23.800001f, -15.3f, 207.40001f),
                ],

                [
                    new Vector3(23.800001f, -15.3f, 207.40001f),
                    new Vector3(27.2f, -20.400002f, 207.40001f),
                    new Vector3(35.7f, -5.1000004f, 207.40001f),
                ],

                [
                    new Vector3(27.2f, -20.400002f, 207.40001f),
                    new Vector3(42.5f, -8.5f, 207.40001f),
                    new Vector3(35.7f, -5.1000004f, 207.40001f),
                ],

                [
                    new Vector3(35.7f, 11.900001f, 207.40001f),
                    new Vector3(35.7f, -5.1000004f, 207.40001f),
                    new Vector3(42.5f, -8.5f, 207.40001f),
                ],

                [
                    new Vector3(42.5f, 23.800001f, 207.40001f),
                    new Vector3(35.7f, 11.900001f, 207.40001f),
                    new Vector3(42.5f, -8.5f, 207.40001f),
                ],

                [
                    new Vector3(35.7f, 11.900001f, 207.40001f),
                    new Vector3(42.5f, 23.800001f, 207.40001f),
                    new Vector3(35.7f, 23.800001f, 207.40001f),
                ],

                [
                    new Vector3(-35.7f, -5.1000004f, 207.40001f),
                    new Vector3(-27.2f, -20.400002f, 207.40001f),
                    new Vector3(-23.800001f, -15.3f, 207.40001f),
                ],

                [
                    new Vector3(-42.5f, -8.5f, 207.40001f),
                    new Vector3(-27.2f, -20.400002f, 207.40001f),
                    new Vector3(-35.7f, -5.1000004f, 207.40001f),
                ],

                [
                    new Vector3(-42.5f, -8.5f, 207.40001f),
                    new Vector3(-35.7f, -5.1000004f, 207.40001f),
                    new Vector3(-35.7f, 11.900001f, 207.40001f),
                ],

                [
                    new Vector3(-42.5f, 23.800001f, 207.40001f),
                    new Vector3(-42.5f, -8.5f, 207.40001f),
                    new Vector3(-35.7f, 11.900001f, 207.40001f),
                ],

                [
                    new Vector3(-35.7f, 11.900001f, 207.40001f),
                    new Vector3(-35.7f, 23.800001f, 207.40001f),
                    new Vector3(-42.5f, 23.800001f, 207.40001f),
                ],

                [
                    new Vector3(-35.7f, 11.900001f, 207.40001f),
                    new Vector3(35.7f, 23.800001f, 207.40001f),
                    new Vector3(-35.7f, 23.800001f, 207.40001f),
                ],

                [
                    new Vector3(-35.7f, 11.900001f, 207.40001f),
                    new Vector3(35.7f, 11.900001f, 207.40001f),
                    new Vector3(35.7f, 23.800001f, 207.40001f),
                ],


            ],
            new Vector3(4.382401E-14f, 8.668599E-14f, 1f),
            2
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
            Assert.That(result.Triangles.Length / 3, Is.EqualTo(expectedOutput.Length), "Unexpected triangle count.");
            Assert.That(result.RegionCount, Is.EqualTo(expectedRegionCount), "Unexpected region count.");
            Assert.That(result.PlaneNormal, Is.EqualTo(expectedNormal), "Unexpected plane normal.");

            // Assert that each triangle exists in output, and that there are no extra triangles or duplicates, however
            // ignore the order of vertices in each triangle or the order of triangles in the list.

            // Convert result triangles (indices) to array of triangle arrays (Vector3)
            var actualTriangles = new List<Vector3[]>();
            for (int i = 0; i < result.Triangles.Length; i += 3)
            {
                actualTriangles.Add([
                    expectedInput[result.Triangles[i]],
                    expectedInput[result.Triangles[i + 1]],
                    expectedInput[result.Triangles[i + 2]]
                ]);
            }
            
            for (int i = 0; i < result.Triangles.Length; i += 3)
            {
                Console.WriteLine("[");
                Console.WriteLine($"new Vector3({expectedInput[result.Triangles[i]]}),".Replace("<", "").Replace(">", ""));
                Console.WriteLine($"new Vector3({expectedInput[result.Triangles[i + 1]]}),".Replace("<", "").Replace(">", ""));
                Console.WriteLine($"new Vector3({expectedInput[result.Triangles[i + 2]]}),".Replace("<", "").Replace(">", ""));
                Console.WriteLine("],");
                Console.WriteLine();
            }
            
            for (int i = 0; i < result.Triangles.Length; i += 3)
            {
                Console.WriteLine("<p>");
                Console.WriteLine($"c({Random.Shared.Next(0, 256)},{Random.Shared.Next(0, 256)},{Random.Shared.Next(0, 256)})");
                Console.WriteLine("gr(40)");
                Console.WriteLine("fs(1)");
                Console.WriteLine($"p({expectedInput[result.Triangles[i]]})".Replace("<", "").Replace(">", "")
                    .Replace(", ", ","));
                Console.WriteLine($"p({expectedInput[result.Triangles[i + 1]]})".Replace("<", "").Replace(">", "")
                    .Replace(", ", ","));
                Console.WriteLine($"p({expectedInput[result.Triangles[i + 2]]})".Replace("<", "").Replace(">", "")
                    .Replace(", ", ","));
                Console.WriteLine("</p>");
                Console.WriteLine();
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

    #region Hole Detection Tests

    /// <summary>
    /// Door polygon with a window cutout. The self-intersecting path defines a door shape
    /// with a rectangular window hole. Should detect 2 regions (outer + 1 hole).
    /// </summary>
    [Test]
    public void DoorPolygon_WithWindowCutout_DetectsHole()
    {
        var vertices = new Vector3[]
        {
            new Vector3(-56,-17,192),
            new Vector3(-56,-9,192),
            new Vector3(-56,40,192),
            new Vector3(-57,40,177),
            new Vector3(-59,40,161),
            new Vector3(-60,40,145),
            new Vector3(-62,40,129),
            new Vector3(-63,40,118),
            new Vector3(-63,-73,118),
            new Vector3(-56,-73,185),
            new Vector3(-56,-37,192),
            new Vector3(-56,-37,185),
            new Vector3(-57,-65,180),
            new Vector3(-62,-65,128),
            new Vector3(-62,-32,128),
            new Vector3(-59,-32,157),
            new Vector3(-58,-22,163),
            new Vector3(-56,-22,185),
            new Vector3(-56,-37,185),
            new Vector3(-56,-37,192),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Assert.That(result.RegionCount, Is.EqualTo(2), "Door with window should detect 2 regions (outer + 1 hole).");
        Assert.That(result.Triangles.Length, Is.GreaterThan(0), "Should produce triangles.");
        Assert.That(result.Triangles.Length % 3, Is.EqualTo(0), "Triangle array length must be divisible by 3.");

        // All triangle indices must be valid
        foreach (var idx in result.Triangles)
        {
            Assert.That(idx, Is.LessThan((uint)vertices.Length), $"Triangle index {idx} out of range.");
        }

        // Verify no degenerate triangles (all 3 vertices distinct)
        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            var a = vertices[result.Triangles[i]];
            var b = vertices[result.Triangles[i + 1]];
            var c = vertices[result.Triangles[i + 2]];
            Assert.That(a, Is.Not.EqualTo(b), $"Degenerate triangle at {i}: vertex {result.Triangles[i]} equals {result.Triangles[i+1]}");
            Assert.That(b, Is.Not.EqualTo(c), $"Degenerate triangle at {i}: vertex {result.Triangles[i+1]} equals {result.Triangles[i+2]}");
            Assert.That(c, Is.Not.EqualTo(a), $"Degenerate triangle at {i}: vertex {result.Triangles[i+2]} equals {result.Triangles[i]}");
        }
    }

    /// <summary>
    /// Car rear polygon with 2 rectangular holes. Should detect 3 regions (outer + 2 holes).
    /// </summary>
    [Test]
    public void CarRearPolygon_WithTwoHoles_DetectsBothHoles()
    {
        var vertices = new Vector3[]
        {
            new Vector3(-40,-54,-103),
            new Vector3(-40,-27,-103),
            new Vector3(40,-27,-103),
            new Vector3(40,-54,-103),
            new Vector3(38,-43,-103),
            new Vector3(33,-42,-104),
            new Vector3(33,-34,-104),
            new Vector3(38,-33,-103),
            new Vector3(38,-43,-103),
            new Vector3(40,-54,-103),
            new Vector3(19,-43,-103),
            new Vector3(0,-45,-103),
            new Vector3(-19,-43,-103),
            new Vector3(-40,-54,-103),
            new Vector3(-38,-43,-103),
            new Vector3(-33,-42,-104),
            new Vector3(-33,-34,-104),
            new Vector3(-38,-33,-103),
            new Vector3(-38,-43,-103),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Assert.That(result.RegionCount, Is.EqualTo(3), "Car rear should detect 3 regions (outer + 2 holes).");
        Assert.That(result.Triangles.Length, Is.GreaterThan(0), "Should produce triangles.");
        Assert.That(result.Triangles.Length % 3, Is.EqualTo(0), "Triangle array length must be divisible by 3.");

        foreach (var idx in result.Triangles)
        {
            Assert.That(idx, Is.LessThan((uint)vertices.Length), $"Triangle index {idx} out of range.");
        }

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            Assert.That(vertices[result.Triangles[i]], Is.Not.EqualTo(vertices[result.Triangles[i + 1]]));
            Assert.That(vertices[result.Triangles[i + 1]], Is.Not.EqualTo(vertices[result.Triangles[i + 2]]));
            Assert.That(vertices[result.Triangles[i + 2]], Is.Not.EqualTo(vertices[result.Triangles[i]]));
        }
    }

    /// <summary>
    /// Windshield polygon with 1 hole. Should detect 2 regions.
    /// </summary>
    [Test]
    public void WindshieldPolygon_WithHole_DetectsHole()
    {
        var vertices = new Vector3[]
        {
            new Vector3(42.5f,23.800001f,207.40001f),
            new Vector3(42.5f,-8.5f,207.40001f),
            new Vector3(27.2f,-20.400002f,207.40001f),
            new Vector3(13.6f,-23.800001f,207.40001f),
            new Vector3(-13.6f,-23.800001f,207.40001f),
            new Vector3(-27.2f,-20.400002f,207.40001f),
            new Vector3(-42.5f,-8.5f,207.40001f),
            new Vector3(-42.5f,23.800001f,207.40001f),
            new Vector3(-35.7f,23.800001f,207.40001f),
            new Vector3(35.7f,23.800001f,207.40001f),
            new Vector3(35.7f,11.900001f,207.40001f),
            new Vector3(-35.7f,11.900001f,207.40001f),
            new Vector3(-35.7f,-5.1000004f,207.40001f),
            new Vector3(-23.800001f,-15.3f,207.40001f),
            new Vector3(-13.6f,-17f,207.40001f),
            new Vector3(13.6f,-17f,207.40001f),
            new Vector3(23.800001f,-15.3f,207.40001f),
            new Vector3(35.7f,-5.1000004f,207.40001f),
            new Vector3(35.7f,23.800001f,207.40001f),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Assert.That(result.RegionCount, Is.EqualTo(2), "Windshield should detect 2 regions (outer + 1 hole).");
        Assert.That(result.Triangles.Length, Is.GreaterThan(0), "Should produce triangles.");
        Assert.That(result.Triangles.Length % 3, Is.EqualTo(0), "Triangle array length must be divisible by 3.");

        foreach (var idx in result.Triangles)
        {
            Assert.That(idx, Is.LessThan((uint)vertices.Length), $"Triangle index {idx} out of range.");
        }

        for (int i = 0; i < result.Triangles.Length; i += 3)
        {
            Assert.That(vertices[result.Triangles[i]], Is.Not.EqualTo(vertices[result.Triangles[i + 1]]));
            Assert.That(vertices[result.Triangles[i + 1]], Is.Not.EqualTo(vertices[result.Triangles[i + 2]]));
            Assert.That(vertices[result.Triangles[i + 2]], Is.Not.EqualTo(vertices[result.Triangles[i]]));
        }
    }

    /// <summary>
    /// Simple convex quad with no holes should take the fast path (TryTriangulateSimple).
    /// </summary>
    [Test]
    public void SimpleConvexQuad_NoHoles_UsesFastPath()
    {
        var vertices = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(10, 10, 0),
            new Vector3(0, 10, 0),
        };

        var result = PolygonTriangulator.Triangulate(vertices);

        Assert.That(result.RegionCount, Is.EqualTo(1), "Simple quad should have 1 region.");
        Assert.That(result.Triangles.Length, Is.EqualTo(6), "Simple convex quad should produce 2 triangles (6 indices).");

        foreach (var idx in result.Triangles)
        {
            Assert.That(idx, Is.LessThan((uint)vertices.Length), $"Triangle index {idx} out of range.");
        }

        // Fan triangulation from vertex 0: (0,1,2) and (0,2,3)
        Assert.That(result.Triangles[0], Is.EqualTo((uint)0));
        Assert.That(result.Triangles[1], Is.EqualTo((uint)1));
        Assert.That(result.Triangles[2], Is.EqualTo((uint)2));
        Assert.That(result.Triangles[3], Is.EqualTo((uint)0));
        Assert.That(result.Triangles[4], Is.EqualTo((uint)2));
        Assert.That(result.Triangles[5], Is.EqualTo((uint)3));
    }

    #endregion
}