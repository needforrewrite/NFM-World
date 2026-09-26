// LLM maintained.
//
// The pipeline state object: the input layout, both shaders, and the three baked state objects the
// abstraction's PipelineDesc bundles together.
//
// This is the one place D3D11 and GL's backends differ structurally rather than in vocabulary. GL has
// no pipeline object - state is global and re-applied per draw - so GlPipelineState carries a
// *description*. Here the state genuinely is a set of objects, created once and bound with three
// pointer-sized calls, which is why Apply is so much shorter than its GL counterpart.
//
// The input layout is the interesting half. It is built by matching the caller's declared
// VertexLayoutDesc entries against the input signature D3DReflect reports for the compiled vertex
// shader, in the shader's declaration order, using the element count the shader declares.
using NFMWorld.Shaders;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace NFMWorld.Graphics.D3D11;

/// <summary>
/// Per-pipeline D3D11 state: an input layout plus the shader pair and the blend, depth-stencil and
/// rasterizer objects.
/// </summary>
internal sealed unsafe class D3D11PipelineState : IPipelineState
{
    private readonly D3D11GraphicsDevice _device;

    internal ID3D11InputLayout* InputLayout;
    private ID3D11BlendState* _blendState;
    private ID3D11DepthStencilState* _depthStencilState;
    private ID3D11RasterizerState* _rasterizerState;

    /// <summary>The stencil reference the depth-stencil state was baked with - see <see cref="Apply"/>.</summary>
    private uint _stencilReference;

    /// <summary>
    /// The input elements this pipeline's layout resolved to, kept for the smoke test.
    ///
    /// Not read by the draw path, which needs only the layout object. It is recorded because the
    /// element list is the whole content of the semantic contract - a wrong SemanticIndex or a wrong
    /// per-stream stride is invisible in a rendered frame and obvious in this table.
    /// </summary>
    internal IReadOnlyList<ResolvedElement> Elements { get; }

    public PipelineDesc Desc { get; }

    /// <summary>
    /// A record of one input element as it was resolved against the shader's signature.
    /// </summary>
    internal readonly record struct ResolvedElement(
        string Semantic, int SemanticIndex, DXGI_FORMAT Format, int InputSlot,
        int AlignedByteOffset, bool PerInstance, int InstanceStepRate);

    internal D3D11PipelineState(D3D11GraphicsDevice device, D3D11ShaderModule vertex, D3D11ShaderModule pixel, PipelineDesc desc)
    {
        _device = device;
        Desc = desc;

        Elements = BuildInputLayout(device, vertex.VertexBytecode, desc.VertexLayouts, out var layout);
        InputLayout = layout;

        CreateBlendState();
        CreateDepthStencilState();
        CreateRasterizerState();

        VertexShader = vertex;
        PixelShader = pixel;
    }

    internal D3D11ShaderModule VertexShader { get; }
    internal D3D11ShaderModule PixelShader { get; }

    /// <summary>
    /// The reflection the caller's <c>SetUniform</c> slots are resolved against. Taken from the
    /// vertex module, which is the same object the app passes as both shaders - a pipeline built
    /// from two different modules would be a caller error the shader compiler's merged block could
    /// not express anyway.
    /// </summary>
    public ShaderReflection Reflection => VertexShader.Reflection;

    /// <summary>
    /// Binds everything this pipeline owns. Four calls.
    ///
    /// The viewport and scissor rectangle are deliberately not set here. Both are keyed off the
    /// current render target's size, which only the command buffer knows, and both are applied there
    /// next to <c>SetRenderTarget</c>/<c>SetViewport</c> - the same split the GL backend makes.
    /// </summary>
    internal void Apply(D3D11GraphicsDevice device)
    {
        var context = device.Context;

        context->IASetInputLayout(InputLayout);
        context->VSSetShader(VertexShader.VertexShader, null, 0);
        context->PSSetShader(PixelShader.PixelShader, null, 0);
        context->RSSetState(_rasterizerState);

        // A null blend factor with an empty blend state: the abstraction's BlendStateDesc has no
        // BlendFactor field, so no caller can ask for one, and D3D11 only reads this array for the
        // BLEND_FACTOR blend modes that consequently never appear.
        context->OMSetBlendState(_blendState, null, uint.MaxValue);
        context->OMSetDepthStencilState(_depthStencilState, _stencilReference);
    }

