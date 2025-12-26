using NFMWorld.Util;
using Random = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

public static class MeshDamage
{
    public static void DamageX(
        CarStats stat,
        Car car,
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

            for (var i = 0; i < car.Mesh.Polys.Length; i++)
            {
                var breakFactor = 0.0f;
                for (var j = 0; j < car.Mesh.Polys[i].Points.Length; j++)
                {
                    if (UMath.Py(
                            (float)wheel.Position.X,
                            car.Mesh.Polys[i].Points[j].X, // x
                            (float)wheel.Position.Z,
                            car.Mesh.Polys[i].Points[j].Z // z
                        ) < stat.Clrad)
                    {
                        breakFactor = damageFactor / 20.0f * Random.Single();
                        car.Mesh.Polys[i].Points[j].Z -= breakFactor * UMath.Sin(car.Rotation.Xz.Degrees) *
                                                     UMath.Cos(car.Rotation.Zy.Degrees); // z
                        car.Mesh.Polys[i].Points[j].X += breakFactor * UMath.Cos(car.Rotation.Xz.Degrees) *
                                                     UMath.Cos(car.Rotation.Xy.Degrees); // x
                    }
                }

                if (breakFactor != 0.0)
                {
                    if (Math.Abs(breakFactor) >= 1.0F)
                    {
                        car.Chip(i, breakFactor);
                    }

                    if (car.Mesh.Polys[i].PolyType != PolyType.Glass)
                    {
                        car.Mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                        if (car.Bfase[i] > 20 && saturation > 0.25)
                        {
                            saturation = 0.25f;
                        }

                        if (car.Bfase[i] > 25 && brightness > 0.7)
                        {
                            brightness = 0.7f;
                        }

                        if (car.Bfase[i] > 30 && saturation > 0.15)
                        {
                            saturation = 0.15f;
                        }

                        if (car.Bfase[i] > 35 && brightness > 0.6)
                        {
                            brightness = 0.6f;
                        }

                        if (car.Bfase[i] > 40)
                        {
                            hue = 0.075f;
                        }

                        if (car.Bfase[i] > 50 && brightness > 0.5)
                        {
                            brightness = 0.5f;
                        }

                        if (car.Bfase[i] > 60)
                        {
                            hue = 0.05f;
                        }

                        car.Bfase[i] += Math.Abs(breakFactor);
                        car.Mesh.Polys[i] = car.Mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                    }
                }
            }

            car.Mesh.RebuildMesh();
        }
    }

    public static void DamageY(
        CarStats stat,
        Car car,
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
            var zy = car.Rotation.Zy.Degrees;
            var xy = car.Rotation.Xy.Degrees;
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
                for (var i = 0; i < car.Mesh.Polys.Length; i++)
                {
                    var breakFactor = 0.0f;
                    for (var j = 0; j < car.Mesh.Polys[i].Points.Length; j++)
                    {
                        if (UMath.Py(
                                (float)wheel.Position.X,
                                car.Mesh.Polys[i].Points[j].X, // x
                                (float)wheel.Position.Z,
                                car.Mesh.Polys[i].Points[j].Z // z
                            ) < stat.Clrad)
                        {
                            breakFactor = damageFactor / 20.0f * Random.Single();
                            car.Mesh.Polys[i].Points[j].Z += breakFactor * UMath.Sin(zy); // z
                            car.Mesh.Polys[i].Points[j].X -= breakFactor * UMath.Sin(xy); // x
                        }
                    }

                    if (breakFactor != 0.0F)
                    {
                        if (Math.Abs(breakFactor) >= 1.0F)
                        {
                            car.Chip(i, breakFactor);
                        }

                        if (car.Mesh.Polys[i].PolyType != PolyType.Glass)
                        {
                            car.Mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                            if (car.Bfase[i] > 20 && saturation > 0.25)
                            {
                                saturation = 0.25f;
                            }

                            if (car.Bfase[i] > 25 && brightness > 0.7)
                            {
                                brightness = 0.7f;
                            }

                            if (car.Bfase[i] > 30 && saturation > 0.15)
                            {
                                saturation = 0.15f;
                            }

                            if (car.Bfase[i] > 35 && brightness > 0.6)
                            {
                                brightness = 0.6f;
                            }

                            if (car.Bfase[i] > 40)
                            {
                                hue = 0.075f;
                            }

                            if (car.Bfase[i] > 50 && brightness > 0.5)
                            {
                                brightness = 0.5f;
                            }

                            if (car.Bfase[i] > 60)
                            {
                                hue = 0.05f;
                            }

                            car.Bfase[i] += Math.Abs(breakFactor);
                            car.Mesh.Polys[i] = car.Mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
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
                    for (var i = 0; i < car.Mesh.Polys.Length; i++)
                    {
                        var polyDmg = 0.0f;
                        for (var j = 0; j < car.Mesh.Polys[i].Points.Length; j++)
                        {
                            polyDmg = damageFactor / 15.0f * Random.Single();
                            if ((
                                    Math.Abs(car.Mesh.Polys[i].Points[j].Y /* y */ - stat.Flipy - squash) <
                                    stat.Msquash * 3 ||
                                    car.Mesh.Polys[i].Points[j].Y /* y */ < stat.Flipy + squash
                                ) && squash < stat.Msquash)
                            {
                                car.Mesh.Polys[i].Points[j].Y /* y */ += polyDmg;
                                totalDmg += polyDmg;
                                damagedPts++;
                            }
                        }

                        if (car.Mesh.Polys[i].PolyType != PolyType.Glass && polyDmg != 0.0f)
                        {
                            car.Bfase[i] += polyDmg;
                        }

                        if (Math.Abs(polyDmg) >= 1.0)
                        {
                            car.Chip(i, polyDmg);
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

            car.Mesh.RebuildMesh();
        }
    }

    public static void DamageZ(
        CarStats stat,
        Car car,
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

            for (var i = 0; i < car.Mesh.Polys.Length; i++)
            {
                var breakFactor = 0.0f;
                for (var j = 0; j < car.Mesh.Polys[i].Points.Length; j++)
                {
                    if (UMath.Py(
                            (float)wheel.Position.X,
                            car.Mesh.Polys[i].Points[j].X, // x
                            (float)wheel.Position.Z,
                            car.Mesh.Polys[i].Points[j].Z // z
                        ) < stat.Clrad)
                    {
                        breakFactor = damageFactor / 20.0f * Random.Single();
                        car.Mesh.Polys[i].Points[j].Z += breakFactor * UMath.Cos(car.Rotation.Xz.Degrees) *
                                                     UMath.Cos(car.Rotation.Zy.Degrees); // z
                        car.Mesh.Polys[i].Points[j].X += breakFactor * UMath.Sin(car.Rotation.Xz.Degrees) *
                                                     UMath.Cos(car.Rotation.Xy.Degrees); // x
                    }
                }

                if (breakFactor != 0.0F)
                {
                    if (Math.Abs(breakFactor) >= 1.0F)
                    {
                        car.Chip(i, breakFactor);
                    }

                    if (car.Mesh.Polys[i].PolyType != PolyType.Glass)
                    {
                        car.Mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                        if (car.Bfase[i] > 20 && saturation > 0.25)
                        {
                            saturation = 0.25f;
                        }

                        if (car.Bfase[i] > 25 && brightness > 0.7f)
                        {
                            brightness = 0.7f;
                        }

                        if (car.Bfase[i] > 30 && saturation > 0.15f)
                        {
                            saturation = 0.15f;
                        }

                        if (car.Bfase[i] > 35 && brightness > 0.6f)
                        {
                            brightness = 0.6f;
                        }

                        if (car.Bfase[i] > 40)
                        {
                            hue = 0.075f;
                        }

                        if (car.Bfase[i] > 50 && brightness > 0.5f)
                        {
                            brightness = 0.5f;
                        }

                        if (car.Bfase[i] > 60)
                        {
                            hue = 0.05f;
                        }

                        car.Bfase[i] += Math.Abs(breakFactor);
                        car.Mesh.Polys[i] = car.Mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                    }
                }
            }

            car.Mesh.RebuildMesh();
        }
    }
}