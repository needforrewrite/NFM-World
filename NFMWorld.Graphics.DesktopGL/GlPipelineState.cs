// LLM maintained.
//
// The pipeline state object: a linked program plus every bit of fixed-function state the
// abstraction bundles into a PipelineDesc, applied in one place.
//
// GL has no pipeline object - state is global and set piecemeal - so unlike D3D11 there is nothing
// to hold a compiled state blob. What this type carries is the *description* of the state and the
// vertex-array object that goes with it, because the VAO is genuinely per-pipeline (it stores the
// attribute format, not the buffer) and is the one piece of GL state that has to be rebuilt
// whenever the layout changes.
using NFMWorld.Shaders;
using Silk.NET.OpenGL;

namespace NFMWorld.Graphics.DesktopGL;

/// <summary>
/// Per-pipeline GL state.
///
/// The vertex-array object is created here and its attributes are pointed at their buffers at draw
/// time. That split is not a preference - it is what ES 3.0 leaves available. The separated form
/// (<c>glVertexAttribFormat</c> + <c>glVertexAttribBinding</c> + <c>glBindVertexBuffer</c>) that
/// stores a *format* in the VAO and a *buffer* in a separate binding point arrived in ES 3.1, and
/// ANGLE's ES 3.0 context rejects all three calls with GL_INVALID_OPERATION. The classic form,
/// <c>glVertexAttribPointer</c>, does both jobs in one call and therefore needs the buffer bound at
/// the moment it is called - which is draw time, when the caller's <c>SetVertexBuffer</c> has
/// finally named one.
/// </summary>
internal sealed class GlPipelineState : IPipelineState
{
    private readonly GL _gl;
    private readonly GlDeletionQueue _deletions;

    public PipelineDesc Desc { get; }

    /// <summary>
    /// The abstraction's reflection, as carried by the shader module the program was built from.
    /// Handed to <see cref="GlShaderProgram"/> at construction and read back here, so the pipeline
    /// and the program can never disagree about it.
    /// </summary>
    public ShaderReflection Reflection { get; }

    internal GlShaderProgram Program { get; }

    /// <summary>The VAO holding this pipeline's attribute enable state and divisors.</summary>
    internal uint VertexArray { get; }

    /// <summary>
    /// Every attribute the pipeline's layouts declare, flattened into the order its locations are
    /// assigned, each remembering which vertex stream supplies it.
    ///
    /// The vertex-buffer slot is carried per <em>attribute</em> rather than per stream, because that
    /// is what <c>glVertexAttribPointer</c> needs: it takes the buffer currently bound to
    /// <c>GL_ARRAY_BUFFER</c>, so binding a stream and pointing its attributes at it has to happen
    /// attribute by attribute. A per-stream table would still have to be walked, and this way the
    /// stream is never a lookup.
    /// </summary>
    internal IReadOnlyList<GlAttributeBinding> Attributes { get; }

    /// <summary>
    /// The GL attribute locations the vertex stage actually declares. Built once from the linked
    /// program so a draw can check its layout against it.
    /// </summary>
    internal IReadOnlyDictionary<int, string> AttributeLocations { get; }

    internal GlPipelineState(GL gl, GlDeletionQueue deletions, GlShaderProgram program, PipelineDesc desc)
    {
        _gl = gl;
        _deletions = deletions;
        Program = program;
        Desc = desc;
        Reflection = program.Reflection;
        AttributeLocations = program.AttributeLocations;

        VertexArray = _gl.GenVertexArray();
        Attributes = AssignLocations(program, desc);
    }

    /// <summary>
    /// One attribute's place in the vertex stream: which location it occupies, which
    /// <c>SetVertexBuffer</c> slot supplies it, and the format and offset within that stream.
    /// </summary>
    internal readonly record struct GlAttributeBinding(
        uint Location,
        int VertexSlot,
        int InstanceStepRate,
        int Components,
        VertexAttribPointerType Type,
        bool Normalized,
        int OffsetInBytes);