    // ── input layout ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the input layout from the caller's declared layouts and the compiled vertex shader's
    /// own input signature.
    /// </summary>
    /// <remarks>
    /// The abstraction's contract is that the generated HLSL declares the <em>caller's</em> semantics
    /// - <c>POSITION0</c>/<c>POSITION1</c>/<c>POSITION2</c>, <c>NORMAL0</c>, <c>COLOR0</c>,
    /// <c>TEXCOORD0</c> through <c>TEXCOORD8</c> - so an element's name and index come straight from
    /// the layout entry and are never re-derived from the entry's position. That was verified against
    /// all thirteen generated bundles rather than assumed, because the alternative (renaming every
    /// input to <c>TEXCOORD&lt;location&gt;</c>, which is what upstream sokol-shdc does) would
    /// require appending the index to the name here instead, and the two produce identical-looking
    /// code that fails completely differently at runtime.
    ///
    /// The <em>order</em> is the shader's, though, not the caller's, and it has to be: D3D11's
    /// <c>CreateInputLayout</c> takes a semantic-name table that is consumed in the order given, and
    /// it validates that table against the shader's signature - so the elements have to be presented
    /// in declaration order and the count has to match. Two of this app's layouts happen to be written
    /// in that order already (Mesh's and LineMesh's both list Color before Centroid to follow the
    /// HLSL), but presenting them in their own order would depend on that coincidence and would
    /// break the first time a caller listed an attribute out of order. Walking the signature removes
    /// the coincidence from the contract.
    ///
    /// Every input the shader declares must be supplied, and that is a hard failure rather than a
    /// warning: an input the layout does not cover would read as whatever the input assembler's
    /// missing-attribute default is, which is <c>(0, 0, 0, 1)</c> - a silently wrong NORMAL or
    /// TEXCOORD, not a visible error. The reverse - a layout entry the shader does not declare - is
    /// not an error, and is handled by never being reached: only the shader's own inputs generate
    /// elements here, so an unused attribute is simply omitted from the layout, the same way the
    /// OpenGL backend's positional location numbering ignores it.
    /// </remarks>
    private static List<ResolvedElement> BuildInputLayout(
        D3D11GraphicsDevice device, byte[] vertexBytecode, IReadOnlyList<VertexLayoutDesc> layouts,
        out ID3D11InputLayout* layoutObjectReturn)
    {
        var signature = D3D11Reflector.ReadInputSignature(vertexBytecode);
        if (signature.Count == 0)
        {
            throw new InvalidOperationException(
                "The compiled vertex shader declares no input parameters, so there is nothing to build " +
                "an input layout from. Every bundle in this tree declares at least one.");
        }

        var descriptors = new D3D11_INPUT_ELEMENT_DESC[signature.Count];
        var pins = new System.Runtime.InteropServices.GCHandle[signature.Count];
        var resolved = new List<ResolvedElement>(signature.Count);

        try
        {
            for (var i = 0; i < signature.Count; i++)
            {
                var (slot, attribute) = Find(layouts, signature[i], i);
                var layout = layouts[slot];

                if (layout.StrideInBytes <= 0)
                {
                    throw new InvalidOperationException(
                        $"Vertex layout slot {slot} declares a stride of {layout.StrideInBytes}.");
                }

                var name = D3D11Interop.PinAscii(attribute.Semantic);
                pins[i] = name;

                descriptors[i] = new D3D11_INPUT_ELEMENT_DESC
                {
                    SemanticName = D3D11Interop.AddressOf(name),
                    SemanticIndex = (uint)attribute.Slot,
                    Format = attribute.Format.ToDxgiFormat(),
                    InputSlot = (uint)slot,
                    // Relative to the stream's own start, so that a caller whose layout begins at a
                    // non-zero offset does not have every element shifted by it. The command buffer
                    // applies the same stream start again as the vertex buffer's own offset.
                    AlignedByteOffset = (uint)attribute.OffsetInBytes,
                    InputSlotClass = layout.InstanceStepRate > 0
                        ? D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_INSTANCE_DATA
                        : D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA,
                    InstanceDataStepRate = layout.InstanceStepRate > 0 ? (uint)layout.InstanceStepRate : 0,
                };

                resolved.Add(new ResolvedElement(
                    attribute.Semantic, attribute.Slot, descriptors[i].Format, slot,
                    attribute.OffsetInBytes, layout.InstanceStepRate > 0, layout.InstanceStepRate));
            }

            ID3D11InputLayout* layoutObject = null;
            fixed (D3D11_INPUT_ELEMENT_DESC* elementDescs = descriptors)
            fixed (byte* bytecode = vertexBytecode)
                D3D11Interop.Check(
                    device.Device->CreateInputLayout(
                        elementDescs, (uint)descriptors.Length, bytecode, (nuint)vertexBytecode.Length, &layoutObject),
                    $"CreateInputLayout ({descriptors.Length} element(s))");

            layoutObjectReturn = layoutObject;
            return resolved;
        }
        finally
        {
            // CreateInputLayout copies each semantic name into its own storage, so the pins are only
            // needed for the duration of the call. Letting them leak would be a permanent non-moving
            // GC handle per input element per pipeline.
            for (var i = 0; i < pins.Length; i++)
            {
                if (pins[i].IsAllocated)
                    pins[i].Free();
            }
        }
    }

