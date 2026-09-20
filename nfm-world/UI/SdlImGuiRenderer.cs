using Hexa.NET.ImGui;
using NFMWorld.DriverInterface;
using NFMWorld.Graphics;
using NFMWorld.Platform.SDL3;
using NFMWorld.Shaders;
using NFMWorld.Shaders.Generated;

namespace NFMWorld;

/// <summary>
/// Replaces <c>MonoGame.ImGuiNet.ImGuiRenderer</c> (whose constructor takes an FNA <c>Game</c> -
/// a type that no longer exists in this app's object model). Ported from that class's reference
/// implementation (<c>MonoGame.ImGuiNet/ImGuiRenderer.cs</c>) against the new command-buffer
/// graphics abstraction instead of XNA's <c>GraphicsDevice</c>/<c>BasicEffect</c>: one small
/// hand-authored shader (<c>data/shaders/ImGui.fx</c>) instead of <c>BasicEffect</c>, dynamic
/// <see cref="IBuffer"/>s instead of <c>VertexBuffer</c>/<c>IndexBuffer</c>, and a per-draw-command
/// <see cref="ICommandBuffer.SetScissorRect"/>/<see cref="ICommandBuffer.SetShaderResource"/>
/// instead of <c>GraphicsDevice.ScissorRectangle</c>/<c>Effect.Texture</c>.
/// </summary>
public sealed class SdlImGuiRenderer : IDisposable
{
    /// <summary>sizeof(ImDrawVert): float2 Position + float2 UV + packed byte4 Color.</summary>
    private const int VertexStride = 20;

