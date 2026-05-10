using NFMWorld.Util;
using WorldXaml.UI.Yoga;

namespace NFMWorld.UI;

public class GarageDynamicStatBar : Node
{
    private const float maxSpeed = 1000f;
    private const float speedUp = 0.1f;
    private const int fullBar = 100;

    public int BarMaxWidth
    {
        get;
        set
        {
            field = value;
            Width = value;
        }
    }

    public int BarHeight
    {
        get;
        set
        {
            field = value;
            Height = value + 28;
        }
    }

    private float currentValue = 0f;

    public float TargetValue
    {
        private get;
        set => field = value * 100f;
    }

    private float speed = speedUp;
    
    public string StatName { get; set; }

    private Color[] barColors =
    [
        new Color(255, 0, 0),
        new Color(128, 128, 128),
        new Color(255, 128, 0),
        new Color(128, 128, 128),
        new Color(255, 255, 0),
        new Color(128, 128, 128),
        new Color(128, 255, 0),
        new Color(128, 128, 128),
        new Color(0, 255, 0),
        new Color(128, 128, 128),
        new Color(0, 255, 128),
        new Color(128, 128, 128),
        new Color(0, 255, 255),
        new Color(128, 128, 128),
        new Color(0, 128, 255),
        new Color(128, 128, 128),
        new Color(0, 0, 255),
        new Color(128, 128, 128),
        new Color(128, 0, 255),
        new Color(128, 128, 128),
        new Color(255, 0, 255),
        new Color(128, 128, 128),
        new Color(255, 0, 128),
        new Color(128, 128, 128),
    ];

    public GarageDynamicStatBar()
    {
        BarMaxWidth = 100;
        BarHeight = 10;
        StatName = "Unknown Stat";
    }

    protected override void GameTick()
    {
        currentValue += speed;
        currentValue = Math.Min(TargetValue, currentValue);

        speed += speedUp;
        speed = Math.Min(speed, maxSpeed);
    }

    private int GetColor(int lim, int i)
    {
        if (i < 0)
        {
            return i % lim + lim;
        }
        else
        {
            return i % lim;
        }
    }

    protected override void RenderContent(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        var x = (int)position.X;
        var y = (int)position.Y;
        
        int multiples = 0;
        float remaining = currentValue;

        while (remaining > fullBar)
        {
            remaining -= fullBar;
            multiples++;
        }

        G.SetColor(new Color(0, 0, 0));
        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Bold, 20));
        G.DrawStringStroke(StatName, x, y - 5);
        G.SetColor(new Color(255, 255, 255));
        G.DrawString(StatName, x, y - 5);

        Color baseBarColorStart = multiples > 0 ? barColors[GetColor(barColors.Length, multiples - 1)] : new Color(0, 0, 0, 0);
        Color baseBarColorEnd = multiples > 0 ? barColors[GetColor(barColors.Length, multiples)] : new Color(0, 0, 0, 0);

        Color barColorStart = barColors[GetColor(barColors.Length, multiples)];
        Color barColorEnd = barColors[GetColor(barColors.Length, multiples + 1)];

        G.SetLinearGradient(x, y, BarMaxWidth, BarHeight, [baseBarColorStart, baseBarColorEnd], null);
        G.FillRect(x, y, BarMaxWidth, BarHeight);

        int barRatio = (int)(remaining / fullBar * 100);
        barRatio *= BarMaxWidth / fullBar;

        G.SetLinearGradient(x, y, BarMaxWidth, BarHeight, [barColorStart, barColorEnd], null);
        G.FillRect(x, y, barRatio, BarHeight);

        G.SetColor(new Color(255, 255, 255));
        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Bold, 12));
        G.DrawString(((int)currentValue).ToString(), x + 5, y + BarHeight);
        
        DrawDividers(x, y);
    }

    // Draw the black thing that overlays the stat itself...
    private void DrawDividers(int x, int y)
    {
        G.SetColor(new Color(0, 0, 0));
        G.DrawLine(x, y + BarHeight, x + BarMaxWidth, y + BarHeight);
        G.DrawLine(x, y, x, y + BarHeight);
        G.DrawLine(x + BarMaxWidth, y, x + BarMaxWidth, y + BarHeight);
    }
}