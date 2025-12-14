using NFMWorld.Util;
using Random = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

public static class MeshDamage
{
    public static void DamageX(
        Stat stat,
        Mesh mesh,
        int wheelIdx,
        float damageFactor
    )
    {
        var wheel = mesh.Wheels[wheelIdx];
        
        damageFactor *= stat.Dammult;
        if (Math.Abs(damageFactor) > 100.0f) {
            if (damageFactor > 100.0f) {
                damageFactor -= 100.0f;
            }
            if (damageFactor < -100.0f) {
                damageFactor += 100.0f;
            }
            for (var i = 0; i < mesh.Polys.Length; i++) {
                var breakFactor = 0.0f;
                for (var j = 0; j < mesh.Polys[i].Points.Length; j++) {
                    if (UMath.Py(
                        wheel.Position.X,
                        mesh.Polys[i].Points[j].X, // x
                        wheel.Position.Z,
                        mesh.Polys[i].Points[j].Z // z
                    ) < stat.Clrad) {
                        breakFactor = damageFactor / 20.0f * Random.Single();
                        mesh.Polys[i].Points[j].Z -= breakFactor * UMath.Sin(mesh.Rotation.Xz.Degrees) * UMath.Cos(mesh.Rotation.Zy.Degrees); // z
                        mesh.Polys[i].Points[j].X += breakFactor * UMath.Cos(mesh.Rotation.Xz.Degrees) * UMath.Cos(mesh.Rotation.Xy.Degrees); // x
                    }
                }
                if (breakFactor > 0.0) {
                    if (mesh.Polys[i].PolyType != PolyType.Glass) {
                        mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                        if (mesh.Bfase[i] > 20 && saturation > 0.25) {
                            saturation = 0.25f;
                        }
                        if (mesh.Bfase[i] > 25 && brightness > 0.7) {
                            brightness = 0.7f;
                        }
                        if (mesh.Bfase[i] > 30 && saturation > 0.15) {
                            saturation = 0.15f;
                        }
                        if (mesh.Bfase[i] > 35 && brightness > 0.6) {
                            brightness = 0.6f;
                        }
                        if (mesh.Bfase[i] > 40) {
                            hue = 0.075f;
                        }
                        if (mesh.Bfase[i] > 50 && brightness > 0.5) {
                            brightness = 0.5f;
                        }
                        if (mesh.Bfase[i] > 60) {
                            hue = 0.05f;
                        }
                        mesh.Bfase[i] += Math.Abs(breakFactor);
                        mesh.Polys[i] = mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                    }
                }
            }
            mesh.RebuildMesh();
        }
    }

    public static void DamageY(
        Stat stat,
        Mesh mesh,
        int wheelIdx,
        float damageFactor,
        
        bool mtouch,
        ref int nbsq,
        ref int squash
    ) {
        var wheel = mesh.Wheels[wheelIdx];

        damageFactor *= stat.Dammult;
        if (Math.Abs(damageFactor) > 100.0f) {
            if (damageFactor > 100.0f) {
                damageFactor -= 100.0f;
            }
            if (damageFactor < -100.0f) {
                damageFactor += 100.0f;
            }

            var flipZy = 0;
            var flipXy = 0;
            var zy = mesh.Rotation.Zy.Degrees;
            var xy = mesh.Rotation.Xy.Degrees;
            for (/**/; zy < 360; zy += 360) {

            }
            for (/**/; zy > 360; zy -= 360) {

            }
            if (zy < 210 && zy > 150) {
                flipZy = -1;
            }
            if (zy > 330 || zy < 30) {
                flipZy = 1;
            }
            for (/**/; xy < 360; xy += 360) {

            }
            for (/**/; xy > 360; xy -= 360) {

            }
            if (xy < 210 && xy > 150) {
                flipXy = -1;
            }
            if (xy > 330 || xy < 30) {
                flipXy = 1;
            }

            if (flipXy * flipZy == 0 || mtouch) {
                for (var i = 0; i < mesh.Polys.Length; i++) {
                    var breakFactor = 0.0f;
                    for (var j = 0; j < mesh.Polys[i].Points.Length; j++) {
                        if (UMath.Py(
                            wheel.Position.X,
                            mesh.Polys[i].Points[j].X, // x
                            wheel.Position.Z,
                            mesh.Polys[i].Points[j].Z // z
                        ) < stat.Clrad) {
                            breakFactor = damageFactor / 20.0f * Util.Random.Single();
                            mesh.Polys[i].Points[j].Z += breakFactor * UMath.Sin(zy); // z
                            mesh.Polys[i].Points[j].X -= breakFactor * UMath.Sin(xy); // x
                        }
                    }
                    if (breakFactor > 0.0) {
                        if (mesh.Polys[i].PolyType != PolyType.Glass) {
                            mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                            if (mesh.Bfase[i] > 20 && saturation > 0.25) {
                                saturation = 0.25f;
                            }
                            if (mesh.Bfase[i] > 25 && brightness > 0.7) {
                                brightness = 0.7f;
                            }
                            if (mesh.Bfase[i] > 30 && saturation > 0.15) {
                                saturation = 0.15f;
                            }
                            if (mesh.Bfase[i] > 35 && brightness > 0.6) {
                                brightness = 0.6f;
                            }
                            if (mesh.Bfase[i] > 40) {
                                hue = 0.075f;
                            }
                            if (mesh.Bfase[i] > 50 && brightness > 0.5) {
                                brightness = 0.5f;
                            }
                            if (mesh.Bfase[i] > 60) {
                                hue = 0.05f;
                            }
                            mesh.Bfase[i] += Math.Abs(breakFactor);
                            mesh.Polys[i] = mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                        }
                    }
                }
            }
            if (flipXy * flipZy == 1) {
                if (nbsq > 0) {
                    var totalDmg = 0f;
                    var damagedPts = 1;
                    for (var i = 0; i < mesh.Polys.Length; i++) {
                        var polyDmg = 0.0f;
                        for (var j = 0; j < mesh.Polys[i].Points.Length; j++) {
                            polyDmg = damageFactor / 15.0f * Random.Single();
                            if ((
                                Math.Abs(mesh.Polys[i].Points[j].Y /* y */ - stat.Flipy - squash) < stat.Msquash * 3 ||
                                mesh.Polys[i].Points[j].Y /* y */ < stat.Flipy + squash
                            ) && squash < stat.Msquash) {
                                mesh.Polys[i].Points[j].Y /* y */ += polyDmg;
                                totalDmg += polyDmg;
                                damagedPts++;
                            }
                        }
                        if (mesh.Polys[i].PolyType != PolyType.Glass && polyDmg != 0.0f) {
                            mesh.Bfase[i] += polyDmg;
                        }
                        // if (Math.Abs(f108) >= 1.0) {
                        //     conto.p[i].chip = 1;
                        //     conto.p[i].ctmag = f108;
                        // }
                    }
                    squash += (int)(totalDmg / damagedPts);
                    nbsq = 0;
                } else {
                    nbsq++;
                }
            }
            mesh.RebuildMesh();
        }
    }
    
