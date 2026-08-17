using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using Random = NFMWorldLibrary.Util.Random;

namespace NFMWorld;

public static class MeshDamage
{
    public static void NewCar(BackendCar car, CarVisual visual)
    {
        visual.Mesh.Polys = visual.Mesh.OriginalPolys.Select(static poly => poly.SafeClone()).ToArray();
        
        for (var i = 0; i < visual.Mesh.Polys.Length; i++)
        {
            visual.Bfase[i] = 0.0f;
        }
        
        visual.Mesh.RebuildMesh();
    }
    
    public static void DamageX(
        CarStats stat,
        BackendCar car,
        CarVisual visual,
        int wheelIdx,
        float damageFactor
    )
    {
        var wheel = car.Wheels[wheelIdx];

        damageFactor *= (float)stat.Dammult;
        if (Math.Abs(damageFactor) > 100.0f)
        {
            if (damageFactor > 100.0f)
            {
                damageFactor -= 100.0f;
            }

            if (damageFactor < -100.0f)
            {
                damageFactor += 100.0f;
            }

            for (var i = 0; i < visual.Mesh.Polys.Length; i++)
            {
                var breakFactor = 0.0f;
                for (var j = 0; j < visual.Mesh.Polys[i].Points.Length; j++)
                {
                    if (UMath.Py(
                            (float)wheel.Position.X,
                            visual.Mesh.Polys[i].Points[j].X, // x
                            (float)wheel.Position.Z,
                            visual.Mesh.Polys[i].Points[j].Z // z
                        ) < stat.Clrad)
                    {
                        breakFactor = damageFactor / 20.0f * Random.Single();
                        visual.Mesh.Polys[i].Points[j].Z -= (breakFactor * UMath.SinUnsafe((float)visual.Rotation.Xz.Degrees) *
                                                     UMath.CosUnsafe((float)visual.Rotation.Zy.Degrees)); // z
                        visual.Mesh.Polys[i].Points[j].X += (breakFactor * UMath.CosUnsafe((float)visual.Rotation.Xz.Degrees) *
                                                     UMath.CosUnsafe((float)visual.Rotation.Xy.Degrees)); // x
                    }
                }

                if (breakFactor != 0.0)
                {
                    if (Math.Abs(breakFactor) >= 1.0F)
                    {
                        visual.Chip(i, breakFactor);
                    }

                    if (visual.Mesh.Polys[i].PolyType != PolyType.Glass)
                    {
                        visual.Mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                        if (visual.Bfase[i] > 20 && saturation > 0.25)
                        {
                            saturation = 0.25f;
                        }

                        if (visual.Bfase[i] > 25 && brightness > 0.7)
                        {
                            brightness = 0.7f;
                        }

                        if (visual.Bfase[i] > 30 && saturation > 0.15)
                        {
                            saturation = 0.15f;
                        }

                        if (visual.Bfase[i] > 35 && brightness > 0.6)
                        {
                            brightness = 0.6f;
                        }

                        if (visual.Bfase[i] > 40)
                        {
                            hue = 0.075f;
                        }

                        if (visual.Bfase[i] > 50 && brightness > 0.5)
                        {
                            brightness = 0.5f;
                        }

                        if (visual.Bfase[i] > 60)
                        {
                            hue = 0.05f;
                        }

                        visual.Bfase[i] += Math.Abs(breakFactor);
                        visual.Mesh.Polys[i] = visual.Mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                    }
                }
            }

            visual.Mesh.RebuildMesh();
        }
    }

