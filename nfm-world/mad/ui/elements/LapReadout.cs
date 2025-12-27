using NFMWorld.Util;
using NFMWorld.Mad.UI.yoga;
using NFMWorld.DriverInterface;

namespace NFMWorld.Mad.UI.Elements;

public class LapReadout : Node
{
    public int CurrentLap;
    public int StageLaps;
    public static IImage LapImage = IBackend.Backend.LoadImage(new Util.File("data/images/lap.gif"));

    public override void RenderContent(Vector2 position, Vector2 size)
    {
        G.DrawImage(LapImage, (int)position.X, (int)position.Y, (int)Width.InternalValue.value, (int)Height.InternalValue.value);
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringStroke(CurrentLap + "/" + StageLaps, (int)position.X + LapImage.Width, (int)position.Y + LapImage.Height);
        G.SetColor(new Color(255, 255, 255));
        G.DrawString(CurrentLap + "/" + StageLaps, (int)position.X + LapImage.Width, (int)position.Y + LapImage.Height);
    }
}