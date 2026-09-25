// LLM maintained.
using System.Runtime.InteropServices;
using System.Text;
// ExecutionModel exists in both namespaces with different underlying values; the one
// carried by EntryPoint and taken by SetEntryPoint is the SPIRV (not Cross) enum.
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Cross;
using ExecutionModel = Silk.NET.SPIRV.ExecutionModel;

namespace NFMWorld.ShaderCompiler;

/// <summary>
/// SPIR-V -> reflection + backend source, via SPIRV-Cross's C API.
///
/// This is the C# replacement for sokol-shdc's <c>reflection.cc</c> (837 lines) and
/// <c>spirvcross.cc</c> (581 lines). Uses only <c>spvc_</c> entry points, so nothing
/// here needs the <c>UnprotectedCompiler</c> subclass trick the C++ version uses to
/// reach <c>variable_is_depth_or_compare</c>.
///
/// Lifetime note: everything allocated off a <c>spvc_context</c> -- the parsed IR,
/// the compilers, their options, and the shader-resource lists -- is owned by that
/// context and freed by <c>spvc_context_destroy</c>. Only the context itself needs
/// explicit release.
/// </summary>
public sealed unsafe class SpirvCrossReflector : IDisposable
{
    private readonly Cross _cross = Cross.GetApi();

    public sealed class StageResult
    {
        public required StageReflection Reflection { get; init; }

        /// <summary>
        /// Vulkan-flavoured GLSL: separate <c>texture2D</c>/<c>sampler</c> declarations.
        /// This is the flavour reflection is derived from - the separate declarations are what
        /// expose the resource lists sokol's <c>views</c>/<c>samplers</c> are built from - and it
        /// is not directly compilable by a GL driver.
        /// </summary>
        public required string Glsl { get; init; }

        /// <summary>
        /// OpenGL-flavoured GLSL, with combined <c>sampler2D</c> uniforms and a
        /// <c>#version</c> sokol's GL backend accepts. This is the one that ships.
        /// </summary>
        public required string GlslGl { get; init; }

        /// <summary>
        /// OpenGL ES 3.0 GLSL (<c>#version 300 es</c>), for a GLES driver or ANGLE. This is a
        /// different target from <see cref="GlslGl"/> even though both are "GL": ES requires
        /// explicit precision qualifiers the desktop flavour omits, and it matches varyings by
        /// name where desktop GLSL matches them by <c>layout(location)</c>, so this pass renames
        /// the interface variables where <see cref="GlslGl"/> does not (see
        /// <c>HarmonizeVaryingNames</c>).
        /// </summary>
        public required string GlslEs { get; init; }

        public required string Msl { get; init; }
        public required string Hlsl { get; init; }
    }

    // Pointer types are not valid generic type arguments, so the BCL delegate shapes
    // (Action<T>, Func<T>) cannot be used for an options callback.
    private unsafe delegate void OptionConfigurator(CompilerOptions* options);

    /// <summary>Applies a per-compiler fixup after its options are installed.</summary>
    private unsafe delegate void Remapper(Compiler* compiler);