    /// <summary>
    /// Finds the layout attribute a signature entry names.
    ///
    /// Matched on the name and index pair, which is exactly how D3D11 itself resolves them, and the
    /// error names both sides so a mismatch is legible - this is the failure a semantic-name
    /// convention change would produce, and it produces it identically for every bundle.
    /// </summary>
    private static (int Slot, VertexAttributeDesc Attribute) Find(
        IReadOnlyList<VertexLayoutDesc> layouts, D3D11Reflector.SignatureEntry entry, int position)
    {
        for (var slot = 0; slot < layouts.Count; slot++)
        {
            foreach (var attribute in layouts[slot].Attributes)
            {
                if (attribute.Slot == entry.SemanticIndex &&
                    attribute.Semantic.Equals(entry.Semantic, StringComparison.Ordinal))
                {
                    return (slot, attribute);
                }
            }
        }

        throw new InvalidOperationException(
            $"The compiled vertex shader's input {position} is '{entry.Semantic}{entry.SemanticIndex}', " +
            "which none of the pipeline's vertex layouts declares. The shader and the layout disagree " +
            "about the semantic contract - see D3D11PipelineState.BuildInputLayout.");
    }

    // ── baked state objects ─────────────────────────────────────────────────────────────────────

    private void CreateBlendState()
    {
        var blend = Desc.BlendState;

        var desc = new D3D11_BLEND_DESC
        {
            // Not exposed by the abstraction, so it is off: it would multiply coverage into alpha,
            // which no caller here asks for and which would silently change an alpha-blended draw.
            AlphaToCoverageEnable = false,
            // Off, so only render target 0's entry below is read. This backend never binds more than
            // one render target.
            IndependentBlendEnable = false,
        };

        desc.RenderTarget.e0 = new D3D11_RENDER_TARGET_BLEND_DESC
        {
            BlendEnable = blend.Enabled,
            SrcBlend = blend.SourceColor.ToBlend(),
            DestBlend = blend.DestinationColor.ToBlend(),
            BlendOp = blend.ColorOperation.ToBlendOp(),
            SrcBlendAlpha = blend.SourceAlpha.ToBlend(),
            DestBlendAlpha = blend.DestinationAlpha.ToBlend(),
            BlendOpAlpha = blend.AlphaOperation.ToBlendOp(),
            RenderTargetWriteMask = blend.ColorWriteMask.ToWriteMask(),
        };

        ID3D11BlendState* state = null;
        D3D11Interop.Check(
            _device.Device->CreateBlendState(&desc, &state),
            $"CreateBlendState ({blend.Enabled}, colour {blend.SourceColor}/{blend.DestinationColor} {blend.ColorOperation}, " +
            $"alpha {blend.SourceAlpha}/{blend.DestinationAlpha} {blend.AlphaOperation}, mask {blend.ColorWriteMask})");
        _blendState = state;
    }

