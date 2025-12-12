using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Input;

public class ImGuiController : IDisposable
{
    private GL _gl;
    private IWindow _window;
    private IInputContext _input;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private uint _shaderProgram;
    private uint _fontTexture;
    private int _windowWidth;
    private int _windowHeight;

    public ImGuiController(GL gl, IWindow window, IInputContext input)
    {
        _gl = gl;
        _window = window;
        _input = input;
        _windowWidth = window.Size.X;
        _windowHeight = window.Size.Y;

        // Initialize ImGui
        ImGui.CreateContext();
        ImGui.StyleColorsDark();


        // custom style
        var style = ImGui.GetStyle();
        
        // Rounding 
        style.WindowRounding = 4.0f;
        style.FrameRounding = 6.0f;
        style.GrabRounding = 4.0f;
        style.PopupRounding = 6.0f;
        style.ScrollbarRounding = 6.0f;
        style.TabRounding = 4.0f;
        
        // Spacing and padding
        style.WindowPadding = new Vector2(12, 12);
        style.FramePadding = new Vector2(8, 4);
        style.ItemSpacing = new Vector2(8, 6);
        
        // Border
        style.WindowBorderSize = 2.0f;
        style.FrameBorderSize = 2.0f;
        
        Vector4 RGB(int r, int g, int b, float a = 1.0f) => new Vector4(r / 255f, g / 255f, b / 255f, a);
        
        var colors = style.Colors;
        
        // Windows and backgrounds
        colors[(int)ImGuiCol.WindowBg] = RGB(31, 26, 46, 0.95f);          // Dark purple
        colors[(int)ImGuiCol.ChildBg] = RGB(26, 20, 38, 0.90f);           // Darker purple
        colors[(int)ImGuiCol.PopupBg] = RGB(26, 20, 38, 0.95f);           // Darker purple
        colors[(int)ImGuiCol.MenuBarBg] = RGB(38, 31, 56, 1.0f);          // Medium purple
        
        // Borders
        colors[(int)ImGuiCol.Border] = RGB(230, 128, 26, 0.8f);           // Orange
        colors[(int)ImGuiCol.BorderShadow] = RGB(0, 0, 0, 0.5f);          // Black shadow
        
        // Text
        colors[(int)ImGuiCol.Text] = RGB(255, 191, 51, 1.0f);             // Light orange/yellow
        colors[(int)ImGuiCol.TextDisabled] = RGB(153, 115, 38, 1.0f);     // Dimmed orange
        
        // Title bar
        colors[(int)ImGuiCol.TitleBg] = RGB(38, 31, 64, 1.0f);            // Dark purple
        colors[(int)ImGuiCol.TitleBgActive] = RGB(51, 38, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.TitleBgCollapsed] = RGB(31, 26, 51, 0.75f);  // Very dark purple
        
        // Frames (inputs, etc)
        colors[(int)ImGuiCol.FrameBg] = RGB(38, 31, 56, 0.9f);            // Medium purple
        colors[(int)ImGuiCol.FrameBgHovered] = RGB(64, 51, 89, 1.0f);     // Lighter purple
        colors[(int)ImGuiCol.FrameBgActive] = RGB(77, 64, 102, 1.0f);     // Even lighter purple
        
        // Buttons (dark with orange on hover)
        colors[(int)ImGuiCol.Button] = RGB(38, 31, 64, 1.0f);             // Dark purple
        colors[(int)ImGuiCol.ButtonHovered] = RGB(64, 51, 89, 1.0f);      // Lighter purple
        colors[(int)ImGuiCol.ButtonActive] = RGB(128, 77, 3, 0.8f);       // Dark orange
        
        // Headers
        colors[(int)ImGuiCol.Header] = RGB(51, 38, 77, 1.0f);             // Medium purple
        colors[(int)ImGuiCol.HeaderHovered] = RGB(230, 128, 26, 0.6f);    // Orange
        colors[(int)ImGuiCol.HeaderActive] = RGB(128, 77, 3, 0.8f);       // Dark orange
        
        // Tabs
        colors[(int)ImGuiCol.Tab] = RGB(38, 31, 64, 1.0f);                     // Dark purple (inactive)
        colors[(int)ImGuiCol.TabHovered] = RGB(230, 128, 26, 0.8f);            // Orange (hovered)
        colors[(int)ImGuiCol.TabSelected] = RGB(128, 77, 3, 1.0f);           // Orange (active/selected)
        colors[(int)ImGuiCol.TabDimmed] = RGB(31, 26, 51, 1.0f);               // Very dark purple (unfocused)
        colors[(int)ImGuiCol.TabDimmedSelected] = RGB(128, 77, 26, 0.8f);      // Dimmed orange (unfocused selected)
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = RGB(230, 128, 26, 1.0f); // Orange underline
        colors[(int)ImGuiCol.TabSelectedOverline] = RGB(230, 128, 26, 1.0f);   // Orange underline (focused)
        
        // Checkmarks and sliders (orange)
        colors[(int)ImGuiCol.CheckMark] = RGB(255, 179, 51, 1.0f);        // Light orange
        colors[(int)ImGuiCol.SliderGrab] = RGB(230, 128, 26, 1.0f);       // Orange
        colors[(int)ImGuiCol.SliderGrabActive] = RGB(255, 166, 51, 1.0f); // Lighter orange
        
        // Scrollbar
        colors[(int)ImGuiCol.ScrollbarBg] = RGB(26, 20, 38, 0.9f);        // Dark purple
        colors[(int)ImGuiCol.ScrollbarGrab] = RGB(64, 51, 89, 1.0f);      // Medium purple
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = RGB(89, 71, 115, 1.0f); // Lighter purple
        colors[(int)ImGuiCol.ScrollbarGrabActive] = RGB(230, 128, 26, 1.0f); // Orange
        
        // Separators (orange)
        colors[(int)ImGuiCol.Separator] = RGB(230, 128, 26, 0.5f);        // Orange
        colors[(int)ImGuiCol.SeparatorHovered] = RGB(230, 128, 26, 0.8f); // Orange
        colors[(int)ImGuiCol.SeparatorActive] = RGB(255, 153, 51, 1.0f);  // Lighter orange
        
        // Resize grip
        colors[(int)ImGuiCol.ResizeGrip] = RGB(230, 128, 26, 0.3f);       // Orange
        colors[(int)ImGuiCol.ResizeGripHovered] = RGB(230, 128, 26, 0.6f); // Orange
        colors[(int)ImGuiCol.ResizeGripActive] = RGB(255, 153, 51, 1.0f);  // Lighter orange
        style.FrameRounding = 3.0f;
        style.WindowPadding = new Vector2(10, 10);
        style.FramePadding = new Vector2(5, 3);
        style.ItemSpacing = new Vector2(8, 4);

        // Set up ImGui I/O
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(window.Size.X, window.Size.Y);
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        // Create device objects
        CreateDeviceObjects();
        
        // Setup input callbacks
        SetupInput();
    }