    /// <summary>
    /// Reflects <paramref name="spirv"/> and cross-compiles it to every backend.
    ///
    /// <paramref name="declaredInputs"/> is the vertex entry point's declared semantics, in
    /// declaration order, as recovered by <see cref="HlslSemantics"/>. It is only consulted for
    /// the vertex stage, and may be empty - the HLSL for a fragment stage, or for a vertex stage
    /// whose semantics could not be read, keeps spirv-cross's default naming.
    /// </summary>
    public StageResult ReflectAndCross(
        byte[] spirv,
        ShaderStage stage,
        string entryPoint,
        IReadOnlyList<HlslSemantics.DeclaredInput>? declaredInputs = null)
    {
        Context* context = null;
        Check(_cross.ContextCreate(&context), context, "context create");

        ParsedIr* ir = null;
        try
        {
            fixed (byte* p = spirv)
            {
                Check(_cross.ContextParseSpirv(context, (uint*)p, (UIntPtr)(spirv.Length / 4), &ir), context, "parse");
            }

            var model = stage == ShaderStage.Vertex ? ExecutionModel.Vertex : ExecutionModel.Fragment;

            // GLSL is the canonical backend for reflection: Vulkan-flavoured GLSL is the
            // only flavour carrying the separate texture2D/sampler declarations sokol-shdc
            // requires (it rejects combined image samplers outright).
            var glslCompiler = MakeCompiler(context, ir, model, entryPoint, Backend.Glsl, options =>
            {
                _cross.CompilerOptionsSetBool(options, CompilerOption.GlslVulkanSemantics, 1);
                _cross.CompilerOptionsSetBool(options, CompilerOption.RelaxNanChecks, 1);
                _cross.CompilerOptionsSetUint(options, CompilerOption.GlslVersion, 450);
            });
            var glsl = Compile(glslCompiler, context, "GLSL");

            // A second GLSL pass for the GL backend. It cannot reuse the compiler above: that
            // one has GlslVulkanSemantics set, which is exactly what makes it emit the separate
            // texture2D/sampler pairs reflection needs, and what a GL driver cannot compile.
            //
            // Uniform blocks are emitted as plain uniforms rather than as a std140 UBO, because
            // sokol's GL backend resolves each uniform by name through glGetUniformLocation
            // (sokol_gfx.h:11696) using the names in desc.uniform_blocks[].glsl_uniforms - so the
            // GLSL has to declare them as loose uniforms for those lookups to find anything.
            var glslGlCompiler = MakeCompiler(context, ir, model, entryPoint, Backend.Glsl, options =>
            {
                _cross.CompilerOptionsSetBool(options, CompilerOption.GlslEmitUniformBufferAsPlainUniforms, 1);
                _cross.CompilerOptionsSetBool(options, CompilerOption.RelaxNanChecks, 1);
                // 410 is the floor sokol documents for the desktop GL backend, and the lowest
                // version that supports the layout qualifiers the generated code uses.
                _cross.CompilerOptionsSetUint(options, CompilerOption.GlslVersion, 410);
            });
            // GL has one combined sampler object where Vulkan has separate image and sampler, so
            // the separate declarations have to be folded into combined sampler uniforms before
            // compilation. This is the step that makes the Vulkan-flavoured pass above unusable
            // as the shipped GLSL and this one usable.
            Check(_cross.CompilerBuildCombinedImageSamplers(glslGlCompiler), context, "build combined samplers");
            var glslGl = Compile(glslGlCompiler, context, "GLSL(GL)");

            // A third GLSL pass for OpenGL ES / ANGLE, and the only one of the four targets that is
            // not just a different spelling of an existing one. GlslES selects the dialect: it emits
            // `#version 300 es`, and with the precision options below the `precision highp float;`
            // declarations ES requires in every fragment shader - a shader that omits those does not
            // compile, so neither option is cosmetic.
            //
            // Like the desktop GL pass this needs combined samplers, and it deliberately does not
            // set GlslVulkanSemantics - an ES driver has no more use for separate texture2D and
            // sampler objects than a desktop one does.
            var glslEsCompiler = MakeCompiler(context, ir, model, entryPoint, Backend.Glsl, options =>
            {
                _cross.CompilerOptionsSetBool(options, CompilerOption.GlslES, 1);
                // Highp rather than the default mediump: it matches what the D3D path already gets,
                // and the game's shadow-map and outline maths depends on the extra range.
                _cross.CompilerOptionsSetBool(options, CompilerOption.GlslESDefaultFloatPrecisionHighp, 1);
                _cross.CompilerOptionsSetBool(options, CompilerOption.GlslESDefaultIntPrecisionHighp, 1);
                _cross.CompilerOptionsSetBool(options, CompilerOption.RelaxNanChecks, 1);
                _cross.CompilerOptionsSetUint(options, CompilerOption.GlslVersion, 300);
            });
            Check(_cross.CompilerBuildCombinedImageSamplers(glslEsCompiler), context, "build combined samplers (ES)");
            // The varying names have to be made to agree across the two stages before compiling,
            // which is a step the HLSL and desktop GLSL targets do not need. See the method.
            HarmonizeVaryingNames(glslEsCompiler, model);
            // Folding the separate image/sampler pairs into combined samplers leaves each one named
            // after the SPIR-V id rather than the resource, so the same names would not be findable
            // by glGetUniformLocation. See the method.
            NameCombinedSamplers(glslEsCompiler, context);
            var glslEs = Compile(glslEsCompiler, context, "GLSL(ES)");

            Resources* resources = null;
            StageReflection reflection;
            Check(_cross.CompilerCreateShaderResources(glslCompiler, &resources), context, "shader resources");
            reflection = ReflectStage(glslCompiler, resources, stage, entryPoint);

            var mslCompiler = MakeCompiler(context, ir, model, entryPoint, Backend.Msl, options =>
                _cross.CompilerOptionsSetUint(options, CompilerOption.MslVersion, 20100));
            var msl = Compile(mslCompiler, context, "MSL");

            var hlslCompiler = MakeCompiler(context, ir, model, entryPoint, Backend.Hlsl, options =>
                _cross.CompilerOptionsSetUint(options, CompilerOption.HlslShaderModel, 50),
                // Only the vertex stage has input semantics to restore, and only when the
                // caller could read them back out of the source.
                remap: stage == ShaderStage.Vertex && declaredInputs is { Count: > 0 }
                    ? compiler => ApplySemanticRemap(compiler, declaredInputs)
                    : null);
            var hlsl = Compile(hlslCompiler, context, "HLSL");
            if (stage == ShaderStage.Vertex && declaredInputs is { Count: > 0 })
                hlsl = FixMatrixColumnSemantics(hlsl, declaredInputs);

            return new StageResult
            {
                Reflection = reflection,
                Glsl = glsl,
                GlslGl = glslGl,
                GlslEs = glslEs,
                Msl = msl,
                Hlsl = hlsl,
            };
        }
        finally
        {
            // Frees the IR, the compilers, their options, and the resource lists.
            _cross.ContextDestroy(context);
        }
    }

