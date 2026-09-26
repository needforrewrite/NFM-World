// LLM maintained.
//
// The resource types behind the D3D11 backend: buffers, textures, samplers, render targets and the
// compiled shader module. Each is a thin owner of one or two COM objects - the interesting work is
// in what the device does at creation time and in the pipeline's state objects.
//
// The ownership model is different from the GL backends', and deliberately so. There is no deletion
// queue here: a COM Release is thread-safe and refcounted, so the reason GlDeletionQueue exists - a
// GL call from a finalizer runs against a context that is not current, and does something silently
// wrong rather than throwing - has no D3D11 counterpart. What replaces it is D3D11Interop.Release,
// which releases through a local and nulls it, so a second Dispose is a no-op instead of a
// double-free.
using NFMWorld.Shaders;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace NFMWorld.Graphics.D3D11;

/// <summary>
/// The bind, CPU-access and misc flags a resource can carry.
///
/// Spelled out as <c>uint</c> constants because that is how they are declared on the descriptor
/// structs: <c>BindFlags</c> and <c>CPUAccessFlags</c> are plain ints, and the enums that name the
/// bits (<c>D3D11_BIND_FLAG</c>, <c>D3D11_CPU_ACCESS_FLAG</c>) are documented as being ORed
/// together, which in C# means a cast at every use. One cast each, here.
/// </summary>
internal static class D3D11Flags
{
    internal const uint BindVertexBuffer = (uint)D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER;
    internal const uint BindIndexBuffer = (uint)D3D11_BIND_FLAG.D3D11_BIND_INDEX_BUFFER;
    internal const uint BindConstantBuffer = (uint)D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER;
    internal const uint BindShaderResource = (uint)D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE;
    internal const uint BindRenderTarget = (uint)D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET;
    internal const uint BindDepthStencil = (uint)D3D11_BIND_FLAG.D3D11_BIND_DEPTH_STENCIL;

