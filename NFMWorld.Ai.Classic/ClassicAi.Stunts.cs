using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Ai.Classic;

public sealed partial class ClassicAi
{
    private void FlyStunts(BackendCar car)
    {
        switch (_driver.StuntPhase)
        {
            case StuntPhase.Takeoff:
                TakeOff(car);
                break;
            case StuntPhase.Flipping:
                FlyAndWaitForLanding(car);
                break;
            case StuntPhase.Landing:
                Land(car);
                break;
        }
    }

    /// <summary>Decides on a stunt from how hard the car left the ground.</summary>
    private void TakeOff(BackendCar car)
    {
        var mad = car.CarPhysics;
        var height = World.Ground - car.Position.Y;

        // Scy is positive downwards (gravity adds to it) and the original's wheelSpeedY
        // positive upwards: negate it, or the lift is always negative and no bot ever jumps.
        var lift = (-mad.SummedWheelVelocity.Y * (height + 5)) / 40;

        if (lift > (fix64)7 && (Sim() > _driver.StuntCaution / 3))
        {
            _driver.TakeoffXy = mad.Pxy;
            _driver.TakeoffZy = mad.Pzy;
            _driver.AirTicks = 0;
            _driver.Flip = 0;
            _driver.Roll = 0;
            _driver.FlipReverses = false;
            _driver.RollReverses = false;
            _driver.StuntPhase = StuntPhase.Flipping;
            // The engine only enters a stunt (`StuntState` 1 -> 2) on a handbrake press
            // while airborne, and the direction keys do nothing until it has.
            car.Control.Handb = true;
            ChooseFlipsAndRolls(lift);
        }
        else
        {
            _driver.StuntPhase = StuntPhase.None;
        }

        _driver.Afta = true;

        if (_driver.Fixing == Fix.Close)
        {
            _driver.Fixing = Fix.Jumping;
            _driver.RethinkAfter += 30;
        }
    }

    /// <summary>Chooses between a flip and a roll for this jump, with the usual sloppiness.</summary>
    private void ChooseFlipsAndRolls(fix64 lift)
    {
        if (lift < (fix64)16)
        {
            _driver.Flip = -1;
            _driver.FlipAfter = 0;
            _driver.FlipReverses = false;
            return;
        }

        if (Sim() > Sim())
        {
            _driver.Flip = (Sim() > Sim()) ? -1 : 1;
            _driver.FlipAfter = (int)FixedMath.Floor((fix64)10 * Sim() * _driver.StuntCaution);
            if (Sim() > (fix64)0.85f)
                _driver.FlipReverses = true;

            if (Sim() > _driver.StuntCaution + (fix64)0.3f)
            {
                _driver.Roll = (Sim() > Sim()) ? -1 : 1;
                _driver.RollAfter = (int)FixedMath.Floor((fix64)30 * Sim());
                if (Sim() > (fix64)0.75f)
                    _driver.RollReverses = true;
            }
        }
        else
        {
            _driver.Roll = (Sim() > Sim()) ? -1 : 1;
            _driver.RollAfter = (int)FixedMath.Floor((fix64)10 * Sim() * _driver.StuntCaution);
            if (Sim() > (fix64)0.75f)
                _driver.RollReverses = true;

            if (Sim() > _driver.StuntCaution + (fix64)0.3f)
            {
                _driver.Flip = (Sim() > Sim()) ? -1 : 1;
                _driver.FlipAfter = (int)FixedMath.Floor((fix64)30 * Sim());
                if (Sim() > (fix64)0.85f)
                    _driver.FlipReverses = true;
            }
        }
    }

    /// <summary>Keeps flipping and rolling, then watches for the ground.</summary>
    private void FlyAndWaitForLanding(BackendCar car)
    {
        var mad = car.CarPhysics;
        var control = car.Control;
        _driver.AirTicks++;

        // Handbrake is what puts the car in a stunt and keeps it there: releasing it mid-air
        // drops StuntState back to 0 and the direction keys stop doing anything.
        control.Handb = true;

        if (_driver.Roll != 0 && _driver.AirTicks > _driver.RollAfter)
        {
            if (_driver.RollReverses && FixedMath.Abs(mad.Pxy - _driver.TakeoffXy) > (fix64)180)
            {
                _driver.Roll = -_driver.Roll;
                _driver.RollReverses = false;
            }

            control.Handb = true;
            if (_driver.Roll == -1)
                control.Left = true;
            else
                control.Right = true;
        }

        if (_driver.Flip != 0 && _driver.AirTicks > _driver.FlipAfter)
        {
            if (_driver.FlipReverses && FixedMath.Abs(mad.Pzy - _driver.TakeoffZy) > (fix64)180)
            {
                _driver.Flip = -_driver.Flip;
                _driver.FlipReverses = false;
            }

            control.Handb = true;
            if (_driver.Flip == -1)
                control.Down = true;
            else
                control.Up = true;
        }

        // Scy points down, so unlike the original's wheelSpeedY a fall is positive here.
        var height = World.Ground - car.Position.Y;
        var fall = (mad.SummedWheelVelocity.Y * (fix64)100) / (height + 5);
        if (fall <= _driver.LandingMargin)
            return;

        _driver.PressedUp = false;
        _driver.PressedDown = false;
        _driver.PressedLeft = false;
        _driver.PressedRight = false;
        _driver.RollSettled = false;
        _driver.PitchSettled = false;
        _driver.StraightenPitch = false;
        _driver.StraightenRoll = false;
        _driver.StuntPhase = StuntPhase.Landing;
        _driver.LandingStep = 0;
    }

