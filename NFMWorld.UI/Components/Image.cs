using Microsoft.UI.Reactor.Layout;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorld.DriverInterface.UI;

public partial class Image : Component
{
    public override bool DebugIsContentfulNode => true;

    public Image()
    {
        NodeInternal.MeasureFunction += Measure;
    }

    private YogaSize Measure(YogaNode node, float availableWidth, YogaMeasureMode widthMode, float availableHeight, YogaMeasureMode heightMode)
    {
        return new YogaSize(Scale * ImageData?.Width ?? availableWidth, Scale * ImageData?.Height ?? availableHeight);
    }

    [ClientOnly]
    public IImage? ImageData
    {
        get;
        set
        {
            field = value;
            NodeInternal.MarkDirty();
        }
    }

    [ClientOnly]
    public float Scale
    {
        get;
        set
        {
            field = value;
            NodeInternal.MarkDirty();
        }
    } = 1f;

    [ClientOnly]
    protected override void RenderContent(LuaVector2 position, LuaVector2 size)
    {
        if (ImageData != null)
        {
            G.DrawImage(ImageData, (int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
        }
    }
}