    internal const uint CpuAccessWrite = (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE;
    internal const uint CpuAccessRead = (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ;
}

/// <summary>
/// A D3D11 buffer. The API distinguishes vertex, index and constant buffers only by bind flags, so
/// one type covers all three and the kind is recorded rather than encoded in the vtable.
/// </summary>
internal sealed unsafe class D3D11Buffer : IBuffer
{
    /// <summary>
    /// The resource. Held as <c>ID3D11Buffer*</c> for the resource calls and cast to a bare
    /// <c>IUnknown*</c> at the bind sites - <c>ID3D11Buffer</c> is not assignable to
    /// <c>ID3D11DeviceChild</c> in this assembly, because each interface is declared standalone
    /// rather than following the SDK's inheritance chain.
    /// </summary>
    internal ID3D11Buffer* Handle;

    public BufferKind Kind { get; }
    public BufferUsage Usage { get; }
    public int SizeInBytes { get; }

    /// <summary>
    /// The abstraction's index width. D3D11 takes it per draw rather than baking it into the buffer,
    /// but <c>ICommandBuffer.SetIndexBuffer</c> passes only the buffer, so it has to be remembered
    /// here - the same reason <c>GlBuffer</c> does.
    /// </summary>
    internal IndexFormat IndexFormat { get; }

    /// <summary>
    /// The buffer's CPU-side contents, kept only for a mutable buffer.
    ///
    /// Both the GL and sokol backends keep one, for the same reason: this backend's update path is a
    /// discard-map, which replaces the whole resource, so the bytes outside the updated range have to
    /// come from somewhere and no API offers a way to read them back. The mirror is that somewhere -
    /// see <see cref="Update"/>.
    /// </summary>
    private readonly byte[]? _mirror;

    internal D3D11Buffer(D3D11GraphicsDevice device, BufferDesc desc, ReadOnlySpan<byte> initialData)
    {
        Kind = desc.Kind;
        Usage = desc.Usage;
        SizeInBytes = desc.SizeInBytes;
        IndexFormat = desc.IndexFormat;

        // An immutable buffer is never written again, so it needs no copy. A mutable one keeps its
        // contents here from creation, so an update can re-send the whole buffer with only its own
        // range changed.
        _mirror = desc.Usage == BufferUsage.Dynamic ? new byte[desc.SizeInBytes] : null;
        if (_mirror is not null)
            initialData[..Math.Min(initialData.Length, desc.SizeInBytes)].CopyTo(_mirror);

        var d3dDesc = new D3D11_BUFFER_DESC
        {
            ByteWidth = (uint)desc.SizeInBytes,
            Usage = desc.Usage.ToUsage(),
            BindFlags = desc.Kind == BufferKind.Vertex ? D3D11Flags.BindVertexBuffer : D3D11Flags.BindIndexBuffer,
            CPUAccessFlags = desc.Usage == BufferUsage.Dynamic ? D3D11Flags.CpuAccessWrite : 0,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        // An immutable buffer declared without contents is legal, and the abstraction allows it: the
        // buffer is created by anything that needs the handle first and filled by UpdateBuffer
        // afterwards, which is how the deferred-upload renderers in this tree work.
        // From the mirror where there is one, because it - not initialData - is the source of truth: a
        // short initialData leaves the rest of the mirror zeroed, and zero is a defined value where the
        // storage a fresh resource gets would not be.
        D3D11_SUBRESOURCE_DATA initial = default;
        var contents = _mirror ?? initialData.ToArray();
        var hasInitial = contents.Length > 0;
        fixed (byte* data = contents)
        {
            if (hasInitial)
                initial.pSysMem = data;

            ID3D11Buffer* buffer = null;
            D3D11Interop.Check(
                device.Device->CreateBuffer(&d3dDesc, hasInitial ? &initial : null, &buffer),
                $"CreateBuffer ({desc.Kind}, {desc.Usage}, {desc.SizeInBytes} byte(s))");
            Handle = buffer;
        }
    }

    /// <summary>
    /// Records <paramref name="data"/> at <paramref name="offsetBytes"/> and re-sends the whole buffer
    /// through the map/discard path.
    ///
    /// <c>Map(WRITE_DISCARD)</c> rather than a mapped write in place, and the difference is not about
    /// speed: discard tells the driver the previous contents are dead, so it can hand back fresh
    /// storage instead of stalling until the GPU has finished reading the old. It is also the only one
    /// of the two D3D11 accepts on a <c>DYNAMIC</c> buffer - <c>UpdateSubresource</c> is rejected there
    /// outright, which is why <c>UploadUniforms</c> uses the other one on its <c>DEFAULT</c> buffer.
    ///
    /// Copying into the mirror first is what makes a sub-range write non-destructive: the bytes
    /// outside the range stay in the mirror, so re-sending all of it is equivalent to patching the
    /// range in place. A discard-map replaces the resource whole and there is no way to read the rest
    /// back, so the mirror is the only source there is for them.
    ///
    /// The upload is immediate rather than deferred to the draw, for the reason the GL backend's
    /// identical choice gives: deferring would coalesce a frame's writes into one upload, but tracking
    /// every buffer that might be read pulls in opposite directions at once - one created with its
    /// contents and drawn without an update needs flushing, one updated and never drawn needs
    /// uploading anyway - and a per-frame whole-buffer write is cheaper than tracking both.
    /// </summary>
    internal void Update(D3D11GraphicsDevice device, ReadOnlySpan<byte> data, int offsetBytes)
    {
        if (Usage != BufferUsage.Dynamic)
        {
            throw new NotSupportedException(
                $"This buffer was created with {nameof(BufferUsage)}.{Usage}; only " +
                $"{nameof(BufferUsage)}.{BufferUsage.Dynamic} buffers can be updated after creation.");
        }

        if (offsetBytes < 0 || offsetBytes + data.Length > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(data),
                $"{data.Length} byte(s) at offset {offsetBytes} overruns the {SizeInBytes}-byte buffer.");
        }

        data.CopyTo(_mirror.AsSpan(offsetBytes));

        D3D11_MAPPED_SUBRESOURCE mapped;
        D3D11Interop.Check(
            device.Context->Map((ID3D11Resource*)Handle, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, &mapped),
            "Map (buffer write)");

        // pData for a non-structured buffer is one contiguous run covering the whole resource, so a
        // single copy is a complete upload.
        fixed (byte* source = _mirror)
            Buffer.MemoryCopy(source, mapped.pData, SizeInBytes, SizeInBytes);

        device.Context->Unmap((ID3D11Resource*)Handle, 0);
    }

    public void Dispose() => D3D11Interop.Release(ref Handle);
}

/// <summary>
/// A 2D texture with its shader resource view.
///
/// The view is built eagerly rather than lazily: every texture a caller creates here is eventually
/// sampled, so a lazy one would only move a failure from creation (where the format and size are
/// still in hand) to a draw (where they are not).
/// </summary>
internal sealed unsafe class D3D11Texture : ITexture
{
    internal ID3D11Texture2D* Texture;
    internal ID3D11ShaderResourceView* View;

    public int Width { get; }
    public int Height { get; }
    public TextureFormat Format { get; }

    /// <summary>Whether the storage was allocated with a full mip chain.</summary>
    internal bool MipMapped { get; }

    /// <summary>
    /// Whether this texture is one of a render target's attachments.
    ///
    /// Recorded because it changes what the texture may be used for, and because <c>ReadTexture</c>
    /// consults it to reject a depth-stencil attachment by <em>name</em> rather than by format alone.
    /// Nothing here enforces the sampling rule - the abstraction's contract is the caller's to keep.
    /// </summary>
    internal bool RenderTargetable { get; }

    internal int SampleCount { get; }

    internal D3D11Texture(D3D11GraphicsDevice device, TextureDesc desc, ReadOnlySpan<byte> initialData, int sampleCount = 1)
    {
        Width = desc.Width;
        Height = desc.Height;
        Format = desc.Format;
        RenderTargetable = desc.RenderTargetable;
        SampleCount = sampleCount;

        // A mip chain and any other point in this class are mutually exclusive, and each exclusion is a
        // hard API rule rather than a policy: a multisampled resource has exactly one level, and a
        // depth-stencil resource cannot be viewed as a shader resource below level 0, so the chain
        // would be unreadable. Clearing the flag here means there is exactly one place that decides
        // whether a chain exists, and nothing downstream can be handed a texture whose MipMapped
        // disagrees with its actual level count.
        MipMapped = desc.MipMapped && sampleCount == 1 && !desc.Format.IsDepthFormat();

        // Derived from the cleared flag rather than from the request, so the two cannot disagree.
        var levels = MipMapped ? (uint)MipLevelCount(desc.Width, desc.Height) : 1u;

        var textureDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)desc.Width,
            Height = (uint)desc.Height,
            MipLevels = levels,
            ArraySize = 1,
            // The typeless resource format for a depth-stencil texture, the concrete one otherwise;
            // the view's format is what differs, and the pair is kept together in the mapping table.
            Format = desc.Format.ToDxgiFormat(),
            SampleDesc = new DXGI_SAMPLE_DESC { Count = (uint)sampleCount, Quality = 0 },
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11Flags.BindShaderResource
                | (desc.RenderTargetable
                    ? (desc.Format.IsDepthFormat() ? D3D11Flags.BindDepthStencil : D3D11Flags.BindRenderTarget)
                    : 0),
            // No CPU access: a default-usage resource is GPU-local. Readback goes through a separate
            // staging resource, which is what ReadTexture builds.
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        // Only level 0 can be supplied here - D3D11 has no mip-chain initialiser - and no caller in
        // this tree creates a mipmapped texture with data, so that gap is theoretical today.
        D3D11_SUBRESOURCE_DATA initial = default;
        var hasInitial = !initialData.IsEmpty;
        fixed (byte* data = initialData)
        {
            if (hasInitial)
            {
                initial.pSysMem = data;
                // The generated callers pack rows tightly, one row per level-0 line.
                initial.SysMemPitch = (uint)desc.Format.RowBytes(desc.Width);
                initial.SysMemSlicePitch = 0;
            }

            ID3D11Texture2D* texture = null;
            D3D11Interop.Check(
                device.Device->CreateTexture2D(&textureDesc, hasInitial ? &initial : null, &texture),
                $"CreateTexture2D ({desc.Width}x{desc.Height} {desc.Format}, {sampleCount}x)");
            Texture = texture;
        }

        if (sampleCount == 1)
            View = CreateView(device);
    }

    /// <summary>
    /// Builds the sampled view over this texture, or returns null when there is none to build.
    ///
    /// A multisampled texture gets no view, and that is an API rule rather than a shortcut: D3D11
    /// requires a multisampled view's format to be <c>_TYPELESS</c>, and this abstraction's formats
    /// are all concrete, so the desc this would have to pass is not one the caller named. Returning
    /// null says "there is nothing to sample" rather than hiding a mistake - D3D11RenderTarget's
    /// multisampled attachments are what the swapchain resolves, and a caller sampling one directly
    /// is making a mistake this class cannot see from here.
    ///
    /// A depth-stencil texture does get one, spelled as the depth half of its typeless family rather
    /// than as the resource's own typeless format. That is the one case where the view's format
    /// differs from the resource's, and the reason the two mappings live beside each other.
    /// </summary>
    private ID3D11ShaderResourceView* CreateView(D3D11GraphicsDevice device)
    {
        if (SampleCount > 1)
            return null;

        if (!Format.IsDepthFormat())
        {
            // A null desc means "the whole resource, in the resource's own format" - valid because a
            // colour resource's format is already concrete.
            ID3D11ShaderResourceView* colorView = null;
            D3D11Interop.Check(
                device.Device->CreateShaderResourceView((ID3D11Resource*)Texture, null, &colorView),
                $"CreateShaderResourceView ({Width}x{Height} {Format})");
            return colorView;
        }

        var viewDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC
        {
            Format = DXGI_FORMAT.DXGI_FORMAT_R24_UNORM_X8_TYPELESS,
            ViewDimension = D3D_SRV_DIMENSION.D3D_SRV_DIMENSION_TEXTURE2D,
        };
        // A view cannot declare more levels than the resource has, and a depth-stencil resource
        // never has a chain - see the MipMapped assignment in the constructor.
        viewDesc.Anonymous.Texture2D.MostDetailedMip = 0;
        viewDesc.Anonymous.Texture2D.MipLevels = 1;

        ID3D11ShaderResourceView* view = null;
        D3D11Interop.Check(
            device.Device->CreateShaderResourceView((ID3D11Resource*)Texture, &viewDesc, &view),
            $"CreateShaderResourceView ({Width}x{Height} {Format})");
        return view;
    }

    private static int MipLevelCount(int width, int height)
    {
        var levels = 1;
        while (width > 1 || height > 1)
        {
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
            levels++;
        }
        return levels;
    }

    /// <summary>
    /// Uploads a sub-rectangle of level 0.
    ///
    /// A boxed <c>UpdateSubresource</c> takes no source row-pitch parameter - it derives one from the
    /// box's width - so this is only expressible for a rectangle whose rows are laid out the way the
    /// box expects. Two callers in this tree upload sub-rectangles and both write <em>whole rows</em>
    /// within the box: the font atlas streams a run of complete rows, and ImGui uploads whole
    /// textures. So the requirement is a rectangle at x = 0 covering the texture's full width, and
    /// anything else is refused rather than silently sheared. Shearing is the failure mode here: the
    /// driver would read each destination row from the wrong offset in the source, which looks like a
    /// diagonal smear rather than an error.
    /// </summary>
    internal void Update(D3D11GraphicsDevice device, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (MipMapped)
        {
            throw new NotSupportedException(
                "Updating a mipmapped texture would leave every level past 0 stale, and regenerating " +
                "the chain on every update is a cost this POC does not take on. Create it single-level.");
        }

        if (x != 0 || width != Width)
        {
            throw new NotSupportedException(
                $"UpdateSubresource cannot express a source row pitch, so a sub-rectangle is only " +
                $"uploadable when it spans the texture's full width: got x={x}, width={width} of {Width}. " +
                "Update the whole row range instead.");
        }

        var box = new D3D11_BOX
        {
            left = 0,
            top = (uint)y,
            front = 0,
            right = (uint)Width,
            bottom = (uint)(y + height),
            back = 1,
        };

        fixed (byte* source = data)
            device.Context->UpdateSubresource((ID3D11Resource*)Texture, 0, &box, source, (uint)Format.RowBytes(Width), 0);
    }

    public void Dispose()
    {
        D3D11Interop.Release(ref View);
        D3D11Interop.Release(ref Texture);
    }
}

/// <summary>A D3D11 sampler state object.</summary>
internal sealed unsafe class D3D11Sampler : ISampler
{
    internal ID3D11SamplerState* Handle;

