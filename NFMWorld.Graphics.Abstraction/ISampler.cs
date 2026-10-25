namespace NFMWorld.Graphics;

public enum TextureFilter
{
    Point,
    Linear,
}

public enum TextureAddressMode
{
    Wrap,
    Clamp,
    Mirror,
}

public readonly record struct SamplerDesc(
    TextureFilter Filter = TextureFilter.Linear,
    TextureAddressMode AddressU = TextureAddressMode.Wrap,
    TextureAddressMode AddressV = TextureAddressMode.Wrap);

public interface ISampler : IDisposable
{
    SamplerDesc Desc { get; }
}
