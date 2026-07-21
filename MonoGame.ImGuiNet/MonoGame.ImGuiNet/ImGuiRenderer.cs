using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;

#nullable enable

namespace MonoGame.ImGuiNet;

internal class TextureInfo
{
    public Texture2D? Texture;
    public bool IsManaged;
}

public class ImGuiRenderer : IDisposable
{
    private Game _game;

    // Graphics
    private GraphicsDevice _graphicsDevice;

    private BasicEffect? _effect;
    private RasterizerState _rasterizerState;

    private byte[]? _vertexData;
    private VertexBuffer? _vertexBuffer;
    private int _vertexBufferSize;

    private byte[]? _indexData;
    private IndexBuffer? _indexBuffer;
    private int _indexBufferSize;

    // Textures
    private Dictionary<ImTextureID, TextureInfo> _textures;
    private int _nextTexId = 1;

    // Input
    private int _scrollWheelValue;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private int _horizontalScrollWheelValue;
#pragma warning restore CS0414
    private readonly float WHEEL_DELTA = 120;
    private Keys[] _allKeys = Enum.GetValues<Keys>();

    public ImGuiRenderer(Game game)
    {
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        _game = game ?? throw new ArgumentNullException(nameof(game));
        _graphicsDevice = game.GraphicsDevice;

        _textures = new Dictionary<ImTextureID, TextureInfo>();

        _rasterizerState = new RasterizerState()
        {
            CullMode = CullMode.None,
            DepthBias = 0,
            FillMode = FillMode.Solid,
            MultiSampleAntiAlias = false,
            ScissorTestEnable = true,
            SlopeScaleDepthBias = 0
        };

        SetupInput();
        SetupBackendCapabilities();
    }

