using System.Numerics;
using Microsoft.Xna.Framework;

namespace NFMWorld.DriverInterface.DriverInterface;

public sealed class DummyBackend : IBackend
{
    public IRadicalMusic LoadMusic(string file, double tempomul)
    {
        return new DummyMusic();
    }

    public void StopAllSounds()
    {
    }

    public ISoundClip GetSound(string filePath)
    {
        return new DummySoundClip();
    }

    public IGraphics Graphics { get; } = new DummyGraphics();

    public sealed class DummyGraphics : IGraphics
    {
        public Vector2 Viewport => new();
        public float Scale { get; set; } = 1;

        public IImage LoadImage(string file)
        {
            return new DummyImage();
        }

        public IImage LoadImage(ReadOnlyMemory<byte> file)
        {
            return new DummyImage();
        }

        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos)
        {
        }

        public void SetColor(Color c)
        {
        }

        public float Alpha
        {
            set { }
        }

        public void DrawImage(IImage image, int x, int y)
        {
        }

        public void SetFont(Font font)
        {
        }

        public IFontMetrics GetFontMetrics()
        {
            return new DummyFontMetrics();
        }

        public IFontMetrics GetFontMetrics(Font font)
        {
            return new DummyFontMetrics();
        }

        public void DrawString(ReadOnlySpan<char> text, int x, int y)
        {
        }
        public void DrawStringAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top)
        {
        }

        public void DrawStringStrokeAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        {
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
        }

        public void BeginPath()
        {
        }

        public void MoveTo(float x, float y)
        {
        }

        public void LineTo(float x, float y)
        {
        }

        public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
        }

        public void ClosePath()
        {
        }

        public void MarkHole()
        {
        }

        public void Stroke()
        {
        }

        public void Fill()
        {
        }
    }

    public void SetAllVolumes(float vol)
    {
    }

    public Key GetKeyFromScancode(Key key)
    {
        return key;
    }
}

file sealed class DummyMusic : IRadicalMusic
{
    public void SetPaused(bool p0)
    {
    }

    public void Dispose()
    {
    }

    public void Play()
    {
    }

    public void SetVolume(float vol)
    {
    }

    public float GetVolume()
    {
        return 1f;
    }

    public void SetFreqMultiplier(double multiplier)
    {
    }
}

file sealed class DummyFontMetrics : IFontMetrics
{
    public Vector2 MeasureText(ReadOnlySpan<char> text)
    {
        return Vector2.Zero;
    }

    public float LineHeight => 0;
}

file sealed class DummySoundClip : ISoundClip
{
    public void Play()
    {
    }

    public void Loop()
    {
    }

    public void Stop()
    {
    }
}

file sealed class DummyImage : IImage
{
    public int Height => 0;
    public int Width => 0;
}