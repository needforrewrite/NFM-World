using NFMWorld.DriverInterface;

namespace NFMWorld.UI;

public class StaticImage(string path)
{
    public string Path { get; set; } = path;

    public IImage ProvideValue(IServiceProvider serviceProvider)
    {
        return NFMWorld.DriverInterface.IBackend.Backend.LoadCachedImage(Path);
    }
}