    private void SetupBackendCapabilities()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        ImGuiPlatformIOPtr platformIO = ImGui.GetPlatformIO();
        if (_graphicsDevice.GraphicsProfile == GraphicsProfile.Reach)
        {
            platformIO.RendererTextureMaxWidth = 2048;
            platformIO.RendererTextureMaxHeight = 2048;
        }
        else
        {
            platformIO.RendererTextureMaxWidth = 4096;
            platformIO.RendererTextureMaxHeight = 4096;
        }
    }

    #region ImGuiRenderer

    public virtual void RebuildFontAtlas() { }

    public virtual unsafe ImTextureRef BindTexture(Texture2D texture)
    {
        IntPtr texId = new IntPtr(_nextTexId++);

        _textures[(ImTextureID)texId] = new TextureInfo
        {
            Texture = texture,
            IsManaged = false,
        };

        return new ImTextureRef(null, texId);
    }

    public virtual void UnbindTexture(ImTextureRef textureRef)
    {
        if (_textures.TryGetValue(textureRef.TexID, out var textureInfo))
        {
            if (textureInfo.IsManaged)
                textureInfo.Texture?.Dispose();
            _textures.Remove(textureRef.TexID);
        }
    }

    public virtual void BeginLayout(GameTime gameTime)
    {
        ImGui.GetIO().DeltaTime = Math.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 0.0001f);
        UpdateInput();
        ImGui.NewFrame();
    }

    public virtual void EndLayout()
    {
        ImGui.Render();
        unsafe
        {
            ImDrawDataPtr drawData = ImGui.GetDrawData();
            ProcessTextureUpdates(drawData);
            RenderDrawData(drawData);
        }
    }

    public virtual void UpdateTexture(ImTextureDataPtr textureData)
    {
        switch (textureData.Status)
        {
            case ImTextureStatus.WantCreate:  CreateTexture(textureData);     break;
            case ImTextureStatus.WantUpdates: UpdateTextureData(textureData); break;
            case ImTextureStatus.WantDestroy: DestroyTexture(textureData);    break;
        }
    }

    private unsafe void CreateTexture(ImTextureDataPtr textureData)
    {
        SurfaceFormat format = textureData.Format == ImTextureFormat.Rgba32 ? SurfaceFormat.Color : SurfaceFormat.Alpha8;
        Texture2D texture = new Texture2D(_graphicsDevice, textureData.Width, textureData.Height, false, format);

        if (textureData.Pixels != null)
        {
            int bytesPerPixel = textureData.Format == ImTextureFormat.Rgba32 ? 4 : 1;
            byte[] data = new byte[textureData.Width * textureData.Height * bytesPerPixel];
            Marshal.Copy(new IntPtr(textureData.Pixels), data, 0, data.Length);
            texture.SetData(data);
        }

        _textures[textureData.TexID] = new TextureInfo { Texture = texture, IsManaged = true };
        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private unsafe void UpdateTextureData(ImTextureDataPtr textureData)
    {
        IntPtr texId = textureData.GetTexID();
        if (!_textures.TryGetValue(texId, out var textureInfo) || textureInfo.Texture == null)
            return;

        Texture2D texture = textureInfo.Texture;
        SurfaceFormat newFormat = textureData.Format == ImTextureFormat.Rgba32 ? SurfaceFormat.Color : SurfaceFormat.Alpha8;

        if (texture.Width != textureData.Width || texture.Height != textureData.Height || texture.Format != newFormat)
        {
            texture.Dispose();
            texture = new Texture2D(_graphicsDevice, textureData.Width, textureData.Height, false, newFormat);
            textureInfo.Texture = texture;
        }

        if (textureData.Pixels != null)
        {
            int bytesPerPixel = textureData.Format == ImTextureFormat.Rgba32 ? 4 : 1;
            byte[] data = new byte[textureData.Width * textureData.Height * bytesPerPixel];
            Marshal.Copy(new IntPtr(textureData.Pixels), data, 0, data.Length);
            texture.SetData(data);
        }

        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private void DestroyTexture(ImTextureDataPtr textureData)
    {
        IntPtr texId = textureData.GetTexID();
        if (_textures.TryGetValue(texId, out var textureInfo))
        {
            if (textureInfo.IsManaged)
                textureInfo.Texture?.Dispose();
            _textures.Remove(texId);
        }
    }

    #endregion ImGuiRenderer

    #region Setup & Update

    protected virtual void SetupInput()
    {
        TextInputEXT.TextInput += c =>
        {
            if (c == '\t') return;
            ImGui.GetIO().AddInputCharacter(c);
        };
        TextInputEXT.StartTextInput();
    }

    protected virtual Effect UpdateEffect(Texture2D texture)
    {
        _effect ??= new BasicEffect(_graphicsDevice);

        var io = ImGui.GetIO();
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
        _effect.TextureEnabled = true;
        _effect.Texture = texture;
        _effect.VertexColorEnabled = true;
        return _effect;
    }

    protected virtual void UpdateInput()
    {
        if (!_game.IsActive) return;

        var io = ImGui.GetIO();
        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();

        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(3, mouse.XButton1 == ButtonState.Pressed);
        io.AddMouseButtonEvent(4, mouse.XButton2 == ButtonState.Pressed);

        io.AddMouseWheelEvent(0, (mouse.ScrollWheelValue - _scrollWheelValue) / WHEEL_DELTA);
        _scrollWheelValue = mouse.ScrollWheelValue;
        _horizontalScrollWheelValue = 0;

        foreach (var key in _allKeys)
        {
            if (TryMapKeys(key, out ImGuiKey imguikey))
                io.AddKeyEvent(imguikey, keyboard.IsKeyDown(key));
        }

        io.DisplaySize = new System.Numerics.Vector2(
            _graphicsDevice.PresentationParameters.BackBufferWidth,
            _graphicsDevice.PresentationParameters.BackBufferHeight);
        io.DisplayFramebufferScale = new System.Numerics.Vector2(1f, 1f);
    }

    private bool TryMapKeys(Keys key, out ImGuiKey imguikey)
    {
        if (key == Keys.None) { imguikey = ImGuiKey.None; return true; }

        imguikey = key switch
        {
            Keys.Back => ImGuiKey.Backspace,
            Keys.Tab => ImGuiKey.Tab,
            Keys.Enter => ImGuiKey.Enter,
            Keys.CapsLock => ImGuiKey.CapsLock,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Space => ImGuiKey.Space,
            Keys.PageUp => ImGuiKey.PageUp,
            Keys.PageDown => ImGuiKey.PageDown,
            Keys.End => ImGuiKey.End,
            Keys.Home => ImGuiKey.Home,
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.PrintScreen => ImGuiKey.PrintScreen,
            Keys.Insert => ImGuiKey.Insert,
            Keys.Delete => ImGuiKey.Delete,
            >= Keys.D0 and <= Keys.D9 => ImGuiKey.Key0 + (key - Keys.D0),
            >= Keys.A and <= Keys.Z => ImGuiKey.A + (key - Keys.A),
            >= Keys.NumPad0 and <= Keys.NumPad9 => ImGuiKey.Keypad0 + (key - Keys.NumPad0),
            Keys.Multiply => ImGuiKey.KeypadMultiply,
            Keys.Add => ImGuiKey.KeypadAdd,
            Keys.Subtract => ImGuiKey.KeypadSubtract,
            Keys.Decimal => ImGuiKey.KeypadDecimal,
            Keys.Divide => ImGuiKey.KeypadDivide,
            >= Keys.F1 and <= Keys.F12 => ImGuiKey.F1 + (key - Keys.F1),
            Keys.NumLock => ImGuiKey.NumLock,
            Keys.Scroll => ImGuiKey.ScrollLock,
            Keys.LeftShift => ImGuiKey.ModShift,
            Keys.LeftControl => ImGuiKey.ModCtrl,
            Keys.LeftAlt => ImGuiKey.ModAlt,
            Keys.OemSemicolon => ImGuiKey.Semicolon,
            Keys.OemPlus => ImGuiKey.Equal,
            Keys.OemComma => ImGuiKey.Comma,
            Keys.OemMinus => ImGuiKey.Minus,
            Keys.OemPeriod => ImGuiKey.Period,
            Keys.OemQuestion => ImGuiKey.Slash,
            Keys.OemTilde => ImGuiKey.GraveAccent,
            Keys.OemOpenBrackets => ImGuiKey.LeftBracket,
            Keys.OemCloseBrackets => ImGuiKey.RightBracket,
            Keys.OemPipe => ImGuiKey.Backslash,
            Keys.OemQuotes => ImGuiKey.Apostrophe,
            _ => ImGuiKey.None,
        };

        return imguikey != ImGuiKey.None;
    }

    #endregion Setup & Update

    #region Internals

    private unsafe void ProcessTextureUpdates(ImDrawDataPtr drawData)
    {
        if (drawData.Textures.Data == null) return;
        for (int i = 0; i < drawData.Textures.Size; i++)
            UpdateTexture(drawData.Textures.Data[i]);
    }

    private unsafe void RenderDrawData(ImDrawData* drawData)
    {
        var lastViewport = _graphicsDevice.Viewport;
        var lastScissorBox = _graphicsDevice.ScissorRectangle;

        _graphicsDevice.BlendFactor = Color.White;
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;
        _graphicsDevice.RasterizerState = _rasterizerState;
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

        drawData->ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        _graphicsDevice.Viewport = new Viewport(0, 0,
            _graphicsDevice.PresentationParameters.BackBufferWidth,
            _graphicsDevice.PresentationParameters.BackBufferHeight);

        UpdateBuffers(drawData);
        RenderCommandLists(drawData);

        _graphicsDevice.Viewport = lastViewport;
        _graphicsDevice.ScissorRectangle = lastScissorBox;
    }

    private unsafe void UpdateBuffers(ImDrawData* drawData)
    {
        if (drawData->TotalVtxCount == 0) return;

        if (drawData->TotalVtxCount > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();
            _vertexBufferSize = (int)(drawData->TotalVtxCount * 1.5f);
            _vertexBuffer = new VertexBuffer(_graphicsDevice, DrawVertDeclaration.Declaration, _vertexBufferSize, BufferUsage.None);
            _vertexData = new byte[_vertexBufferSize * DrawVertDeclaration.Size];
        }

        if (drawData->TotalIdxCount > _indexBufferSize)
        {
            _indexBuffer?.Dispose();
            _indexBufferSize = (int)(drawData->TotalIdxCount * 1.5f);
            _indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
            _indexData = new byte[_indexBufferSize * sizeof(ushort)];
        }

        int vtxOffset = 0, idxOffset = 0;
        for (int n = 0; n < drawData->CmdListsCount; n++)
        {
            ImDrawList* cmdList = drawData->CmdLists.Data[n];
            fixed (void* vtxDst = &_vertexData![vtxOffset * DrawVertDeclaration.Size])
            fixed (void* idxDst = &_indexData![idxOffset * sizeof(ushort)])
            {
                Buffer.MemoryCopy(cmdList->VtxBuffer.Data, vtxDst, _vertexData.Length, cmdList->VtxBuffer.Size * DrawVertDeclaration.Size);
                Buffer.MemoryCopy(cmdList->IdxBuffer.Data, idxDst, _indexData.Length, cmdList->IdxBuffer.Size * sizeof(ushort));
            }
            vtxOffset += cmdList->VtxBuffer.Size;
            idxOffset += cmdList->IdxBuffer.Size;
        }

        _vertexBuffer!.SetData(_vertexData, 0, drawData->TotalVtxCount * DrawVertDeclaration.Size);
        _indexBuffer!.SetData(_indexData, 0, drawData->TotalIdxCount * sizeof(ushort));
    }

    private unsafe void RenderCommandLists(ImDrawData* drawData)
    {
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;

        int vtxOffset = 0, idxOffset = 0;
        for (int n = 0; n < drawData->CmdListsCount; n++)
        {
            ImDrawList* cmdList = drawData->CmdLists.Data[n];
            for (int cmdi = 0; cmdi < cmdList->CmdBuffer.Size; cmdi++)
            {
                ImDrawCmd* drawCmd = &cmdList->CmdBuffer.Data[cmdi];
                if (drawCmd->ElemCount == 0) continue;

                ImTextureID texId = drawCmd->TexRef.GetTexID();
                if (!_textures.TryGetValue(texId, out var textureInfo) || textureInfo.Texture == null)
                    throw new InvalidOperationException($"Could not find a texture with id '{texId}', please check your bindings");

                _graphicsDevice.ScissorRectangle = new Rectangle(
                    (int)drawCmd->ClipRect.X, (int)drawCmd->ClipRect.Y,
                    (int)(drawCmd->ClipRect.Z - drawCmd->ClipRect.X),
                    (int)(drawCmd->ClipRect.W - drawCmd->ClipRect.Y));

                var effect = UpdateEffect(textureInfo.Texture);
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
#pragma warning disable CS0618
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        (int)drawCmd->VtxOffset + vtxOffset, 0,
                        cmdList->VtxBuffer.Size,
                        (int)drawCmd->IdxOffset + idxOffset,
                        (int)drawCmd->ElemCount / 3);
#pragma warning restore CS0618
                }
            }
            vtxOffset += cmdList->VtxBuffer.Size;
            idxOffset += cmdList->IdxBuffer.Size;
        }
    }

    #endregion Internals

    public void Dispose()
    {
        _effect?.Dispose();
        _rasterizerState.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        foreach (var t in _textures.Values)
            if (t.IsManaged) t.Texture?.Dispose();
        _textures.Clear();
        ImGui.DestroyContext();
    }
}