    private Compiler* MakeCompiler(
        Context* context,
        ParsedIr* ir,
        ExecutionModel model,
        string entryPoint,
        Backend backend,
        OptionConfigurator configure,
        Remapper? remap = null)
    {
        Compiler* compiler = null;
        // Copy, not TakeOwnership: TakeOwnership *moves* the parsed IR into the compiler,
        // leaving it empty for every subsequent compiler built from the same IR. Three
        // backends are built per stage, so the IR has to stay usable.
        Check(_cross.ContextCreateCompiler(context, backend, ir, CaptureMode.Copy, &compiler), context, "create compiler");

        // Select which entry point to cross-compile, then rename it to `main` so the
        // emitted HLSL/GLSL/MSL is uniform regardless of the source entry point's name.
        // glslang renames the entry point to `main` itself when targeting SPIR-V, so ask
        // the IR what is actually there rather than assuming the HLSL function name.
        var actual = EntryPointOf(compiler, model) ?? entryPoint;
        Check(_cross.CompilerSetEntryPoint(compiler, actual, model), context, $"set entry point '{actual}'");
        if (actual != "main")
            Check(_cross.CompilerRenameEntryPoint(compiler, actual, "main", model), context, "rename entry point");

        CompilerOptions* options = null;
        Check(_cross.CompilerCreateCompilerOptions(compiler, &options), context, "create options");
        configure(options);
        Check(_cross.CompilerInstallCompilerOptions(compiler, options), context, "install options");
        // Must come after the options are installed, and only applies to the HLSL backend -
        // the remap table is an HLSL-signature concept.
        remap?.Invoke(compiler);
        return compiler;
    }

