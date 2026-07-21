using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xilium.CefGlue;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Off-screen CEF render handler. Receives OnPaint callbacks with dirty rects
/// and uploads changed pixel regions to a Texture2D for compositing in the
/// FNA draw pipeline.
/// </summary>
internal sealed class NfmwCefRenderHandler(GraphicsDevice graphicsDevice) : CefRenderHandler
{
    private Texture2D? _browserTexture;
    private int _textureWidth;
    private int _textureHeight;
    private bool _needsFullUpload = true;

    // Popup support (e.g., <select> dropdowns)
    private Texture2D? _popupTexture;
    private int _popupWidth;
    private int _popupHeight;
    private CefRectangle _popupRect;
    private bool _popupVisible;

    // Pre-allocated buffer for copying dirty rect pixel data
    private byte[]? _copyBuffer;

    public Texture2D? BrowserTexture => _browserTexture;

    /// <summary>The popup overlay texture (e.g., for &lt;select&gt; dropdowns).</summary>
    public Texture2D? PopupTexture => _popupTexture;

    /// <summary>Whether a CEF popup is currently visible.</summary>
    public bool PopupVisible => _popupVisible;

    /// <summary>Screen-space rectangle where the popup should be drawn.</summary>
    public CefRectangle PopupRect => _popupRect;

    public int ViewWidth { get; private set; }
    public int ViewHeight { get; private set; }

    public event Action? OnBrowserPainted;

    public void SetViewSize(int width, int height)
    {
        ViewWidth = width;
        ViewHeight = height;
    }

    protected override void GetViewRect(CefBrowser browser, out CefRectangle rect)
    {
        rect = new CefRectangle(0, 0, Math.Max(ViewWidth, 1), Math.Max(ViewHeight, 1));
    }

    protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
    {
        screenInfo.DeviceScaleFactor = 1.0f;
        screenInfo.Depth = 32;
        screenInfo.DepthPerComponent = 8;
        screenInfo.IsMonochrome = false;
        screenInfo.Rectangle = new CefRectangle(0, 0, Math.Max(ViewWidth, 1), Math.Max(ViewHeight, 1));
        screenInfo.AvailableRectangle = screenInfo.Rectangle;
        return true;
    }

    protected override void OnPaint(CefBrowser browser, CefPaintElementType type,
        CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
    {
        if (width <= 0 || height <= 0 || buffer == IntPtr.Zero)
            return;

        // Route popup paints to a separate texture
        if (type == CefPaintElementType.Popup)
        {
            PaintPopup(dirtyRects, buffer, width, height);
            return;
        }

        if (type != CefPaintElementType.View)
            return;

        EnsureTexture(ref _browserTexture, ref _textureWidth, ref _textureHeight, width, height);
        UploadToTexture(_browserTexture!, _textureWidth, dirtyRects, buffer, width, height);

        OnBrowserPainted?.Invoke();
    }

    private void PaintPopup(CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
    {
        EnsureTexture(ref _popupTexture, ref _popupWidth, ref _popupHeight, width, height);
        UploadToTexture(_popupTexture!, _popupWidth, dirtyRects, buffer, width, height);

        OnBrowserPainted?.Invoke();
    }

    private void EnsureTexture(ref Texture2D? texture, ref int texWidth, ref int texHeight,
        int width, int height)
    {
        if (texture == null || texWidth != width || texHeight != height)
        {
            texture?.Dispose();
            texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            texWidth = width;
            texHeight = height;
            _needsFullUpload = true;
        }
    }

    private void UploadToTexture(Texture2D texture, int texWidth,
        CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
    {
        var bytesPerPixel = 4; // BGRA

        var stride = width * bytesPerPixel;

        if (_needsFullUpload || dirtyRects.Length == 0)
        {
            // Full upload
            var totalBytes = width * height * bytesPerPixel;
            EnsureCopyBuffer(totalBytes);
            unsafe
            {
                fixed (byte* dst = _copyBuffer)
                {
                    Buffer.MemoryCopy(buffer.ToPointer(), dst, totalBytes, totalBytes);
                }
            }
            texture.SetData(_copyBuffer!);
            _needsFullUpload = false;
        }
        else
        {
            // Dirty-rect partial upload — only upload changed regions to GPU
            foreach (var rect in dirtyRects)
            {
                var clampedRect = new Rectangle(
                    Math.Max(0, rect.X),
                    Math.Max(0, rect.Y),
                    Math.Min(rect.Width, width - rect.X),
                    Math.Min(rect.Height, height - rect.Y));

                if (clampedRect.Width <= 0 || clampedRect.Height <= 0)
                    continue;

                var rectBytes = clampedRect.Width * clampedRect.Height * bytesPerPixel;
                EnsureCopyBuffer(rectBytes);

                // Copy only this dirty rect's pixels from the full buffer
                unsafe
                {
                    var srcPtr = (byte*)buffer.ToPointer();
                    fixed (byte* dstPtr = _copyBuffer)
                    {
                        for (int y = 0; y < clampedRect.Height; y++)
                        {
                            var srcOffset = ((clampedRect.Y + y) * stride) + (clampedRect.X * bytesPerPixel);
                            var dstOffset = y * clampedRect.Width * bytesPerPixel;
                            Buffer.MemoryCopy(
                                srcPtr + srcOffset,
                                dstPtr + dstOffset,
                                rectBytes - dstOffset,
                                clampedRect.Width * bytesPerPixel);
                        }
                    }
                }

                texture.SetData(0, clampedRect, _copyBuffer!, 0, rectBytes);
            }
        }
    }

    protected override void OnPopupShow(CefBrowser browser, bool show)
    {
        _popupVisible = show;
        if (!show)
        {
            // Popup hidden — clear the popup texture
            _popupTexture?.Dispose();
            _popupTexture = null;
            _popupWidth = 0;
            _popupHeight = 0;
        }
    }

    protected override void OnPopupSize(CefBrowser browser, CefRectangle rect)
    {
        // Called before OnPaint(PET_POPUP). Records where the popup
        // should be positioned on the main view.
        _popupRect = rect;
    }

    protected override void OnScrollOffsetChanged(CefBrowser browser, double x, double y)
    {
    }

    protected override void OnImeCompositionRangeChanged(CefBrowser browser,
        CefRange selectedRange, CefRectangle[] characterBounds)
    {
    }

    protected override void OnAcceleratedPaint(CefBrowser browser,
        CefPaintElementType type, CefRectangle[] dirtyRects, nint sharedTextureHandle)
    {
        // Accelerated paint uses shared textures — not used in off-screen mode
    }

    protected override CefAccessibilityHandler GetAccessibilityHandler()
    {
        return null!;
    }

    private void EnsureCopyBuffer(int size)
    {
        if (_copyBuffer == null || _copyBuffer.Length < size)
        {
            _copyBuffer = new byte[size];
        }
    }

    public void DestroyTexture()
    {
        _browserTexture?.Dispose();
        _browserTexture = null;
        _textureWidth = 0;
        _textureHeight = 0;

        _popupTexture?.Dispose();
        _popupTexture = null;
        _popupWidth = 0;
        _popupHeight = 0;
        _popupVisible = false;

        _needsFullUpload = true;
    }
}
