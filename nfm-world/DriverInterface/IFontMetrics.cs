namespace NFMWorld.DriverInterface;

public interface IFontMetrics
{
    public float StringWidth(string astring);
    public float Height(string astring);
}