    /// <summary>
    /// Restores the source's semantic names into the HLSL spirv-cross emits.
    ///
    /// spirv-cross names every vertex input <c>TEXCOORD&lt;location&gt;</c>, position included
    /// (verified against the committed <c>out.Vertex.hlsl</c>, which declares
    /// <c>float4 v_Position : TEXCOORD0</c>). The alternative mechanism,
    /// <c>CompileOption.HlslUserSemantic</c>, is a no-op for this pipeline: it makes spirv-cross
    /// read <c>SpvDecorationUserSemantic</c> as a literal HLSL semantic, and glslang's HLSL
    /// front-end never emits that decoration.
    ///
    /// glslang assigns each vertex input's <c>Location</c> in *declaration order*, ignoring the
    /// semantic index, so a declaration's location is the sum of the registers declared before it.
    /// That was checked against the real <c>Line.fx</c> struct (whose semantic indices are
    /// deliberately out of declaration order) by <c>scratch/SemProbe/LocationProbe.cs</c>.
    /// </summary>
    private void ApplySemanticRemap(Compiler* compiler, IReadOnlyList<HlslSemantics.DeclaredInput> declaredInputs)
    {
        // glslang gives one stage input per source parameter, in declaration order, so the two
        // lists pair up 1:1 - a matrix stays one input carrying four columns rather than becoming
        // four inputs. Only the location each one landed on has to be read back.
        var reflected = ReflectInputs(compiler);

        // Each declaration's location is the running total of the registers before it, which is how
        // glslang numbers them: it assigns locations to the entry point's parameters in declaration
        // order *before* optimising, so a matrix counts as one location per column and an input the
        // stage never reads still consumes its slot. Poly's CreateShadowMapVS is the case that
        // proves both halves - it declares six inputs and uses two, and the surviving `world` matrix
        // is still at location 5 rather than renumbered down to 1.
        //
        // That same elimination is why the reflected list is a *subsequence* of the declared one
        // rather than a list of the same length, so the two are matched by location here. Pairing
        // them by index - as this did while every entry point happened to use every input it
        // declared - would have silently attributed `world`'s registers to `Normal`.
        var pinned = new List<GCHandle>();
        try
        {
            var table = new List<HlslVertexAttributeRemap>();
            var covered = new HashSet<int>();
            var location = 0;

            foreach (var declared in declaredInputs)
            {
                if (reflected.Any(r => r.Location == location))
                {
                    var handle = GCHandle.Alloc(Encoding.ASCII.GetBytes(declared.Semantic + "\0"), GCHandleType.Pinned);
                    pinned.Add(handle);
                    table.Add(new HlslVertexAttributeRemap
                    {
                        Location = (uint)location,
                        Semantic = (byte*)handle.AddrOfPinnedObject(),
                    });
                    covered.Add(location);

                    // A matrix input occupies one location per column, and the remap names the
                    // whole input with one semantic (FixMatrixColumnSemantics then numbers the
                    // columns), so the columns after the first are not separate remaps.
                }

                location += declared.Registers;
            }

            // An unremapped input would keep spirv-cross's `TEXCOORD<location>` name, which the
            // caller's layout is not allowed to know - the failure would surface at runtime as an
            // input layout that never binds, so it is raised here instead.
            var uncovered = reflected.Where(r => !covered.Contains(r.Location)).ToList();
            if (uncovered.Count > 0)
                throw new InvalidOperationException(
                    $"The vertex shader declares semantics at locations " +
                    $"[{string.Join(", ", declaredInputs.Select((d, i) => i))}] but the SPIR-V has " +
                    $"inputs at [{string.Join(", ", uncovered.Select(u => u.Location))}] " +
                    $"({string.Join(", ", uncovered.Select(u => u.Name))}) that no declared parameter " +
                    "accounts for; the two cannot be paired.");

            fixed (HlslVertexAttributeRemap* entries = table.ToArray())
                _cross.CompilerHlslAddVertexAttributeRemap(compiler, entries, (UIntPtr)table.Count);
        }
        finally
        {
            foreach (var handle in pinned) handle.Free();
        }
    }

    /// <summary>A stage input as the SPIR-V carries it: one location, or several for a matrix.</summary>
    private sealed record ReflectedInput(string Name, int Location, int Columns);

    /// <summary>
    /// Rewrites the semantic of a matrix input's columns.
    ///
    /// The remap table gives spirv-cross the semantic a source parameter was declared with, but a
    /// matrix parameter is carried as one input with one location, and spirv-cross has no way to
    /// express "this register is one further along" - it names the columns
    /// <c>&lt;semantic&gt;_&lt;column&gt;</c> (<c>TEXCOORD3_0</c>..<c>TEXCOORD3_3</c>), reusing the
    /// declared index rather than advancing it. fxc instead expands the matrix into consecutive
    /// registers with an ascending index, which is what the app's instance layout declares
    /// (TEXCOORD3..6), so the suffixes are corrected here.
    /// </summary>
    private static string FixMatrixColumnSemantics(string hlsl, IReadOnlyList<HlslSemantics.DeclaredInput> declaredInputs)
    {
        foreach (var declared in declaredInputs)
        {
            if (declared.Registers <= 1)
                continue;
            var (baseName, baseIndex) = SplitSemantic(declared.Semantic);
            for (var column = 0; column < declared.Registers; column++)
                hlsl = hlsl.Replace($": {declared.Semantic}_{column};", $": {baseName}{baseIndex + column};");
        }
        return hlsl;
    }