    private void SetupInput()
    {
        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyChar += OnKeyChar;
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }
        
        foreach (var mouse in _input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnScroll;
        }
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        var io = ImGui.GetIO();
        io.AddInputCharacter(c);
    }
    
    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(TranslateKey(key), true);
    }
    
    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(TranslateKey(key), false);
    }
    
    private ImGuiKey TranslateKey(Key key)
    {
        return key switch
        {
            Key.Tab => ImGuiKey.Tab,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.Backspace => ImGuiKey.Backspace,
            Key.Space => ImGuiKey.Space,
            Key.Enter => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.Apostrophe => ImGuiKey.Apostrophe,
            Key.Comma => ImGuiKey.Comma,
            Key.Minus => ImGuiKey.Minus,
            Key.Period => ImGuiKey.Period,
            Key.Slash => ImGuiKey.Slash,
            Key.Semicolon => ImGuiKey.Semicolon,
            Key.Equal => ImGuiKey.Equal,
            Key.LeftBracket => ImGuiKey.LeftBracket,
            Key.BackSlash => ImGuiKey.Backslash,
            Key.RightBracket => ImGuiKey.RightBracket,
            Key.GraveAccent => ImGuiKey.GraveAccent,
            Key.CapsLock => ImGuiKey.CapsLock,
            Key.ScrollLock => ImGuiKey.ScrollLock,
            Key.NumLock => ImGuiKey.NumLock,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.Pause => ImGuiKey.Pause,
            Key.Keypad0 => ImGuiKey.Keypad0,
            Key.Keypad1 => ImGuiKey.Keypad1,
            Key.Keypad2 => ImGuiKey.Keypad2,
            Key.Keypad3 => ImGuiKey.Keypad3,
            Key.Keypad4 => ImGuiKey.Keypad4,
            Key.Keypad5 => ImGuiKey.Keypad5,
            Key.Keypad6 => ImGuiKey.Keypad6,
            Key.Keypad7 => ImGuiKey.Keypad7,
            Key.Keypad8 => ImGuiKey.Keypad8,
            Key.Keypad9 => ImGuiKey.Keypad9,
            Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
            Key.KeypadDivide => ImGuiKey.KeypadDivide,
            Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
            Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
            Key.KeypadAdd => ImGuiKey.KeypadAdd,
            Key.KeypadEnter => ImGuiKey.KeypadEnter,
            Key.KeypadEqual => ImGuiKey.KeypadEqual,
            Key.ShiftLeft => ImGuiKey.LeftShift,
            Key.ControlLeft => ImGuiKey.LeftCtrl,
            Key.AltLeft => ImGuiKey.LeftAlt,
            Key.SuperLeft => ImGuiKey.LeftSuper,
            Key.ShiftRight => ImGuiKey.RightShift,
            Key.ControlRight => ImGuiKey.RightCtrl,
            Key.AltRight => ImGuiKey.RightAlt,
            Key.SuperRight => ImGuiKey.RightSuper,
            Key.Menu => ImGuiKey.Menu,
            Key.Number0 => ImGuiKey._0,
            Key.Number1 => ImGuiKey._1,
            Key.Number2 => ImGuiKey._2,
            Key.Number3 => ImGuiKey._3,
            Key.Number4 => ImGuiKey._4,
            Key.Number5 => ImGuiKey._5,
            Key.Number6 => ImGuiKey._6,
            Key.Number7 => ImGuiKey._7,
            Key.Number8 => ImGuiKey._8,
            Key.Number9 => ImGuiKey._9,
            Key.A => ImGuiKey.A,
            Key.B => ImGuiKey.B,
            Key.C => ImGuiKey.C,
            Key.D => ImGuiKey.D,
            Key.E => ImGuiKey.E,
            Key.F => ImGuiKey.F,
            Key.G => ImGuiKey.G,
            Key.H => ImGuiKey.H,
            Key.I => ImGuiKey.I,
            Key.J => ImGuiKey.J,
            Key.K => ImGuiKey.K,
            Key.L => ImGuiKey.L,
            Key.M => ImGuiKey.M,
            Key.N => ImGuiKey.N,
            Key.O => ImGuiKey.O,
            Key.P => ImGuiKey.P,
            Key.Q => ImGuiKey.Q,
            Key.R => ImGuiKey.R,
            Key.S => ImGuiKey.S,
            Key.T => ImGuiKey.T,
            Key.U => ImGuiKey.U,
            Key.V => ImGuiKey.V,
            Key.W => ImGuiKey.W,
            Key.X => ImGuiKey.X,
            Key.Y => ImGuiKey.Y,
            Key.Z => ImGuiKey.Z,
            Key.F1 => ImGuiKey.F1,
            Key.F2 => ImGuiKey.F2,
            Key.F3 => ImGuiKey.F3,
            Key.F4 => ImGuiKey.F4,
            Key.F5 => ImGuiKey.F5,
            Key.F6 => ImGuiKey.F6,
            Key.F7 => ImGuiKey.F7,
            Key.F8 => ImGuiKey.F8,
            Key.F9 => ImGuiKey.F9,
            Key.F10 => ImGuiKey.F10,
            Key.F11 => ImGuiKey.F11,
            Key.F12 => ImGuiKey.F12,
            _ => ImGuiKey.None,
        };
    }
    
    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        var io = ImGui.GetIO();
        if (button == MouseButton.Left) io.AddMouseButtonEvent(0, true);
        if (button == MouseButton.Right) io.AddMouseButtonEvent(1, true);
        if (button == MouseButton.Middle) io.AddMouseButtonEvent(2, true);
    }
    
    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        var io = ImGui.GetIO();
        if (button == MouseButton.Left) io.AddMouseButtonEvent(0, false);
        if (button == MouseButton.Right) io.AddMouseButtonEvent(1, false);
        if (button == MouseButton.Middle) io.AddMouseButtonEvent(2, false);
    }
    
    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        var io = ImGui.GetIO();
        io.AddMousePosEvent(position.X, position.Y);
    }
    
    private void OnScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        var io = ImGui.GetIO();
        io.AddMouseWheelEvent(scrollWheel.X, scrollWheel.Y);
    }

    private void CreateDeviceObjects()
    {
        // Create vertex array
        _vertexArray = _gl.GenVertexArray();
        _gl.BindVertexArray(_vertexArray);

        // Create buffers
        _vertexBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();

        // Create shader program
        string vertexShaderSource = @"
#version 300 es
precision mediump float;
layout (location = 0) in vec2 Position;
layout (location = 1) in vec2 UV;
layout (location = 2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy, 0, 1);
}";

        string fragmentShaderSource = @"
