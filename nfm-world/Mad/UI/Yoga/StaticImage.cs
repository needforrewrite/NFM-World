using NFMWorld.DriverInterface;

namespace NFMWorld.UI.Yoga;

public class StaticImage(string path)
{
    public string Path { get; set; } = path;

    public IImage ProvideValue(IServiceProvider serviceProvider)
    {
        return IBackend.Backend.LoadCachedImage(Path);
    }
}