    public SamplerDesc Desc { get; }

    internal D3D11Sampler(D3D11GraphicsDevice device, SamplerDesc desc)
    {
        Desc = desc;

        var d3dDesc = new D3D11_SAMPLER_DESC
        {
            Filter = desc.Filter.ToFilter(),
            AddressU = desc.AddressU.ToAddressMode(),
            AddressV = desc.AddressV.ToAddressMode(),
            // The abstraction has no third axis and every texture here is 2D, so W is the same
            // decision as U rather than a second one.
            AddressW = desc.AddressU.ToAddressMode(),
            MipLODBias = 0f,
            MaxAnisotropy = 1,
            // Unused while every address mode is wrap, mirror or clamp - the border is only read by
            // BORDER - but it is a field either way, and the value here is the transparent black the
            // API's own default would give. Set rather than left implicit so that adding a BORDER
            // mode later is a one-line change and not a silent behavioural one.
            ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
            MinLOD = 0f,
            // Pinned to level 0, not left at the API's default of float.MaxValue. Every texture this
            // backend creates is single-level, and a sampler free to select a level that does not
            // exist reads as black - the same trap the GL backend's TextureMaxLevel guards against.
            MaxLOD = 0f,
        };
        d3dDesc.BorderColor.e0 = 0f;

        ID3D11SamplerState* sampler = null;
        D3D11Interop.Check(
            device.Device->CreateSamplerState(&d3dDesc, &sampler),
            $"CreateSamplerState ({desc.Filter}, U {desc.AddressU}, V {desc.AddressV})");
        Handle = sampler;
    }