    /// <summary>
    /// Numbers the pipeline's attributes by <em>position</em>: the attributes of
    /// <see cref="PipelineDesc.VertexLayouts"/> are numbered in order, slot 0's first, each
    /// consuming one location. That is not an arbitrary choice - it is how glslang assigns
    /// locations when it compiles the HLSL front-end, in declaration order, ignoring D3D semantic
    /// indices (the shader compiler's <c>ApplySemanticRemap</c> documents the same fact from the
    /// other side). A matrix input is the one case where the abstraction spells the expansion out:
    /// the app's instance layout declares TEXCOORD3..6 as four separate float4 attributes, one
    /// location each, exactly as fxc expands a <c>float4x4 world : TEXCOORD3</c>.
    ///
    /// Nothing is issued to GL here. The locations are numbers the draw path needs, and the VAO
    /// they would be recorded into is not worth binding twice - <c>BindVertexStreams</c> binds it
    /// once and does everything else there.
    /// </summary>
    private static List<GlAttributeBinding> AssignLocations(GlShaderProgram program, PipelineDesc desc)
    {
        var bindings = new List<GlAttributeBinding>();
        var location = 0u;

        for (var slot = 0; slot < desc.VertexLayouts.Count; slot++)
        {
            var layout = desc.VertexLayouts[slot];

            foreach (var attribute in layout.Attributes)
            {
                var (components, type, normalized) = attribute.Format.ToAttribFormat();

                // An attribute the shader stage optimised away has no location, so pointing a
                // vertex array at it would raise GL_INVALID_VALUE. The layout still describes it,
                // because the layout is the caller's and does not know what the compiler kept.
                // The span set is consulted rather than the name table because a matrix input is
                // one entry occupying several locations - see GlShaderProgram.AttributeLocationSpans.
                if (program.AttributeLocationSpans.Contains((int)location))
                {
                    bindings.Add(new GlAttributeBinding(
                        location, slot, layout.InstanceStepRate, components, type, normalized,
                        attribute.OffsetInBytes));
                }

                location++;
            }
        }

        return bindings;
    }

    /// <summary>
    /// Applies the fixed-function state and binds the VAO. Called on every <c>SetPipeline</c>
    /// rather than only when the pipeline changes between draws: GL state is global and any other
    /// caller (the ImGui renderer, a clear) can have touched it, so re-applying is the only way to
    /// be sure. It is cheap - a handful of small GL calls - compared to the bugs an incremental
    /// approach invites.
    ///
    /// The viewport and scissor rectangle are deliberately not set here. Both are keyed off the
    /// current render target's height, which only the command buffer knows, and both are applied
    /// there next to <c>SetRenderTarget</c>/<c>SetViewport</c>.
    /// </summary>
    internal void Apply(GL gl)
    {
        gl.UseProgram(Program.Handle);
        gl.BindVertexArray(VertexArray);

        ApplyBlend(gl);
        ApplyDepthStencil(gl);
        ApplyRasterizer(gl);
    }

    private void ApplyBlend(GL gl)
    {
        var blend = Desc.BlendState;
        if (blend.Enabled)
        {
            gl.Enable(GLEnum.Blend);
            // Separate colour and alpha, because BlendStateDesc models them separately - XNA's
            // AlphaBlend is premultiplied on the colour channels but not necessarily on alpha, and
            // collapsing the two into glBlendFunc would silently change it.
            gl.BlendFuncSeparate(
                blend.SourceColor.ToBlendFactor(), blend.DestinationColor.ToBlendFactor(),
                blend.SourceAlpha.ToBlendFactor(), blend.DestinationAlpha.ToBlendFactor());
            gl.BlendEquationSeparate(blend.ColorOperation.ToBlendEquation(), blend.AlphaOperation.ToBlendEquation());
        }
        else
        {
            gl.Disable(GLEnum.Blend);
        }

        var mask = blend.ColorWriteMask.ToColorMask();
        gl.ColorMask(mask[0], mask[1], mask[2], mask[3]);
    }