    private void CreateDepthStencilState()
    {
        var ds = Desc.DepthStencilState;
        _stencilReference = unchecked((uint)ds.ReferenceStencil);

        var desc = new D3D11_DEPTH_STENCIL_DESC
        {
            DepthEnable = ds.DepthTestEnabled,
            DepthWriteMask = ds.DepthWriteEnabled
                ? D3D11_DEPTH_WRITE_MASK.D3D11_DEPTH_WRITE_MASK_ALL
                : D3D11_DEPTH_WRITE_MASK.D3D11_DEPTH_WRITE_MASK_ZERO,
            DepthFunc = ds.DepthCompare.ToComparison(),
            StencilEnable = ds.StencilTestEnabled,
            StencilReadMask = ds.StencilReadMask.ToStencilMask(),
            StencilWriteMask = ds.StencilWriteMask.ToStencilMask(),
        };

        // Front faces take the plain fields, back faces the ccw* ones - matching
        // FNA3D_DepthStencilState's two-sided layout and the GL backend's binding of GL_FRONT/GL_BACK.
        //
        // "Front" means the counter-clockwise face, and that is a decision made in the rasterizer
        // below (FrontCounterClockwise = true) rather than here - which is why this comment and that
        // field have to be read together. D3D11's FrontFace is whichever winding the rasterizer
        // names front, so the abstraction's front-facing stencil state lands on the same triangles
        // here as it does on GL with its default GL_CCW front face.
        desc.FrontFace = new D3D11_DEPTH_STENCILOP_DESC
        {
            StencilFailOp = ds.StencilFail.ToStencilOp(),
            StencilDepthFailOp = ds.StencilDepthFail.ToStencilOp(),
            StencilPassOp = ds.StencilPass.ToStencilOp(),
            StencilFunc = ds.StencilFunction.ToComparison(),
        };

        // GL has no two-sided toggle - both faces always have their own state - so its backend uses
        // TwoSidedStencil to choose between configuring the back face from the ccw fields and
        // mirroring the front. D3D11 is the same shape and gets the same treatment: a pipeline that
        // did not ask for two-sided stencil gets the front face's rules on both.
        desc.BackFace = ds.TwoSidedStencil
            ? new D3D11_DEPTH_STENCILOP_DESC
            {
                StencilFailOp = ds.CcwStencilFail.ToStencilOp(),
                StencilDepthFailOp = ds.CcwStencilDepthFail.ToStencilOp(),
                StencilPassOp = ds.CcwStencilPass.ToStencilOp(),
                StencilFunc = ds.CcwStencilFunction.ToComparison(),
            }
            : desc.FrontFace;

        ID3D11DepthStencilState* state = null;
        D3D11Interop.Check(
            _device.Device->CreateDepthStencilState(&desc, &state),
            $"CreateDepthStencilState (depth {ds.DepthTestEnabled}/{ds.DepthWriteEnabled} {ds.DepthCompare}, " +
            $"stencil {ds.StencilTestEnabled} two-sided {ds.TwoSidedStencil})");
        _depthStencilState = state;
    }

    private void CreateRasterizerState()
    {
        var raster = Desc.RasterizerState;

        var desc = new D3D11_RASTERIZER_DESC
        {
            FillMode = raster.FillMode.ToFillMode(),
            CullMode = raster.CullMode.ToCullMode(),
            // True, so counter-clockwise-wound triangles are the front face. That is the winding the
            // app's geometry and the GL backend's explicit glFrontFace(GL_CCW) both assume, and it is
            // what makes the stencil op descs above mean the same thing here as there.
            //
            // D3D11 is the odd one out in defaulting this to false - its convention is clockwise-front
            // - so leaving it would flip every cull and every front/back stencil section at once.
            FrontCounterClockwise = true,
            DepthBias = 0,
            // D3D11_FLOAT32_MAX, which is what FNA3D passes: the clamp is the "no clamp" sentinel, and
            // zero here would clamp the bias to nothing and disable it silently.
            DepthBiasClamp = float.MaxValue,
            SlopeScaledDepthBias = 0f,
            // On, so geometry outside the near/far planes is clipped rather than projected to
            // infinity. The abstraction exposes no depth-clip toggle and every caller wants clipping.
            DepthClipEnable = true,
            ScissorEnable = raster.ScissorTestEnabled,
            // On unconditionally, because this backend's swapchain renders through a multisampled
            // faux backbuffer. D3D11 uses this only to pick the quadrilateral line-AA algorithm over
            // the alpha one - it does not gate multisampling of triangles - but leaving it off would
            // make every line in the scene take the alpha path under MSAA, which is the visible
            // difference. The abstraction's RasterizerStateDesc has no field for it, so it cannot be
            // made per-pipeline without inventing one.
            MultisampleEnable = true,
            // Must be off while alpha blending is in use, which it is in several pipelines - D3D11
            // requires AntialiasedLineEnable = false unless the blend state disables alpha blending.
            AntialiasedLineEnable = false,
        };

        ID3D11RasterizerState* state = null;
        D3D11Interop.Check(
            _device.Device->CreateRasterizerState(&desc, &state),
            $"CreateRasterizerState ({raster.CullMode}, {raster.FillMode}, scissor {raster.ScissorTestEnabled})");
        _rasterizerState = state;
    }