    public void Dispose() => D3D11Interop.Release(ref Handle);
}

/// <summary>
/// A render target: a colour texture and, optionally, a depth-stencil one, each owned as an ordinary
/// <see cref="D3D11Texture"/> so they can be sampled afterwards - which is the whole point of an
/// off-screen target for the shadow-cascade passes this abstraction is built around.
///
/// The views live here rather than on the textures, because a texture has no idea what it is attached
/// to and the same texture could in principle be attached in two places.
/// </summary>
internal sealed unsafe class D3D11RenderTarget : IRenderTarget
{
    internal ID3D11RenderTargetView* ColorView;
    internal ID3D11DepthStencilView* DepthView;

    public D3D11Texture ColorTexture { get; }
    public D3D11Texture? DepthStencilTexture { get; }

    ITexture IRenderTarget.ColorTexture => ColorTexture;
    ITexture? IRenderTarget.DepthStencilTexture => DepthStencilTexture;

    internal D3D11RenderTarget(D3D11GraphicsDevice device, RenderTargetDesc desc, int sampleCount = 1)
    {
        ColorTexture = new D3D11Texture(
            device, new TextureDesc(desc.Width, desc.Height, desc.ColorFormat, RenderTargetable: true), default, sampleCount);

        // A null view desc means "the whole resource, in the resource's own format", which is only
        // valid against a typed resource - hence the mapping table's one typeless entry being the
        // depth-stencil pair, which is the one that cannot take a null desc.
        ID3D11RenderTargetView* colorView = null;
        D3D11Interop.Check(
            device.Device->CreateRenderTargetView((ID3D11Resource*)ColorTexture.Texture, null, &colorView),
            $"CreateRenderTargetView ({desc.Width}x{desc.Height} {desc.ColorFormat})");
        ColorView = colorView;

        if (!desc.HasDepthStencil)
            return;

        DepthStencilTexture = new D3D11Texture(
            device, new TextureDesc(desc.Width, desc.Height, desc.DepthStencilFormat, RenderTargetable: true), default, sampleCount);

        var depthViewDesc = new D3D11_DEPTH_STENCIL_VIEW_DESC
        {
            Format = desc.DepthStencilFormat.ToDepthStencilViewFormat(),
            // A multisampled resource needs the MS view dimension; the desc's union member is unused
            // in that case, so only the 2D arm's MipSlice is set below.
            ViewDimension = sampleCount > 1
                ? D3D11_DSV_DIMENSION.D3D11_DSV_DIMENSION_TEXTURE2DMS
                : D3D11_DSV_DIMENSION.D3D11_DSV_DIMENSION_TEXTURE2D,
            Flags = 0,
        };
        depthViewDesc.Anonymous.Texture2D.MipSlice = 0;

        ID3D11DepthStencilView* depthView = null;
        D3D11Interop.Check(
            device.Device->CreateDepthStencilView((ID3D11Resource*)DepthStencilTexture.Texture, &depthViewDesc, &depthView),
            $"CreateDepthStencilView ({desc.Width}x{desc.Height} {desc.DepthStencilFormat}, {sampleCount}x)");
        DepthView = depthView;
    }