#version 300 es
precision mediump float;
in vec2 Frag_UV;
in vec4 Frag_Color;
uniform sampler2D Texture;
out vec4 Out_Color;
void main()
{
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
}";

        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);

        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Create font texture
        CreateFontTexture();
    }

    private unsafe void CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

        _fontTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, 
            PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

        io.Fonts.SetTexID((IntPtr)_fontTexture);
        io.Fonts.ClearTexData();
    }

    public void Update(float deltaTime)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_window.Size.X, _window.Size.Y);
        io.DeltaTime = deltaTime;
        
        _windowWidth = _window.Size.X;
        _windowHeight = _window.Size.Y;
    }

    public void NewFrame()
    {
        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        RenderImGuiDrawData(ImGui.GetDrawData());
    }

    private unsafe void RenderImGuiDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
            return;

        // Setup render state
        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.ScissorTest);

        // Setup viewport and projection matrix
        _gl.Viewport(0, 0, (uint)_windowWidth, (uint)_windowHeight);
        
        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
        
        Span<float> orthoProjection = stackalloc float[] {
            2.0f / (R - L), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (T - B), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (R + L) / (L - R), (T + B) / (B - T), 0.0f, 1.0f,
        };

        _gl.UseProgram(_shaderProgram);
        int projMtxLoc = _gl.GetUniformLocation(_shaderProgram, "ProjMtx");
        _gl.UniformMatrix4(projMtxLoc, 1, false, orthoProjection);

        _gl.BindVertexArray(_vertexArray);

        // Render command lists
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            // Upload vertex/index data
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()), 
                (void*)cmdList.VtxBuffer.Data, BufferUsageARB.StreamDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(cmdList.IdxBuffer.Size * sizeof(ushort)), 
                (void*)cmdList.IdxBuffer.Data, BufferUsageARB.StreamDraw);

            // Setup vertex attributes
            _gl.EnableVertexAttribArray(0);
            _gl.EnableVertexAttribArray(1);
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)Unsafe.SizeOf<ImDrawVert>(), (void*)0);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)Unsafe.SizeOf<ImDrawVert>(), (void*)8);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, (uint)Unsafe.SizeOf<ImDrawVert>(), (void*)16);

            for (int cmd_i = 0; cmd_i < cmdList.CmdBuffer.Size; cmd_i++)
            {
                var pcmd = cmdList.CmdBuffer[cmd_i];
                
                if (pcmd.UserCallback != IntPtr.Zero)
                    continue;

                var clipRect = pcmd.ClipRect;
                _gl.Scissor((int)clipRect.X, (int)(_windowHeight - clipRect.W), 
                    (uint)(clipRect.Z - clipRect.X), (uint)(clipRect.W - clipRect.Y));

                _gl.BindTexture(TextureTarget.Texture2D, (uint)pcmd.TextureId);
                _gl.DrawElementsBaseVertex(PrimitiveType.Triangles, pcmd.ElemCount, 
                    DrawElementsType.UnsignedShort, (void*)(pcmd.IdxOffset * sizeof(ushort)), 
                    (int)pcmd.VtxOffset);
            }
        }

        // Restore state
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.ScissorTest);
    }

    public void Dispose()
    {
        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyChar -= OnKeyChar;
            keyboard.KeyDown -= OnKeyDown;
            keyboard.KeyUp -= OnKeyUp;
        }
        
        foreach (var mouse in _input.Mice)
        {
            mouse.MouseDown -= OnMouseDown;
            mouse.MouseUp -= OnMouseUp;
            mouse.MouseMove -= OnMouseMove;
            mouse.Scroll -= OnScroll;
        }
        
        _gl.DeleteBuffer(_vertexBuffer);
        _gl.DeleteBuffer(_indexBuffer);
        _gl.DeleteVertexArray(_vertexArray);
        _gl.DeleteProgram(_shaderProgram);
        _gl.DeleteTexture(_fontTexture);
        ImGui.DestroyContext();
    }
}