    private void ApplyDepthStencil(GL gl)
    {
        var ds = Desc.DepthStencilState;

        if (ds.DepthTestEnabled) gl.Enable(GLEnum.DepthTest); else gl.Disable(GLEnum.DepthTest);
        gl.DepthMask(ds.DepthWriteEnabled);
        gl.DepthFunc(ds.DepthCompare.ToDepthFunction());

        if (ds.StencilTestEnabled)
        {
            gl.Enable(GLEnum.StencilTest);
            gl.StencilMask((uint)ds.StencilWriteMask);

            // Front faces use the plain stencil* fields, back faces the ccw* ones, matching
            // FNA3D_DepthStencilState's two-sided layout. GL has no "two-sided" toggle - the two
            // faces always have their own state - so TwoSidedStencil only decides whether the back
            // face is configured from the ccw fields or mirrors the front.
            gl.StencilFuncSeparate(GLEnum.Front, ds.StencilFunction.ToStencilFunction(),
                ds.ReferenceStencil, (uint)ds.StencilReadMask);
            gl.StencilOpSeparate(GLEnum.Front, ds.StencilFail.ToStencilOp(),
                ds.StencilDepthFail.ToStencilOp(), ds.StencilPass.ToStencilOp());

            if (ds.TwoSidedStencil)
            {
                gl.StencilFuncSeparate(GLEnum.Back, ds.CcwStencilFunction.ToStencilFunction(),
                    ds.ReferenceStencil, (uint)ds.StencilReadMask);
                gl.StencilOpSeparate(GLEnum.Back, ds.CcwStencilFail.ToStencilOp(),
                    ds.CcwStencilDepthFail.ToStencilOp(), ds.CcwStencilPass.ToStencilOp());
            }
            else
            {
                // Back faces drawn with the same rules as front faces, which is what FNA does when
                // two-sided stencil is off.
                gl.StencilFuncSeparate(GLEnum.Back, ds.StencilFunction.ToStencilFunction(),
                    ds.ReferenceStencil, (uint)ds.StencilReadMask);
                gl.StencilOpSeparate(GLEnum.Back, ds.StencilFail.ToStencilOp(),
                    ds.StencilDepthFail.ToStencilOp(), ds.StencilPass.ToStencilOp());
            }
        }
        else
        {
            gl.Disable(GLEnum.StencilTest);
        }
    }

    private void ApplyRasterizer(GL gl)
    {
        var raster = Desc.RasterizerState;

        if (raster.CullMode == CullMode.None)
            gl.Disable(GLEnum.CullFace);
        else
        {
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(raster.CullMode.ToCullFaceMode());
        }

        // GL's front face is counter-clockwise by default and D3D11's is clockwise, but the ES
        // GLSL comes from SPIR-V through spirv-cross, which does not change the winding - so the
        // projection's handedness is what decides. Setting it explicitly rather than relying on
        // the default is what makes that one decision legible instead of implicit.
        gl.FrontFace(GLEnum.Ccw);

        // Wireframe has no ES 3.0 equivalent, so this is where that is refused. ToPolygonMode throws
        // for FillMode.Wireframe; the returned mode is discarded because there is nothing to set it
        // on - ES 3.0 has no glPolygonMode at all.
        _ = raster.FillMode.ToPolygonMode();

        if (raster.ScissorTestEnabled) gl.Enable(GLEnum.ScissorTest); else gl.Disable(GLEnum.ScissorTest);
    }

    public void Dispose()
    {
        // Queued rather than deleted, because this pipeline is owned by a static Effects field (see
        // GlPipelineState's remarks) and so is disposed whenever the process tears the renderer down
        // - which can be from a finalizer, where no GL delete resolves. See GlDeletionQueue.
        //
        // The program goes with it and in this order: the VAO is a container for the program's
        // attribute state, so it is the outer object of the two.
        _deletions.Request(GlObjectKind.VertexArray, VertexArray);
        Program.Dispose();
    }
}