    public void Dispose()
    {
        // Views before the resources they point into, so no view outlives its resource.
        D3D11Interop.Release(ref DepthView);
        D3D11Interop.Release(ref ColorView);
        ColorTexture.Dispose();
        DepthStencilTexture?.Dispose();
    }
}

/// <summary>
/// One program's two compiled stages plus its reflection, as a pipeline's
/// <c>VertexShader</c>/<c>PixelShader</c> pair.
///
/// The natural unit is the program rather than the stage, for the same reason sokol's module is: the
/// abstraction lets one instance be passed as both pipeline shaders, and the app does exactly that
/// (<c>Effects.Initialize</c> passes each bundle's module twice). The reflection is a property of the
/// program for the same reason - the shader compiler merges both stages into one <c>register(b0)</c>
/// block and one texture/sampler namespace.
///
/// Both stages are compiled at module creation rather than at pipeline creation, because a module is
/// built once per program while several pipelines can be built over it - the app's three Poly variants
/// differ only in depth-stencil state.
/// </summary>
internal sealed unsafe class D3D11ShaderModule : IShaderModule, IDisposable
{
    internal ID3D11VertexShader* VertexShader;
    internal ID3D11PixelShader* PixelShader;

    /// <summary>
    /// The vertex stage's DXBC. Kept because <c>CreateInputLayout</c> needs the bytecode it was
    /// compiled against, not the shader object - the input signature is read out of the blob.
    /// </summary>
    internal byte[] VertexBytecode { get; }