    public void Dispose()
    {
        D3D11Interop.Release(ref _rasterizerState);
        D3D11Interop.Release(ref _depthStencilState);
        D3D11Interop.Release(ref _blendState);
        D3D11Interop.Release(ref InputLayout);
    }
}

/// <summary>
/// Reads a compiled vertex shader's input signature with D3DReflect.
///
/// This is the D3D11-only half of the semantic contract. The other backends never need it - GL
/// numbers attribute locations positionally and sokol is told the bindings explicitly - but D3D11's
/// input layout has to be built from the shader's own declaration, so the shader has to be asked.
/// </summary>
internal static unsafe class D3D11Reflector
{
    /// <summary>One input parameter of the vertex shader, as the DXBC signature declares it.</summary>
    internal readonly record struct SignatureEntry(string Semantic, int SemanticIndex);

    /// <summary>
    /// The vertex shader's input parameters, in declaration order.
    ///
    /// Only the names and indices are taken. The component type and mask are deliberately ignored:
    /// the abstraction's layout is the authority on an attribute's format, and D3D11 takes the format
    /// from the element rather than the signature - a mismatch between the two is caught by
    /// <c>CreateInputLayout</c> reporting the signature, not by validation here.
    /// </summary>
    internal static List<SignatureEntry> ReadInputSignature(byte[] vertexBytecode)
    {
        var iid = D3D11Interop.Iid<ID3D11ShaderReflection>();

        ID3D11ShaderReflection* reflection = null;
        fixed (byte* bytecode = vertexBytecode)
        {
            D3D11Interop.Check(
                DirectX.D3DReflect(bytecode, (nuint)vertexBytecode.Length, &iid, (void**)&reflection),
                "D3DReflect (vertex shader)");
        }

        try
        {
            D3D11_SHADER_DESC shaderDesc;
            D3D11Interop.Check(reflection->GetDesc(&shaderDesc), "ID3D11ShaderReflection::GetDesc");

            var entries = new List<SignatureEntry>((int)shaderDesc.InputParameters);
            for (var i = 0u; i < shaderDesc.InputParameters; i++)
            {
                D3D11_SIGNATURE_PARAMETER_DESC parameter;
                D3D11Interop.Check(
                    reflection->GetInputParameterDesc(i, &parameter),
                    $"ID3D11ShaderReflection::GetInputParameterDesc({i})");

                entries.Add(new SignatureEntry(
                    Ascii(parameter.SemanticName),
                    (int)parameter.SemanticIndex));
            }

            return entries;
        }
        finally
        {
            D3D11Interop.Release(ref reflection);
        }
    }

    /// <summary>
    /// A compiled constant buffer, as the DXBC reflection describes it: its size and the byte offset
    /// of every member. <see cref="Members"/> is empty when the shader declares no such buffer.
    /// </summary>
    internal sealed record ConstantBufferReflection(uint Size, Dictionary<string, int> Members);