    /// <summary>
    /// Renames a stage's interface variables to a name derived from their location, so that the
    /// vertex and fragment stages independently arrive at the same name for the same varying.
    ///
    /// This is only needed for OpenGL ES, and it is needed for every shader, because ES 3.0 links
    /// varyings differently from both of the other targets:
    ///
    /// - HLSL matches a vertex output to a fragment input by <em>semantic</em>.
    /// - Desktop GLSL (this pipeline asks for 410 and up) matches by <em>layout(location)</em>,
    ///   which spirv-cross emits on varyings for those versions.
    /// - Core ES 3.0 matches by <em>name</em> alone. It cannot express varying locations: ANGLE
    ///   rejects them outright - "invalid layout qualifier: only valid on program inputs and
    ///   outputs" - and <c>GL_EXT_separate_shader_objects</c> is not among its extensions.
    ///
    /// spirv-cross names the two sides from different places, so they never coincide:
    /// <c>_entryPointOutput_Color</c> on the vertex side (after the entry point's return value)
    /// against <c>input_Color</c> on the fragment side (after the original HLSL parameter). Both
    /// stages compile cleanly and the mismatch appears only at link, as "FRAGMENT varying
    /// input_Color does not match any VERTEX varying".
    ///
    /// Renaming through the IR rather than the emitted text is what makes this safe: the rename
    /// covers the declaration and every use together, and it does not have to tell a varying apart
    /// from an identically-named vertex attribute - Poly's vertex stage has both an
    /// <c>input_Color</c> attribute and an <c>input_Color</c> varying, and a textual substitution
    /// rewrites the attribute declaration into a redefinition.
    ///
    /// Keying on location is also what the ES linker effectively does, so the two stages agree
    /// without either having to be compiled first and passed to the other.
    /// </summary>
    private unsafe void HarmonizeVaryingNames(Compiler* compiler, ExecutionModel model)
    {
        // In the vertex stage the varyings are outputs; in the fragment stage, inputs. Neither
        // stage's *other* interface is touched: the vertex attributes keep their names because
        // they are bound by location, which ES does allow on attributes.
        var varyings = model == ExecutionModel.Vertex ? ResourceType.StageOutput : ResourceType.StageInput;

        Resources* resources = null;
        Check(_cross.CompilerCreateShaderResources(compiler, &resources), null, "shader resources");

        ReflectedResource* list = null;
        UIntPtr count = 0;
        Check(_cross.ResourcesGetResourceListForType(resources, varyings, &list, &count), null, "varyings");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            var id = list[i].Id;

            // gl_Position, gl_PointSize and gl_FragCoord are matched by the driver under their own
            // built-in names and must keep them.
            if (_cross.CompilerHasDecoration(compiler, id, Decoration.BuiltIn) != 0)
                continue;

            var location = (int)_cross.CompilerGetDecoration(compiler, id, Decoration.Location);
            _cross.CompilerSetName(compiler, id, $"varying_{location}");
        }
    }

    /// <summary>
    /// Renames each combined image sampler after the texture it wraps, so that a GL driver can find
    /// it by the name the reflection reports.
    ///
    /// <c>CompilerBuildCombinedImageSamplers</c> folds the separate <c>texture2D</c>/<c>sampler</c>
    /// declarations into one <c>sampler2D</c> uniform per pair, and the variable it creates is named
    /// from the image's SPIR-V id - the emitted GLSL declares <c>uniform highp sampler2D _1062;</c>
    /// where the reflection says the texture is <c>ShadowMap0</c>. A GL backend resolves a sampler
    /// by <c>glGetUniformLocation(program, name)</c>, so without this every sampled draw binds
    /// nothing: the location lookup returns -1, the sampler uniform keeps its default texture unit
    /// 0, and the draw reads whatever texture happens to be bound there rather than failing.
    ///
    /// This matters for the ES target specifically. The desktop-GL pass has the same naming and the
    /// same lookup, but sokol's GL backend sidesteps it by declaring its own
    /// <c>glsl_uniforms</c> names in <c>sg_shader_desc</c> - a table this POC has no equivalent of,
    /// because nothing sits between the reflection and the driver. Renaming here makes the GLSL
    /// name and the reflected name agree, so the driver resolves the binding the same way the
    /// reflection describes it.
    ///
    /// Only the ES compiler is touched; the rename is not a property of the other targets.
    /// </summary>
    private unsafe void NameCombinedSamplers(Compiler* compiler, Context* context)
    {
        // The combined-sampler list is derived state, not decoration: it exists only after
        // BuildCombinedImageSamplers has run, which is why this is a separate step rather than
        // part of HarmonizeVaryingNames.
        CombinedImageSampler* samplers = null;
        UIntPtr count = 0;
        Check(_cross.CompilerGetCombinedImageSamplers(compiler, &samplers, &count), context, "combined samplers");

        for (nuint i = 0; i < (nuint)count; i++)
        {
            // Each pair knows the image and sampler it was built from. The image carries the
            // binding the reflection reports as the texture's slot, so naming the pair after the
            // image's own name is what makes the two agree.
            var imageId = samplers[i].ImageId;
            var name = _cross.CompilerGetNameS(compiler, imageId);
            if (string.IsNullOrEmpty(name))
                continue;

            _cross.CompilerSetName(compiler, samplers[i].CombinedId, name);
        }
    }

    private List<ReflectedInput> ReflectInputs(Compiler* compiler)
    {
        Resources* resources = null;
        Check(_cross.CompilerCreateShaderResources(compiler, &resources), null, "shader resources");

        var inputs = new List<ReflectedInput>();
        ReflectedResource* list = null;
        UIntPtr count = 0;
        Check(_cross.ResourcesGetResourceListForType(resources, ResourceType.StageInput, &list, &count), null, "stage inputs");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            var id = list[i].Id;
            var type = _cross.CompilerGetTypeHandle(compiler, list[i].TypeId);
            inputs.Add(new ReflectedInput(
                _cross.CompilerGetNameS(compiler, id) ?? $"input{i}",
                (int)_cross.CompilerGetDecoration(compiler, id, Decoration.Location),
                type is null ? 1 : Math.Max(1, (int)_cross.TypeGetColumns(type))));
        }
        inputs.Sort((a, b) => a.Location.CompareTo(b.Location));
        return inputs;
    }

    /// <summary>Splits a semantic into its base name and trailing index, defaulting the index to 0.</summary>
    private static (string Name, int Index) SplitSemantic(string semantic)
    {
        var split = semantic.Length;
        while (split > 0 && char.IsAsciiDigit(semantic[split - 1])) split--;
        return split == semantic.Length ? (semantic, 0) : (semantic[..split], int.Parse(semantic[split..]));
    }

    /// <summary>Returns the name of the IR's entry point for the given stage, or null if it has none.</summary>
    private string? EntryPointOf(Compiler* compiler, ExecutionModel model)
    {
        EntryPoint* entries = null;
        UIntPtr count = 0;
        Check(_cross.CompilerGetEntryPoints(compiler, &entries, &count), null, "get entry points");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            var name = Marshal.PtrToStringUTF8((nint)entries[i].Name);
            if (entries[i].ExecutionModel != model) continue;
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return null;
    }

    private string Compile(Compiler* compiler, Context* context, string what)
    {
        byte* source = null;
        var result = _cross.CompilerCompile(compiler, &source);
        if (result != Result.Success)
            throw new InvalidOperationException($"{what} cross-compile failed: {_cross.ContextGetLastErrorStringS(context)}");
        return Marshal.PtrToStringUTF8((nint)source) ?? "";
    }

    private StageReflection ReflectStage(Compiler* compiler, Resources* resources, ShaderStage stage, string entryPoint)
    {
        var inputs = new List<StageAttr>();
        ReflectedResource* list = null;
        UIntPtr count = 0;

        Check(_cross.ResourcesGetResourceListForType(resources, ResourceType.StageInput, &list, &count), null, "stage inputs");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            var id = list[i].Id;
            var name = _cross.CompilerGetNameS(compiler, id) ?? $"input{i}";
            var location = (int)_cross.CompilerGetDecoration(compiler, id, Decoration.Location);

            // The member's type id is carried on the resource itself; for an aggregate it differs
            // from the base type id, which is what distinguishes a matrix input's columns from
            // the vector a scalar/vector input takes.
            var type = _cross.CompilerGetTypeHandle(compiler, list[i].TypeId);
            var columns = type is null ? 1 : (int)_cross.TypeGetColumns(type);
            var vectorSize = type is null ? 4 : (int)_cross.TypeGetVectorSize(type);

            inputs.Add(new StageAttr(name, location, location, BaseType.Float, vectorSize, columns));
        }

        var textures = new List<TextureBinding>();
        Check(_cross.ResourcesGetResourceListForType(resources, ResourceType.SeparateImage, &list, &count), null, "textures");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            var id = list[i].Id;
            var name = _cross.CompilerGetNameS(compiler, id) ?? $"tex{i}";
            var binding = (int)_cross.CompilerGetDecoration(compiler, id, Decoration.Binding);
            textures.Add(new TextureBinding(name, binding, stage, (int)id));
        }

        var samplers = new List<SamplerBinding>();
        Check(_cross.ResourcesGetResourceListForType(resources, ResourceType.SeparateSamplers, &list, &count), null, "samplers");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            var id = list[i].Id;
            var name = _cross.CompilerGetNameS(compiler, id) ?? $"smp{i}";
            var binding = (int)_cross.CompilerGetDecoration(compiler, id, Decoration.Binding);
            samplers.Add(new SamplerBinding(name, binding, stage, (int)id));
        }

        var blocks = new List<UniformBlock>();
        Check(_cross.ResourcesGetResourceListForType(resources, ResourceType.UniformBuffer, &list, &count), null, "uniform buffers");
        for (nuint i = 0; i < (nuint)count; i++)
        {
            // For a uniform buffer, BaseTypeId is the block's struct type (the thing with
            // the members) and Id is the variable that carries the Binding decoration.
            var id = list[i].Id;
            var name = _cross.CompilerGetNameS(compiler, list[i].BaseTypeId)
                    ?? _cross.CompilerGetNameS(compiler, id) ?? $"ub{i}";
            var binding = _cross.CompilerGetDecoration(compiler, id, Decoration.Binding);
            blocks.Add(ReflectBlock(compiler, id, list[i].BaseTypeId, binding, name));
        }

        return new StageReflection(stage, entryPoint, inputs, blocks)
        {
            Textures = textures,
            Samplers = samplers,
        };
    }

    private UniformBlock ReflectBlock(Compiler* compiler, uint id, uint structTypeId, uint binding, string name)
    {
        var type = _cross.CompilerGetTypeHandle(compiler, structTypeId);
        if (type is null)
            return new UniformBlock(name, name, 0, Array.Empty<UniformMember>(), (int)binding);

        UIntPtr size = 0;
        Check(_cross.CompilerGetDeclaredStructSize(compiler, type, &size), null, "struct size");

        var members = new List<UniformMember>();
        var memberCount = _cross.TypeGetNumMemberTypes(type);
        for (uint m = 0; m < memberCount; m++)
        {
            // Member names hang off the struct type id, not the block variable's id.
            var memberName = _cross.CompilerGetMemberNameS(compiler, structTypeId, m) ?? $"m{m}";
            uint offset = 0;
            Check(_cross.CompilerTypeStructMemberOffset(compiler, type, m, &offset), null, "member offset");

            var memberType = _cross.CompilerGetTypeHandle(compiler, _cross.TypeGetMemberType(type, m));
            var baseType = memberType is null ? BaseType.Unknown : MapBaseType(_cross.TypeGetBasetype(memberType));
            var vectorSize = memberType is null ? 1 : (int)_cross.TypeGetVectorSize(memberType);
            var columns = memberType is null ? 1 : (int)_cross.TypeGetColumns(memberType);

            // The size spans the member's full span to the next member rather than the
            // scalar size, so an array or struct member reports what it actually occupies.
            UIntPtr memberSize = 0;
            _cross.CompilerGetDeclaredStructMemberSize(compiler, type, m, &memberSize);

            members.Add(new UniformMember(memberName, (int)offset, (int)memberSize, baseType, vectorSize, columns));
        }

        return new UniformBlock(name, name, (int)size, members);
    }

    private static BaseType MapBaseType(Basetype b) => b switch
    {
        Basetype.Boolean => BaseType.Bool,
        Basetype.Int32 or Basetype.Int16 or Basetype.Int64 or Basetype.Int8 => BaseType.Int,
        Basetype.Uint32 or Basetype.Uint16 or Basetype.Uint64 or Basetype.Uint8 => BaseType.Uint,
        Basetype.FP32 or Basetype.FP16 or Basetype.FP64 => BaseType.Float,
        _ => BaseType.Unknown,
    };

    private void Check(Result result, Context* context, string what)
    {
        if (result != Result.Success)
        {
            var msg = context is null ? "" : _cross.ContextGetLastErrorStringS(context);
            throw new InvalidOperationException($"{what} failed: {result} {msg}");
        }
    }

    public void Dispose() { }
}