    private static readonly VertexLayoutDesc VertexLayout = new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("TEXCOORD", 0, 8, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("COLOR", 0, 16, VertexAttributeFormat.Byte4Normalized),
        ],
        StrideInBytes: VertexStride);

    // See WorldGame.AllKeys's identical Distinct() call for why - Key has aliased members
    // sharing one underlying value (e.g. Oem3 == Oemtilde).
    private static readonly Key[] AllKeys = Enum.GetValues<Key>().Where(k => (uint)k <= 0xFE).Distinct().ToArray();

    private sealed class TextureInfo
    {
        public ITexture? Texture;
        public bool IsManaged;
    }

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly SdlWindow _window;

    private readonly ImGuiEffect _effect;
    private readonly IPipelineState _pipeline;
    private readonly ImGuiEffectParameters _parameters;
    private readonly ISampler _sampler;

    private readonly Dictionary<ImTextureID, TextureInfo> _textures = new();
    private int _nextTexId = 1;

    private byte[]? _vertexData;
    private IBuffer? _vertexBuffer;
    private int _vertexBufferSize;

    private byte[]? _indexData;
    private IBuffer? _indexBuffer;
    private int _indexBufferSize;

    public SdlImGuiRenderer(IGraphicsDevice graphicsDevice, SdlWindow window)
    {
        _graphicsDevice = graphicsDevice;
        _window = window;

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        var platformIO = ImGui.GetPlatformIO();
        platformIO.RendererTextureMaxWidth = 4096;
        platformIO.RendererTextureMaxHeight = 4096;

        _effect = new ImGuiEffect(VFS.ReadAllBytes("./data/shaders/ImGui.fxb"));
        _pipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: _effect.Module,
            PixelShader: _effect.Module,
            VertexLayouts: [VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            // Depth test must stay off so ImGui always paints on top of 3D/Yoga UI content.
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None, ScissorTestEnabled = true }));
        _parameters = _effect.Bind(_pipeline);
        _sampler = graphicsDevice.CreateSampler(new SamplerDesc(
            Filter: TextureFilter.Linear, AddressU: TextureAddressMode.Clamp, AddressV: TextureAddressMode.Clamp));

        window.TextInput += c =>
        {
            if (c == '\t') return;
            ImGui.GetIO().AddInputCharacter(c);
        };

        window.MouseWheel += (x, y) => ImGui.GetIO().AddMouseWheelEvent(x, y);
    }

    public void RebuildFontAtlas()
    {
        // Textures are created/updated lazily via ImGui's own texture-update list (processed in
        // EndLayout → ProcessTextureUpdates) - nothing to do eagerly here.
    }

    public unsafe ImTextureRef BindTexture(ITexture texture)
    {
        var texId = new IntPtr(_nextTexId++);
        _textures[(ImTextureID)texId] = new TextureInfo { Texture = texture, IsManaged = false };
        return new ImTextureRef(null, texId);
    }

    public void UnbindTexture(ImTextureRef textureRef)
    {
        if (_textures.TryGetValue(textureRef.TexID, out var info))
        {
            if (info.IsManaged) info.Texture?.Dispose();
            _textures.Remove(textureRef.TexID);
        }
    }

    public void BeginLayout(GameTime gameTime)
    {
        ImGui.GetIO().DeltaTime = Math.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 0.0001f);
        UpdateInput();
        ImGui.NewFrame();
    }

    public void EndLayout(ICommandBuffer cb)
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        ProcessTextureUpdates(cb, drawData);
        RenderDrawData(cb, drawData);
    }

    private void UpdateInput()
    {
        var io = ImGui.GetIO();

        var (buttons, mouseX, mouseY) = SdlWindow.GetMouseState();
        io.AddMousePosEvent(mouseX, mouseY);
        io.AddMouseButtonEvent(0, buttons.HasFlag(MouseButtons.Primary));
        io.AddMouseButtonEvent(1, buttons.HasFlag(MouseButtons.Secondary));
        io.AddMouseButtonEvent(2, buttons.HasFlag(MouseButtons.Middle));
        io.AddMouseButtonEvent(3, buttons.HasFlag(MouseButtons.XButton1));
        io.AddMouseButtonEvent(4, buttons.HasFlag(MouseButtons.XButton2));

        var keys = SdlWindow.GetKeyboardState();
        foreach (var key in AllKeys)
        {
            if (TryMapKey(key, out var imguiKey))
                io.AddKeyEvent(imguiKey, keys[key]);
        }

        io.DisplaySize = new System.Numerics.Vector2(_window.Width, _window.Height);
        io.DisplayFramebufferScale = new System.Numerics.Vector2(1f, 1f);
    }

    private static bool TryMapKey(Key key, out ImGuiKey imguiKey)
    {
        if (key == Key.None) { imguiKey = ImGuiKey.None; return true; }

        imguiKey = key switch
        {
            Key.Back => ImGuiKey.Backspace,
            Key.Tab => ImGuiKey.Tab,
            Key.Enter => ImGuiKey.Enter,
            Key.CapsLock => ImGuiKey.CapsLock,
            Key.Escape => ImGuiKey.Escape,
            Key.Space => ImGuiKey.Space,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.End => ImGuiKey.End,
            Key.Home => ImGuiKey.Home,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            >= Key.D0 and <= Key.D9 => ImGuiKey.Key0 + (key - Key.D0),
            >= Key.A and <= Key.Z => ImGuiKey.A + (key - Key.A),
            >= Key.NumPad0 and <= Key.NumPad9 => ImGuiKey.Keypad0 + (key - Key.NumPad0),
            Key.Multiply => ImGuiKey.KeypadMultiply,
            Key.Add => ImGuiKey.KeypadAdd,
            Key.Subtract => ImGuiKey.KeypadSubtract,
            Key.Decimal => ImGuiKey.KeypadDecimal,
            Key.Divide => ImGuiKey.KeypadDivide,
            >= Key.F1 and <= Key.F12 => ImGuiKey.F1 + (key - Key.F1),
            Key.NumLock => ImGuiKey.NumLock,
            Key.Scroll => ImGuiKey.ScrollLock,
            Key.LShiftKey => ImGuiKey.ModShift,
            Key.LControlKey => ImGuiKey.ModCtrl,
            Key.LMenu => ImGuiKey.ModAlt,
            Key.OemSemicolon => ImGuiKey.Semicolon,
            Key.Oemplus => ImGuiKey.Equal,
            Key.Oemcomma => ImGuiKey.Comma,
            Key.OemMinus => ImGuiKey.Minus,
            Key.OemPeriod => ImGuiKey.Period,
            Key.OemQuestion => ImGuiKey.Slash,
            Key.Oemtilde => ImGuiKey.GraveAccent,
            Key.OemOpenBrackets => ImGuiKey.LeftBracket,
            Key.OemCloseBrackets => ImGuiKey.RightBracket,
            Key.OemPipe => ImGuiKey.Backslash,
            Key.OemQuotes => ImGuiKey.Apostrophe,
            _ => ImGuiKey.None,
        };

        return imguiKey != ImGuiKey.None;
    }

    private unsafe void ProcessTextureUpdates(ICommandBuffer cb, ImDrawDataPtr drawData)
    {
        if (drawData.Textures.Data == null) return;
        for (var i = 0; i < drawData.Textures.Size; i++)
            UpdateTexture(cb, drawData.Textures.Data[i]);
    }

    private void UpdateTexture(ICommandBuffer cb, ImTextureDataPtr textureData)
    {
        switch (textureData.Status)
        {
            case ImTextureStatus.WantCreate: CreateTexture(textureData); break;
            case ImTextureStatus.WantUpdates: UpdateTextureData(cb, textureData); break;
            case ImTextureStatus.WantDestroy: DestroyTexture(textureData); break;
        }
    }

    private unsafe void CreateTexture(ImTextureDataPtr textureData)
    {
        var format = textureData.Format == ImTextureFormat.Rgba32 ? TextureFormat.Rgba8 : TextureFormat.R8;
        var bytesPerPixel = textureData.Format == ImTextureFormat.Rgba32 ? 4 : 1;
        var initialData = textureData.Pixels != null
            ? new ReadOnlySpan<byte>(textureData.Pixels, textureData.Width * textureData.Height * bytesPerPixel)
            : default;

        var texture = _graphicsDevice.CreateTexture(new TextureDesc(textureData.Width, textureData.Height, format), initialData);
        _textures[textureData.TexID] = new TextureInfo { Texture = texture, IsManaged = true };
        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private unsafe void UpdateTextureData(ICommandBuffer cb, ImTextureDataPtr textureData)
    {
        var texId = textureData.GetTexID();
        if (!_textures.TryGetValue(texId, out var info) || info.Texture == null) return;

        var format = textureData.Format == ImTextureFormat.Rgba32 ? TextureFormat.Rgba8 : TextureFormat.R8;
        var bytesPerPixel = textureData.Format == ImTextureFormat.Rgba32 ? 4 : 1;

        if (info.Texture.Width != textureData.Width || info.Texture.Height != textureData.Height || info.Texture.Format != format)
        {
            info.Texture.Dispose();
            var initialData = textureData.Pixels != null
                ? new ReadOnlySpan<byte>(textureData.Pixels, textureData.Width * textureData.Height * bytesPerPixel)
                : default;
            info.Texture = _graphicsDevice.CreateTexture(new TextureDesc(textureData.Width, textureData.Height, format), initialData);
        }
        else if (textureData.Pixels != null)
        {
            var data = new ReadOnlySpan<byte>(textureData.Pixels, textureData.Width * textureData.Height * bytesPerPixel);
            cb.UpdateTexture(info.Texture, 0, 0, textureData.Width, textureData.Height, data);
        }

        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private void DestroyTexture(ImTextureDataPtr textureData)
    {
        var texId = textureData.GetTexID();
        if (_textures.TryGetValue(texId, out var info))
        {
            if (info.IsManaged) info.Texture?.Dispose();
            _textures.Remove(texId);
        }
    }

    private void RenderDrawData(ICommandBuffer cb, ImDrawDataPtr drawData)
    {
        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        cb.SetViewport(new Viewport(0, 0, _window.Width, _window.Height));

        UpdateBuffers(cb, drawData);
        RenderCommandLists(cb, drawData);
    }

    private unsafe void UpdateBuffers(ICommandBuffer cb, ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount == 0) return;

        if (drawData.TotalVtxCount > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();
            _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
            _vertexBuffer = _graphicsDevice.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, _vertexBufferSize * VertexStride));
            _vertexData = new byte[_vertexBufferSize * VertexStride];
        }

        if (drawData.TotalIdxCount > _indexBufferSize)
        {
            _indexBuffer?.Dispose();
            _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
            _indexBuffer = _graphicsDevice.CreateBuffer(new BufferDesc(BufferKind.Index, BufferUsage.Dynamic, _indexBufferSize * sizeof(ushort), IndexFormat.UInt16));
            _indexData = new byte[_indexBufferSize * sizeof(ushort)];
        }

        int vtxOffset = 0, idxOffset = 0;
        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            fixed (void* vtxDst = &_vertexData![vtxOffset * VertexStride])
            fixed (void* idxDst = &_indexData![idxOffset * sizeof(ushort)])
            {
                Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDst, _vertexData.Length - vtxOffset * VertexStride, cmdList.VtxBuffer.Size * VertexStride);
                Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDst, _indexData.Length - idxOffset * sizeof(ushort), cmdList.IdxBuffer.Size * sizeof(ushort));
            }
            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }

        cb.UpdateBuffer(_vertexBuffer!, _vertexData.AsSpan(0, drawData.TotalVtxCount * VertexStride));
        cb.UpdateBuffer(_indexBuffer!, _indexData.AsSpan(0, drawData.TotalIdxCount * sizeof(ushort)));
    }

    private void RenderCommandLists(ICommandBuffer cb, ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount == 0) return;

        cb.SetPipeline(_pipeline);
        cb.SetVertexBuffer(0, _vertexBuffer!, VertexStride);
        cb.SetIndexBuffer(_indexBuffer!);

        var displaySize = ImGui.GetIO().DisplaySize;
        var projection = System.Numerics.Matrix4x4.CreateOrthographicOffCenter(0f, displaySize.X, displaySize.Y, 0f, -1f, 1f);
        _parameters.Projection.SetValue(cb, projection);

        int vtxOffset = 0, idxOffset = 0;
        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            for (var cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                var drawCmd = cmdList.CmdBuffer[cmdi];
                if (drawCmd.ElemCount == 0) continue;

                var texId = drawCmd.TexRef.GetTexID();
                if (!_textures.TryGetValue(texId, out var info) || info.Texture == null)
                    continue;

                cb.SetScissorRect(new ScissorRect(
                    (int)drawCmd.ClipRect.X, (int)drawCmd.ClipRect.Y,
                    (int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                    (int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)));

                _parameters.Texture.SetValue(cb, info.Texture, _sampler);

                cb.DrawIndexed(
                    baseVertex: (int)drawCmd.VtxOffset + vtxOffset,
                    startIndex: (int)drawCmd.IdxOffset + idxOffset,
                    primitiveCount: (int)drawCmd.ElemCount / 3);
            }
            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _sampler.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        foreach (var t in _textures.Values)
        {
            if (t.IsManaged) t.Texture?.Dispose();
        }
        _textures.Clear();
        ImGui.DestroyContext();
    }
}
