using GraphicsDevice = nfm_world.compat.GraphicsDeviceCompat;
using System.Collections.Frozen;
using System.Reflection;
using ManagedBass;
using Microsoft.Extensions.Logging;
using nfm_world.driverinterface;
using nfm_world.compat;
using nfm_world.skiadriver;
using nfm_world.ui.hud;
using nfm_world.ui.yoga;
using nfm_world.ui.yoga.xaml;
using Font = nfm_world.util.Font;

namespace nfm_world;

public class DummyBackend : IBackend
{
    public Vector2 Viewport => new();
    public float Scale { get; set; } = 1;

    public IRadicalMusic LoadMusic(string file, double tempomul)
    {
        return new RadicalMusic(file, tempomul);
    }

    public IImage LoadImage(string file)
    {
        throw new NotImplementedException();
    }
    
    public IImage LoadCachedImage(string file)
    {
        throw new NotImplementedException();
    }

    public IImage LoadImage(ReadOnlySpan<byte> file)
    {
        throw new NotImplementedException();
    }

    public void StopAllSounds()
    {
        SoundClip.StopAll();
    }

    public ISoundClip GetSound(string filePath)
    {
        return new SoundClip(filePath);
    }

    public IGraphics Graphics { get; } = new DummyGraphics();

    public class DummyGraphics : IGraphics
    {
        public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[] colorPos)
        {
            
        }
        public void SetColor(Color c)
        {
        }

        public void FillPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
        {
        }

        public void DrawPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
        {
        }

        public void FillRect(int x1, int y1, int width, int height)
        {
        }

        public void DrawLine(int x1, int y1, int x2, int y2)
        {
        }

        public void SetAlpha(float f)
        {
        }

        public void DrawImage(IImage image, int x, int y)
        {
        }

        public void SetFont(Font font)
        {
        }

        public IFontMetrics GetFontMetrics()
        {
            throw new NotImplementedException();
        }

        public void DrawString(string text, int x, int y)
        {
        }
        public void DrawStringAligned(string text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top)
        {
        }

        public void DrawStringStrokeAligned(string text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        {
        }

        public void FillOval(int p0, int p1, int p2, int p3)
        {
        }

        public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
        {
        }

        public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
        {
        }

        public void DrawRect(int x1, int y1, int width, int height)
        {
        }

        public void DrawImage(IImage image, int x, int y, int width, int height)
        {
        }
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}