    public ShaderReflection Reflection { get; }

    /// <summary>
    /// Fixed to <see cref="ShaderStage.Vertex"/>, matching the FNA3D and sokol modules' convention of
    /// carrying a stage field the backend does not vary on.
    /// </summary>
    public ShaderStage Stage => ShaderStage.Vertex;

    public ReadOnlyMemory<byte> Bytecode => VertexBytecode;

    internal D3D11ShaderModule(D3D11GraphicsDevice device, ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection)
    {
        Reflection = reflection;

        VertexBytecode = D3D11ShaderCompiler.Compile(vertex.Hlsl, "vs_5_0", "vertex");
        var pixelBytecode = D3D11ShaderCompiler.Compile(pixel.Hlsl, "ps_5_0", "pixel");

        ID3D11VertexShader* vertexShader = null;
        fixed (byte* code = VertexBytecode)
            D3D11Interop.Check(
                device.Device->CreateVertexShader(code, (nuint)VertexBytecode.Length, null, &vertexShader),
                "CreateVertexShader");

        ID3D11PixelShader* pixelShader = null;
        fixed (byte* code = pixelBytecode)
            D3D11Interop.Check(
                device.Device->CreatePixelShader(code, (nuint)pixelBytecode.Length, null, &pixelShader),
                "CreatePixelShader");

        VertexShader = vertexShader;
        PixelShader = pixelShader;
    }