    /// <summary>
    /// The <c>_Global</c> constant buffer's own layout, out of the compiled vertex shader.
    ///
    /// This is what the smoke test compares the generated bundle's reflection against. The two
    /// describe the same block by construction - both come from the shader compiler's packoffsets -
    /// so a disagreement means the compiler and the C# bundle have drifted apart, and would show up as
    /// every uniform landing at the wrong offset with nothing else about the frame looking wrong.
    ///
    /// The name is fixed rather than a parameter because <c>_Global</c> is the one buffer the shader
    /// compiler emits; a shader declaring a second would need <c>GetConstantBufferByIndex</c> and the
    /// caller's slot convention would have to grow a buffer index, which nothing in this tree does.
    /// </summary>
    internal static ConstantBufferReflection? ReflectUniformBlock(byte[] vertexBytecode)
    {
        var iid = D3D11Interop.Iid<ID3D11ShaderReflection>();

        ID3D11ShaderReflection* reflection = null;
        fixed (byte* bytecode = vertexBytecode)
        {
            D3D11Interop.Check(
                DirectX.D3DReflect(bytecode, (nuint)vertexBytecode.Length, &iid, (void**)&reflection),
                "D3DReflect (vertex shader)");
        }

        var name = D3D11Interop.PinAscii("_Global");
        try
        {
            var buffer = reflection->GetConstantBufferByName(D3D11Interop.AddressOf(name));
            if (buffer == null)
                return null;

            D3D11_SHADER_BUFFER_DESC bufferDesc;
            D3D11Interop.Check(buffer->GetDesc(&bufferDesc), "ID3D11ShaderReflectionConstantBuffer::GetDesc");

            var members = new Dictionary<string, int>((int)bufferDesc.Variables);
            for (var i = 0u; i < bufferDesc.Variables; i++)
            {
                var variable = buffer->GetVariableByIndex(i);

                D3D11_SHADER_VARIABLE_DESC variableDesc;
                D3D11Interop.Check(
                    variable->GetDesc(&variableDesc),
                    $"ID3D11ShaderReflectionVariable::GetDesc({i})");

                members[MemberName(Ascii(variableDesc.Name))] = (int)variableDesc.StartOffset;
            }

            return new ConstantBufferReflection(bufferDesc.Size, members);
        }
        finally
        {
            name.Free();
            D3D11Interop.Release(ref reflection);
        }
    }

    /// <summary>
    /// Strips the mangling the shader compiler applies to every member of a declared constant buffer.
    /// </summary>
    /// <remarks>
    /// The generated HLSL declares its block as plain <c>cbuffer _Global : register(b0)</c> with
    /// members named <c>Projection</c>, <c>WorldViewProj</c> and so on - but the DXBC the offline
    /// compiler produces carries a per-shader-assembly prefix on each of them, so D3DReflect reports
    /// <c>_360_Projection</c> where the reflection the C# bundle carries says <c>Projection</c>. The
    /// number is not stable across compilations, so it cannot be matched by construction; what is
    /// stable is the structural rule below.
    ///
    /// The rule is: leading underscores, decimal digits, then one more underscore. A uniform whose own
    /// name has that shape is indistinguishable from a mangled one, and there are none in this shader
    /// set - <c>Shaders/Parameters.cs</c> names every member in camel or Pascal case. So a name that
    /// matches is always mangled, and one that does not is always the member's own, which is exactly
    /// the discrimination this needs.
    ///
    /// This is the D3D11 counterpart of <c>GlShaderProgram.MemberName</c>, which strips the
    /// <c>&lt;block&gt;.</c> qualifier ANGLE prefixes. Both exist because a backend that reads a
    /// driver's reflection has to reconcile that driver's spelling with the reflection the bundle
    /// was generated with - the names are the only key the two share besides the offsets.
    ///
    /// The <c>_Global</c> buffer's own name needs no such treatment: D3DReflect reports it verbatim.
    /// </remarks>
    internal static string MemberName(string reported)
    {
        if (string.IsNullOrEmpty(reported) || reported[0] != '_')
            return reported;

        var i = 0;
        while (i < reported.Length && reported[i] == '_')
            i++;

        var digitsStart = i;
        while (i < reported.Length && char.IsAsciiDigit(reported[i]))
            i++;

        // A trailing underscore only counts when there is something after it and at least one digit
        // before it: "_1" is a member's own name, and so is "__Restore".
        return i > digitsStart && i + 1 < reported.Length && reported[i] == '_'
            ? reported[(i + 1)..]
            : reported;
    }

    /// <summary>
    /// Reads a NUL-terminated ASCII name out of the reflection, which owns the storage until the
    /// reflector is released.
    /// </summary>
    private static unsafe string Ascii(sbyte* text)
    {
        if (text == null)
            return "";

        var length = 0;
        while (text[length] != 0)
            length++;

        return System.Text.Encoding.ASCII.GetString((byte*)text, length);
    }
}
