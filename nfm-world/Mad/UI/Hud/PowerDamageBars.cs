using NFMWorldLibrary;
using WorldXaml.UI.Yoga;

namespace NFMWorld.UI.Hud;

public partial class PowerDamageBars : Box
{
    public PowerDamageBars()
    {
        InitializeComponent();
        PowerBar.Color = GetPowerColor(1f);
        DamageBar.Color = GetDamageColor(0f);
    }
    public void SetDamageBarFill(int hitmag, int maxmag)
    {
        float dmgfill = (float)hitmag / maxmag;
        dmgfill = Math.Min(1f, dmgfill);
        DamageBar.FillAmount = dmgfill;
    }
    public void UpdateDamageBarColor()
    {
        DamageBar.Color = GetDamageColor(DamageBar.FillAmount);
    }
    public void SetPowerBarFill(float power)
    {
        PowerBar.FillAmount = power / 100f;
    }
    public void UpdatePowerBarColor()
    {
        PowerBar.Color = GetPowerColor(PowerBar.FillAmount);
    }
    public void EventPowerUp(object? sender, float f)
    {
        _powerFlickerTicks = (int)(45*(1/Physics.PHYSICS_MULTIPLIER));
    }

    public void Reset()
    {
        SetPowerBarFill(98f);
        SetDamageBarFill(0, 1);
        _damageFlickerTicks = 0;
        _damageFlicker = false;
        _damageFlickerInnerTicks = 0;
        _powerFlicker = false;
        _powerFlickerTicks = 0;
        _powerFlickerInnerTicks = 0;
        UpdateDamageBarColor();
        UpdatePowerBarColor();
    }

    private static int _damageFlickerTicks = 0;
    private static int _damageFlickerInnerTicks = 0;
    private static bool _damageFlicker = false;

    public static Color GetDamageColor(float fill)
    {
        float cmp = 98f * fill;
        int red = 244;
        int green = 244;
        int blue = 11;

        if (cmp > 33)
            green = (int) (244F - 233F * ((cmp - 33) / 65F));

        /* Handle damage flicker when high - complicated to handle all the tick differences!!! */
        if(cmp > 70)
        {
            if(_damageFlickerTicks < 10*(1/Physics.PHYSICS_MULTIPLIER))
            {
                if(_damageFlickerInnerTicks > (int)(1/Physics.PHYSICS_MULTIPLIER) && _damageFlicker)
                {
                    green = 170;
                    _damageFlicker = false;
                    _damageFlickerInnerTicks = 0;
                } else if (_damageFlickerInnerTicks > (int)(1/Physics.PHYSICS_MULTIPLIER))
                {
                    _damageFlicker = true;
                    _damageFlickerInnerTicks = 0;
                }
                _damageFlickerInnerTicks++;
            } else
            {
                _damageFlickerInnerTicks = 0;
            }
            _damageFlickerTicks++;
            if(_damageFlickerTicks > (167*(1/Physics.PHYSICS_MULTIPLIER)) - cmp * 1.5f) _damageFlickerTicks = 0;
        }

        red = (int)(red + red * (World.Snap[0] / 100F));
        if (red > 255)
            red = 255;
        if (red < 0)
            red = 0;

        green = (int)(green + green * (World.Snap[1] / 100F));
        if (green > 255)
            green = 255;
        if (green < 0)
            green = 0;

        blue = (int)(blue + blue * (World.Snap[2] / 100F));
        if (blue > 255)
            blue = 255;
        if (blue < 0)
            blue = 0;

        return new Color(red, green, blue);
    }

    public static int _powerFlickerTicks = 0;
    private static int _powerFlickerInnerTicks = 0;
    private static bool _powerFlicker = false;
    public static Color GetPowerColor(float fill)
    {
        fill *= 100;

        int red = 128;
        if(fill == 98) red = 64;
        
        int green = (int)(190 + fill * 0.37);
        int blue = 244;

        if(_powerFlickerTicks > 0 && _powerFlickerInnerTicks > (1/Physics.PHYSICS_MULTIPLIER) && _powerFlicker)
        {
            red = 128;
            green = 244;
            blue = 244;
            _powerFlickerInnerTicks = 0;
            _powerFlicker = false;
        } else if(_powerFlickerTicks > 0 && _powerFlickerInnerTicks > (1/Physics.PHYSICS_MULTIPLIER) && !_powerFlicker)
        {
            _powerFlicker = true;
        } else if(_powerFlickerTicks <= 0)
        {
            _powerFlicker = false;
            _powerFlickerInnerTicks = 0;
        }
        _powerFlickerTicks--;
        _powerFlickerInnerTicks++;

        red = (int) (red + red * (World.Snap[0] / 100F));
        if (red > 255) {
            red = 255;
        }
        if (red < 0) {
            red = 0;
        }
        green = (int) (green + green * (World.Snap[1] / 100F));
        if (green > 255) {
            green = 255;
        }
        if (green < 0) {
            green = 0;
        }
        blue = (int) (blue + blue * (World.Snap[2] / 100F));
        if (blue > 255) {
            blue = 255;
        }
        if (blue < 0) {
            blue = 0;
        }

        return new Color(red, green, blue);
    }
}