    public void Dispose()
    {
        D3D11Interop.Release(ref PixelShader);
        D3D11Interop.Release(ref VertexShader);
    }
}

/// <summary>
/// Compiles one stage's HLSL with <c>D3DCompile</c>.
/// </summary>
internal static unsafe class D3D11ShaderCompiler
{
    /// <summary>
    /// The entry point, for both stages. spirv-cross emits one <c>main</c> per stage, and the app's
    /// bundles are built from a single entry point each - the Nvg bundles carry four <c>PSMain*</c>
    /// sources in the file, but the bundle's <c>Pixel</c> field is the already-selected one.
    /// </summary>
    private const string EntryPoint = "main";

    /// <summary>
    /// <c>D3DCOMPILE_OPTIMIZATION_LEVEL3</c>. The level FNA3D's own MojoShader path asks for, and
    /// the reason it is not left at the default 1: at /O3 fxc drops the dead cbuffer members and
    /// reshapes the arithmetic, which is the same code the shipping D3D11 title ran. Levels 0 and 1
    /// also change the binary enough that a shader hashing its own cache key would miss.
    /// </summary>
    private const uint CompileFlags = 0x00000003;

    internal static byte[] Compile(string hlsl, string profile, string what)
    {
        // The pinned names have to be released, and they are pinned UTF-8-free ASCII because every
        // identifier in this HLSL is ASCII - the source itself carries no non-ASCII bytes either, so
        // the byte count is the character count.
        var sourceBytes = System.Text.Encoding.ASCII.GetBytes(hlsl);
        var profileBytes = System.Text.Encoding.ASCII.GetBytes(profile + "\0");
        var entryBytes = System.Text.Encoding.ASCII.GetBytes(EntryPoint + "\0");

        fixed (byte* source = sourceBytes)
        fixed (byte* profilePointer = profileBytes)
        fixed (byte* entryPointer = entryBytes)
        {
            ID3DBlob* blob = null;
            ID3DBlob* errors = null;

            var hr = DirectX.D3DCompile(
                source, (nuint)sourceBytes.Length, null, null, null,
                (sbyte*)entryPointer, (sbyte*)profilePointer, CompileFlags, 0, &blob, &errors);

            if (hr < 0)
            {
                var message = errors is null ? "" : "\n" + Read(errors);
                D3D11Interop.Release(ref errors);
                D3D11Interop.Release(ref blob);

                throw new InvalidOperationException(
                    $"{what} shader failed to compile (profile {profile}), 0x{hr.Value:X8}:{message}");
            }

            if (errors is not null)
            {
                // Warnings only - the compile succeeded, so this is not fatal. Surfaced rather than
                // dropped because a spirv-cross construct that only warns today is exactly the thing
                // that becomes an error on the next toolchain bump, and a silent warning is what
                // makes that a mystery later.
                GraphicsDiagnostics.Warning?.Invoke($"{what} shader compiled with warnings:\n{Read(errors)}");
                D3D11Interop.Release(ref errors);
            }

            var length = (int)blob->GetBufferSize();
            var result = new byte[length];
            new ReadOnlySpan<byte>(blob->GetBufferPointer(), length).CopyTo(result);
            D3D11Interop.Release(ref blob);

            return result;
        }
    }

    private static unsafe string Read(ID3DBlob* blob) =>
        System.Text.Encoding.ASCII.GetString((byte*)blob->GetBufferPointer(), (int)blob->GetBufferSize());
}
