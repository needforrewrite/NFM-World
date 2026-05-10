using NFMWorld.DriverInterface;
using WorldXaml.UI.Yoga;

namespace NFMWorld.UI;

public class Image : Node
{
    public IImage? ImageData
    {
        get;
        set
        {
            field = value;
            if (Width.Unit is YgUnit.Undefined or YgUnit.Point or YgUnit.Auto)
            {
                Width = Scale * field?.Width ?? 0;
            }
            if (Height.Unit is YgUnit.Undefined or YgUnit.Point or YgUnit.Auto)
            {
                Height = Scale * field?.Height ?? 0;
            }
        }
    }

    public float Scale
    {
        get;
        set
        {
            field = value;
            if (Width.PointValue is {} widthValue)
            {
                Width = (int)(field * widthValue);
            }
            if (Height.PointValue is {} heightValue)
            {
                Height = (int)(field * heightValue);
            }
        }
    } = 1f;

    protected override void RenderContent(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        if(ImageData != null)
        {
            G.DrawImage(ImageData, (int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
        }
    }
}