    private void Land(BackendCar car)
    {
        var mad = car.CarPhysics;

        if (_driver.LandingStep == 0)
        {
            if (mad.UpComponent != fix64.Zero || mad.DownComponent != fix64.Zero)
                _driver.StraightenPitch = true;
            if (mad.LeftComponent != fix64.Zero || mad.RightComponent != fix64.Zero)
                _driver.StraightenRoll = true;
            _driver.LandingStep = 1;
        }

        if (mad.Wtouch)
        {
            if (_driver.LandingStep == 1)
                _driver.LandingStep = 2;
        }
        else if (_driver.LandingStep == 2)
        {
            if (mad.BadLanding && Sim() > _driver.LandingSloppiness)
            {
                var pitch = _driver.StraightenPitch;
                _driver.StraightenPitch = _driver.StraightenRoll;
                _driver.StraightenRoll = pitch;
            }

            _driver.LandingStep = 3;
        }

        if (_driver.StraightenPitch)
            StraightenPitch(car);
        if (_driver.StraightenRoll)
            StraightenRoll(car);
    }

    /// <summary>Levels the car out on the pitch axis before the wheels come down.</summary>
    private void StraightenPitch(BackendCar car)
    {
        var mad = car.CarPhysics;
        var control = car.Control;

        // Up raises the original's pitch but lowers Pzy (the engine adds down - up), so the
        // angle is negated to compare like the original; the key names still map directly.
        var pitch = FixedMath.Abs(Normalize(-mad.Pzy + (fix64)90));

        if (FixedMath.Abs(mad.LeftComponent - mad.RightComponent) < (fix64)5
            && (_driver.PressedDown || _driver.PressedUp))
            _driver.PitchSettled = true;

        if (mad.DownComponent > mad.UpComponent)
        {
            if (mad.BadLanding)
            {
                if (_driver.PitchSettled)
                {
                    if (pitch < (fix64)90)
                        control.Up = true;
                    else
                        control.Down = true;
                }
                else if (!_driver.PressedDown)
                {
                    control.Down = true;
                }
            }
            else
            {
                if (_driver.PitchSettled)
                {
                    if (_driver.LandsPerfectly && FixedMath.Abs(pitch - (fix64)90) > (fix64)30)
                    {
                        if (pitch < (fix64)90)
                            control.Up = true;
                        else
                            control.Down = true;
                    }
                }
                else if (Sim() > _driver.LandingSloppiness)
                {
                    control.Up = true;
                }

                _driver.PressedDown = true;
            }
        }
        else
        {
            if (mad.BadLanding)
            {
                if (_driver.PitchSettled)
                {
                    if (pitch < (fix64)90)
                        control.Up = true;
                    else
                        control.Down = true;
                }
                else if (!_driver.PressedUp)
                {
                    control.Up = true;
                }
            }
            else
            {
                if (_driver.PitchSettled)
                {
                    if (_driver.LandsPerfectly && FixedMath.Abs(pitch - (fix64)90) > (fix64)30)
                    {
                        if (pitch < (fix64)90)
                            control.Up = true;
                        else
                            control.Down = true;
                    }
                }
                else if (Sim() > _driver.LandingSloppiness)
                {
                    control.Down = true;
                }

                _driver.PressedUp = true;
            }
        }
    }

    /// <summary>Levels the car out on the roll axis before the wheels come down.</summary>
    private void StraightenRoll(BackendCar car)
    {
        var mad = car.CarPhysics;
        var control = car.Control;

        // Pxy and the original's roll both move as "angle += right - left", so this is the
        // original's straightenRoll as written.
        var roll = mad.Pxy + (fix64)90;
        if (car.Control.Zyinv)
            roll += (fix64)180;
        roll = FixedMath.Abs(Normalize(roll));

        if (FixedMath.Abs(mad.LeftComponent - mad.RightComponent) < (fix64)10
            && (_driver.PressedLeft || _driver.PressedRight))
            _driver.RollSettled = true;

        if (mad.LeftComponent > mad.RightComponent)
        {
            if (mad.BadLanding)
            {
                if (_driver.RollSettled)
                {
                    if (roll > (fix64)90)
                        control.Left = true;
                    else
                        control.Right = true;
                }
                else if (!_driver.PressedLeft)
                {
                    control.Left = true;
                }
            }
            else
            {
                if (_driver.RollSettled)
                {
                    if (_driver.LandsPerfectly && FixedMath.Abs(roll - (fix64)90) > (fix64)30)
                    {
                        if (roll > (fix64)90)
                            control.Left = true;
                        else
                            control.Right = true;
                    }
                }
                else if (Sim() > _driver.LandingSloppiness)
                {
                    control.Right = true;
                }

                _driver.PressedLeft = true;
            }
        }
        else
        {
            if (mad.BadLanding)
            {
                if (_driver.RollSettled)
                {
                    if (roll > (fix64)90)
                        control.Left = true;
                    else
                        control.Right = true;
                }
                else if (!_driver.PressedRight)
                {
                    control.Right = true;
                }
            }
            else
            {
                if (_driver.RollSettled)
                {
                    if (_driver.LandsPerfectly && FixedMath.Abs(roll - (fix64)90) > (fix64)30)
                    {
                        if (roll > (fix64)90)
                            control.Left = true;
                        else
                            control.Right = true;
                    }
                }
                else if (Sim() > _driver.LandingSloppiness)
                {
                    control.Left = true;
                }

                _driver.PressedRight = true;
            }
        }
    }
}