    public static void DamageZ(
        Stat stat,
        Mesh mesh,
        int wheelIdx,
        float damageFactor
    ) {
        var wheel = mesh.Wheels[wheelIdx];

        damageFactor *= stat.Dammult;
        if (Math.Abs(damageFactor) > 100.0f) {
            if (damageFactor > 100.0f) {
                damageFactor -= 100.0f;
            }
            if (damageFactor < -100.0f) {
                damageFactor += 100.0f;
            }
            for (var i = 0; i < mesh.Polys.Length; i++) {
                var breakFactor = 0.0f;
                for (var j = 0; j < mesh.Polys[i].Points.Length; j++) {
                    if (UMath.Py(
                        wheel.Position.X,
                        mesh.Polys[i].Points[j].X, // x
                        wheel.Position.Z,
                        mesh.Polys[i].Points[j].Z // z
                    ) < stat.Clrad) {
                        breakFactor = damageFactor / 20.0f * Util.Random.Single();
                        mesh.Polys[i].Points[j].Z += breakFactor * UMath.Cos(mesh.Rotation.Xz.Degrees) * UMath.Cos(mesh.Rotation.Zy.Degrees); // z
                        mesh.Polys[i].Points[j].X += breakFactor * UMath.Sin(mesh.Rotation.Xz.Degrees) * UMath.Cos(mesh.Rotation.Xy.Degrees); // x
                    }
                }
                if (breakFactor > 0.0) {
                    if (mesh.Polys[i].PolyType != PolyType.Glass) {
                        mesh.Polys[i].Color.ToHSB(out var hue, out var saturation, out var brightness);
                        if (mesh.Bfase[i] > 20 && saturation > 0.25) {
                            saturation = 0.25f;
                        }
                        if (mesh.Bfase[i] > 25 && brightness > 0.7f) {
                            brightness = 0.7f;
                        }
                        if (mesh.Bfase[i] > 30 && saturation > 0.15f) {
                            saturation = 0.15f;
                        }
                        if (mesh.Bfase[i] > 35 && brightness > 0.6f) {
                            brightness = 0.6f;
                        }
                        if (mesh.Bfase[i] > 40) {
                            hue = 0.075f;
                        }
                        if (mesh.Bfase[i] > 50 && brightness > 0.5f) {
                            brightness = 0.5f;
                        }
                        if (mesh.Bfase[i] > 60) {
                            hue = 0.05f;
                        }
                        mesh.Bfase[i] += Math.Abs(breakFactor);
                        mesh.Polys[i] = mesh.Polys[i] with { Color = Color3.FromHSB(hue, saturation, brightness) };
                    }
                }
            }
            mesh.RebuildMesh();
        }
    }
}