    public static void DamageY(
        CarStats stat,
        BackendCar car,
        CarVisual visual,
        int wheelIdx,
        float damageFactor,
        bool mtouch,
        ref int nbsq,
        ref int squash
    )
    {
        var wheel = car.Wheels[wheelIdx];

        damageFactor *= (float)stat.Dammult;
        if (Math.Abs(damageFactor) > 100.0f)
        {
            if (damageFactor > 100.0f)
            {
                damageFactor -= 100.0f;
            }

            if (damageFactor < -100.0f)
            {
                damageFactor += 100.0f;
            }

            var flipZy = 0;
            var flipXy = 0;
            var zy = visual.Rotation.Zy.Degrees;
            var xy = visual.Rotation.Xy.Degrees;
            for ( /**/; zy < 360; zy += 360)
            {
            }

            for ( /**/; zy > 360; zy -= 360)
            {
            }

            if (zy < 210 && zy > 150)
            {
                flipZy = -1;
            }

            if (zy > 330 || zy < 30)
            {
                flipZy = 1;
            }

            for ( /**/; xy < 360; xy += 360)
            {
            }

            for ( /**/; xy > 360; xy -= 360)
            {
            }

            if (xy < 210 && xy > 150)
            {
                flipXy = -1;
            }

            if (xy > 330 || xy < 30)
            {
                flipXy = 1;
            }

            if (flipXy * flipZy == 0 || mtouch)
            {
                for (var i = 0; i < visual.Mesh.Polys.Length; i++)
                {
                    var breakFactor = 0.0f;
                    for (var j = 0; j < visual.Mesh.Polys[i].Points.Length; j++)
                    {
                        if (UMath.Py(
                                (float)wheel.Position.X,
                                visual.Mesh.Polys[i].Points[j].X, // x
                                (float)wheel.Position.Z,
                                visual.Mesh.Polys[i].Points[j].Z // z
                            ) < stat.Clrad)
                        {
                            breakFactor = damageFactor / 20.0f * Random.Single();
                            visual.Mesh.Polys[i].Points[j].Z += breakFactor * UMath.SinUnsafe((float)zy); // z
                            visual.Mesh.Polys[i].Points[j].X -= breakFactor * UMath.SinUnsafe((float)xy); // x
                        }
                    }

                    if (breakFactor != 0.0F)
                    {
                        if (Math.Abs(breakFactor) >= 1.0F)
                        {
                            visual.Chip(i, breakFactor);
                        }

                        if (visual.Mesh.Polys[i].PolyType != PolyType.Glass)
                        {
                            visual.Mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                            if (visual.Bfase[i] > 20 && saturation > 0.25)
                            {
                                saturation = 0.25f;
                            }

                            if (visual.Bfase[i] > 25 && brightness > 0.7)
                            {
                                brightness = 0.7f;
                            }

                            if (visual.Bfase[i] > 30 && saturation > 0.15)
                            {
                                saturation = 0.15f;
                            }

                            if (visual.Bfase[i] > 35 && brightness > 0.6)
                            {
                                brightness = 0.6f;
                            }

                            if (visual.Bfase[i] > 40)
                            {
                                hue = 0.075f;
                            }

                            if (visual.Bfase[i] > 50 && brightness > 0.5)
                            {
                                brightness = 0.5f;
                            }

                            if (visual.Bfase[i] > 60)
                            {
                                hue = 0.05f;
                            }

                            visual.Bfase[i] += Math.Abs(breakFactor);
                            visual.Mesh.Polys[i] = visual.Mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                        }
                    }
                }
            }

            if (flipXy * flipZy == 1)
            {
                if (nbsq > 0)
                {
                    var totalDmg = 0f;
                    var damagedPts = 1;
                    for (var i = 0; i < visual.Mesh.Polys.Length; i++)
                    {
                        var polyDmg = 0.0f;
                        for (var j = 0; j < visual.Mesh.Polys[i].Points.Length; j++)
                        {
                            polyDmg = damageFactor / 15.0f * Random.Single();
                            if ((
                                    Math.Abs(visual.Mesh.Polys[i].Points[j].Y /* y */ - stat.Flipy - squash) <
                                    stat.Msquash * 3 ||
                                    visual.Mesh.Polys[i].Points[j].Y /* y */ < stat.Flipy + squash
                                ) && squash < stat.Msquash)
                            {
                                visual.Mesh.Polys[i].Points[j].Y /* y */ += polyDmg;
                                totalDmg += polyDmg;
                                damagedPts++;
                            }
                        }

                        if (visual.Mesh.Polys[i].PolyType != PolyType.Glass && polyDmg != 0.0f)
                        {
                            visual.Bfase[i] += polyDmg;
                        }

                        if (Math.Abs(polyDmg) >= 1.0)
                        {
                            visual.Chip(i, polyDmg);
                        }
                    }

                    squash += (int)(totalDmg / damagedPts);
                    nbsq = 0;
                }
                else
                {
                    nbsq++;
                }
            }

            visual.Mesh.RebuildMesh();
        }
    }

    public static void DamageZ(
        CarStats stat,
        BackendCar car,
        CarVisual visual,
        int wheelIdx,
        float damageFactor
    )
    {
        var wheel = car.Wheels[wheelIdx];

        damageFactor *= (float)stat.Dammult;
        if (Math.Abs(damageFactor) > 100.0f)
        {
            if (damageFactor > 100.0f)
            {
                damageFactor -= 100.0f;
            }

            if (damageFactor < -100.0f)
            {
                damageFactor += 100.0f;
            }

            for (var i = 0; i < visual.Mesh.Polys.Length; i++)
            {
                var breakFactor = 0.0f;
                for (var j = 0; j < visual.Mesh.Polys[i].Points.Length; j++)
                {
                    if (UMath.Py(
                            (float)wheel.Position.X,
                            visual.Mesh.Polys[i].Points[j].X, // x
                            (float)wheel.Position.Z,
                            visual.Mesh.Polys[i].Points[j].Z // z
                        ) < stat.Clrad)
                    {
                        breakFactor = damageFactor / 20.0f * Random.Single();
                        visual.Mesh.Polys[i].Points[j].Z += breakFactor * UMath.CosUnsafe((float)visual.Rotation.Xz.Degrees) *
                                                     UMath.CosUnsafe((float)visual.Rotation.Zy.Degrees); // z
                        visual.Mesh.Polys[i].Points[j].X += breakFactor * UMath.SinUnsafe((float)visual.Rotation.Xz.Degrees) *
                                                     UMath.CosUnsafe((float)visual.Rotation.Xy.Degrees); // x
                    }
                }

                if (breakFactor != 0.0F)
                {
                    if (Math.Abs(breakFactor) >= 1.0F)
                    {
                        visual.Chip(i, breakFactor);
                    }

                    if (visual.Mesh.Polys[i].PolyType != PolyType.Glass)
                    {
                        visual.Mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                        if (visual.Bfase[i] > 20 && saturation > 0.25)
                        {
                            saturation = 0.25f;
                        }

                        if (visual.Bfase[i] > 25 && brightness > 0.7f)
                        {
                            brightness = 0.7f;
                        }

                        if (visual.Bfase[i] > 30 && saturation > 0.15f)
                        {
                            saturation = 0.15f;
                        }

                        if (visual.Bfase[i] > 35 && brightness > 0.6f)
                        {
                            brightness = 0.6f;
                        }

                        if (visual.Bfase[i] > 40)
                        {
                            hue = 0.075f;
                        }

                        if (visual.Bfase[i] > 50 && brightness > 0.5f)
                        {
                            brightness = 0.5f;
                        }

                        if (visual.Bfase[i] > 60)
                        {
                            hue = 0.05f;
                        }

                        visual.Bfase[i] += Math.Abs(breakFactor);
                        visual.Mesh.Polys[i] = visual.Mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                    }
                }
            }

            visual.Mesh.RebuildMesh();
        }
    }
}