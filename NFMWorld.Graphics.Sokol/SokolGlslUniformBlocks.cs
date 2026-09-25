using System.Text;
using System.Text.RegularExpressions;
using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// Splits a stage's uniform block into several, because sokol's GL backend cannot describe a block
/// with more than <c>SG_MAX_UNIFORMBLOCK_MEMBERS</c> members.
///
/// <para>
/// The cap is <b>16</b> (<c>sokol_gfx.h:2179</c>) - an enum constant, not a macro, so it cannot be
/// raised without editing the submodule - and <c>glsl_uniforms</c> is a fixed array of that size
/// (<c>:3985</c>). Both loops that read it stop at 16 (<c>:24350</c> validation, <c>:11684</c>
/// runtime), so the tail of a larger block is dropped and the descriptor is rejected. Our blocks
/// reach 34 members (<c>LineBasic</c>, on the main-menu path) and 22 (<c>PolyBasic</c>), which is why
/// GLCORE cannot create a single shader without this.
/// </para>
///
/// <para>
/// <b>What sokol actually does with a block.</b> It does <em>not</em> create a uniform buffer. The GL
/// backend has no <c>glBindBufferRange</c> for <c>GL_UNIFORM_BUFFER</c> anywhere - the only
/// <c>glBindBufferRange</c> in the file is for <c>GL_SHADER_STORAGE_BUFFER</c> (<c>:10808</c>) - and
/// <c>_sg_gl_apply_uniforms</c> (<c>:12689</c>) walks the descriptor's members and calls
/// <c>glUniform*</c> once per member from a plain byte range. A "uniform block" on GL is therefore
/// just a named, strided slice of memory that gets splatted into loose uniforms, and a member's
/// <c>offset</c> is its byte offset <em>within that slice</em> (<c>:11694</c>). Splitting a block is
/// correspondingly cheap: the members stay loose uniforms, and nothing in the GLSL has to become a
/// real <c>uniform</c> block.
/// </para>
///
/// <para>
/// <b>Why the split is a GLSL rewrite and not a compiler change.</b> The compiler emits one merged
/// <c>_Global</c> and sokol needs it split, so the obvious place to split is where the block is
/// built. It is not where it belongs, for three measured reasons:
/// <list type="number">
/// <item>Both stages declare the <em>identical</em> member list, in all twelve programs (verified by
/// parsing the emitted GLSL), so there is no per-stage structure to preserve - only the block's
/// instance name differs, which is a SPIR-V id (<c>_325</c> in the vertex stage, <c>_197</c> in the
/// pixel stage of <c>LineBasic</c>).</item>
/// <item>The block's layout is plain std140: recomputing every member's offset with sokol's own
/// alignment rules reproduces the reflection's offsets exactly, in every program, with no
/// mismatches.</item>
/// <item>The backend already chooses shader sources per backend at runtime
/// (<see cref="SokolGraphicsDevice.LoadProgram"/>), so a GLCORE-only transform has a natural home,
/// and the compiler, the bundle format and the other backends stay untouched.</item>
/// </list>
/// The compiler-side alternative was measured and rejected. spirv-cross's
/// <c>GLSL_EMIT_UNIFORM_BUFFER_AS_PLAIN_UNIFORMS</c> - already set for this path
/// (<c>SpirvCrossReflector</c>) - flattens the block's <em>syntax</em> but not its storage: the
/// emitted GLSL still declares <c>struct _Global { ... }; uniform _Global _325;</c>, so the member
/// count that overflows sokol is unchanged, and the <c>N.Member</c> naming is left for us anyway.
/// Asking for separate loose <c>uniform</c> declarations instead would help nothing further - sokol
/// needs one <c>glsl_uniforms</c> entry per name either way, and a name costs a
/// <c>glGetUniformLocation</c> per member per program.
/// </para>
///
/// <para>
/// <b>The transform.</b> The single <c>struct _Global</c> and its <c>uniform _Global _325;</c>
/// declaration are replaced by one <em>loose</em> <c>uniform</c> per member - <c>_325.View</c>
/// becoming <c>uniform mat4 _325_View;</c> - grouped so that no group exceeds the member cap, and
/// every <c>_325.Member</c> reference is rewritten to the name the group that holds it declares.
/// Group 0 keeps the block's own instance name as its prefix, so its names read
/// <c>_325_Member</c>; each later group gets a suffix (<c>_325_g1_Member</c>).
/// </para>
///
/// <para>
/// <b>Loose uniforms rather than a struct per group, because sokol's GL backend cannot use a
/// struct at all.</b> This is the part that is easy to get wrong, and the shape is not free: sokol
/// has no way to describe a struct-shaped uniform. A block's members are resolved with
/// <c>glGetUniformLocation</c> from a flat <c>glsl_uniforms</c> table and then uploaded with a plain
/// <c>glUniform*</c> per member (<c>_sg_gl_apply_uniforms</c>, <c>sokol_gfx.h:12689</c>), and there
/// is no entry in that table that means "this member is really a struct field". A name inside a
/// struct is only addressable as the <em>whole</em> struct on the GL side, and sokol has no
/// <c>glUniformMatrix4fv</c>-per-field path for one. Crucially the upload format is not a choice
/// either: the member table is what sokol walks to decide the byte layout
/// (<c>:11686-11708</c>), so a group declared as a struct would have to be uploaded as one
/// <c>mat4</c>/<c>vec4</c> per member <em>anyway</em> - declaring it as a struct only makes the
/// driver-facing text disagree with the upload the driver receives.
/// </para>
///
/// <para>
/// This matches the reference implementation. sokol-shdc's <c>to_glsl</c> sets
/// <c>emit_uniform_buffer_as_plain_uniforms</c> and then calls
/// <c>compiler.flatten_buffer_block()</c> (<c>sokol-tools/src/shdc/spirvcross.cc</c>), which
/// rewrites a block into loose uniforms of exactly this shape - <c>_325_View</c> - and emits a
/// single <c>glsl_uniforms[0]</c> entry per block named after the block, array-typed (see
/// <c>generators/sokolc3.cc</c>). The setting alone does <em>not</em> do this: without the explicit
/// flatten call spirv-cross leaves the struct, and a <c>struct</c>/<c>uniform</c> pair that no
/// backend can describe is what this class exists to avoid.
/// </para>
///
/// <para>
/// Nothing else in the source is touched: the other <c>_N</c> identifiers spirv-cross emits are
/// plain locals, and the block's instance name never appears unqualified.
/// </para>
///
/// <para>
/// <b>Group boundaries land on 16 bytes, which is what makes this padding-free.</b> sokol does not
/// compare its computed member offsets against the GLSL at all - it <em>assigns</em> them. So the
/// only way the uploaded bytes can land correctly is if the offsets sokol derives from a group's
/// declared members are exactly those members' offsets in the flat buffer, relative to where the
/// group starts. Because a group's members keep their original order and each member's alignment is
/// satisfied at its original offset, that identity holds precisely when the group starts on a
/// 16-byte boundary: only then does the running offset need no extra padding to reach the next
/// member. (Verified for all twelve programs by recomputing every group's member offsets the way
/// sokol does and comparing against the reflection, both member by member and against sokol's
/// finished group size.)
/// </para>
///
/// <para>
/// So a group takes <em>at most</em> 16 members and ends at the last member whose successor is
/// 16-aligned relative to the group's start. Measured across all twelve programs that is never
/// fewer than 14 members per group and never needs a synthetic padding member: <c>LineBasic</c> (34
/// members) becomes groups of 15, 14 and 5, and <c>PolyBasic</c> (22) becomes 15 and 7. Every other
/// program fits in one group. A program whose members could not be grouped that way is rejected
/// rather than padded - a padding member is actively wrong here, because sokol would fold it into
/// its running offset and shift every member after it.
/// </para>
///
/// <para>
/// This is a GL-only concern. D3D11 and Metal describe a block by register and never read
/// <c>glsl_uniforms</c>, and Vulkan reads only the scalar <c>spirv_set0_binding_n</c>
/// (<c>:24341-24343</c>) - so all three keep the single merged block the compiler emits.
/// </para>
/// </summary>
internal static class SokolGlslUniformBlocks
{
    /// <summary>
    /// How many members one block may declare. Mirrors sokol's <c>SG_MAX_UNIFORMBLOCK_MEMBERS</c>
    /// (<c>sokol_gfx.h:2179</c>) and also bounds the resulting block count, which must stay within
    /// <c>SG_MAX_UNIFORMBLOCK_BINDSLOTS</c> (8, <c>:2183</c>). The worst program in this repo needs 3,
    /// and the arithmetic is bounded: even at the worst case of one member lost per boundary, 64
    /// members would still fit in 8 blocks.
    /// </summary>
    internal const int MaxMembersPerBlock = 16;

    /// <summary>
    /// One member of a split block.
    /// </summary>
    /// <param name="Name">The member's name, as the block's struct declares it.</param>
    /// <param name="Type">The sokol uniform type this member's GLSL type maps to.</param>
    /// <param name="QualifiedName">
    /// The name to hand sokol as this member's <c>glsl_name</c>, and the name the rewritten GLSL
    /// declares it under: the group's prefix, an underscore, then the member. A plain name rather
    /// than a dotted path, because the member is emitted as a loose uniform rather than as a field
    /// of a struct - see the class remarks. It is produced here rather than reconstructed by the
    /// caller so that the GLSL this class emits and the names the caller gives sokol cannot drift
    /// apart.
    /// </param>
    internal readonly record struct Member(string Name, sg_uniform_type Type, string QualifiedName);

    /// <summary>
    /// One block of the split, as the shader descriptor needs it.
    /// </summary>
    /// <param name="BlockOffset">
    /// Where this block starts in the program's flat uniform buffer. Members are uploaded from
    /// <c>flat member offset - BlockOffset</c>, which is what lets the command buffer keep treating
    /// <see cref="NFMWorld.Graphics.ICommandBuffer.SetUniform"/>'s slot as a flat byte offset.
    /// </param>
    /// <param name="SizeInBytes">
    /// The block's declared size: the span from its first member to its last, rounded up to 16. It is
    /// what sokol computes for the same member list, which is what its size validation compares
    /// against (<c>:24375</c>) and the length the command buffer uploads. Note this can exceed the
    /// span the members occupy - a 12-byte tail after the last 16-aligned member is uploaded but not
    /// read.
    /// </param>
    /// <param name="Members">The block's members, in declaration order.</param>
    internal readonly record struct Block(int BlockOffset, int SizeInBytes, IReadOnlyList<Member> Members);

    /// <summary>
    /// The result of a split.
    /// </summary>
    /// <param name="Source">The stage's GLSL, with its block split into several.</param>
    /// <param name="Blocks">
    /// The split blocks, each belonging on its own register in order. Empty when the source declares
    /// no uniform block, in which case <paramref name="Source"/> is returned unchanged.
    /// </param>
    internal readonly record struct Split(string Source, IReadOnlyList<Block> Blocks);

    /// <summary>
    /// Splits <paramref name="source"/>'s uniform block so that no block exceeds
    /// <see cref="MaxMembersPerBlock"/> members.
    ///
    /// <para>
    /// Returns the source untouched with no blocks when it declares no uniform block, which is the
    /// case for a program with no uniforms.
    /// </para>
    /// </summary>
    /// <param name="source">One stage's desktop-GL GLSL, as spirv-cross emitted it.</param>
    /// <param name="reflection">
    /// The program's reflection, whose <c>Uniforms</c> the command buffer uploads by. It is used
    /// both to map each member to the sokol uniform type the descriptor must declare and to check the
    /// std140 layout this method computes - see <see cref="VerifyLayout"/>.
    /// </param>
    /// <param name="stage">Which stage this source belongs to, for diagnostics only.</param>
    /// <exception cref="InvalidOperationException">
    /// On a source this cannot split correctly: a block member the GLSL and the reflection disagree
    /// about, an unsupported member type, a member declaration it cannot read, or a member list that
    /// will not divide into groups starting on 16-byte boundaries.
    /// </exception>
    internal static Split Apply(string source, ShaderReflection reflection, ShaderStage stage)
    {
        if (!FindBlock(source, stage, out var block))
            return new Split(source, []);

        var reflected = new Dictionary<string, (int Offset, UniformType Type)>(StringComparer.Ordinal);
        foreach (var uniform in reflection.Uniforms)
            reflected[uniform.Name] = (uniform.Offset, uniform.Type);

        // std140 offsets for the whole block, computed from the GLSL types. These are not a parallel
        // notion of the layout that merely happens to agree - the reflection was produced from the
        // same members by the same rules, and VerifyLayout keeps the two honest.
        //
        // `starts[i]` is where member i begins once its alignment is satisfied, and `extent` is where
        // the block's last member ends. Keeping the two apart matters: for a vec3 following a float,
        // the member's start (16-aligned) and its predecessor's end (4 past) are different numbers,
        // and conflating them is the difference between a correct block and a shifted one.
        var starts = new int[block.Members.Count];
        var extent = 0;
        for (var i = 0; i < block.Members.Count; i++)
        {
            var (type, _) = block.Members[i];
            extent = Align(extent, AlignmentOf(type, stage));
            starts[i] = extent;
            extent += SizeOf(type, stage);
        }

        VerifyLayout(block, starts, reflected, stage);

        var groups = Partition(starts, block, stage);
        var blocks = new List<Block>(groups.Count);
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        var unsigned = new List<string>();

        for (var g = 0; g < groups.Count; g++)
        {
            var (start, end) = groups[g];
            var prefix = Prefix(block.Instance, g);
            var members = new List<Member>(end - start);

            for (var i = start; i < end; i++)
            {
                var name = block.Members[i].Name;
                var loose = $"{prefix}_{name}";
                members.Add(new Member(name, UniformTypeOf(reflected[name].Type), loose));

                // Every group rewrites its own members, group 0 included: a loose uniform is not a
                // struct field, so even the first group's references have to lose the dot.
                references[$"{block.Instance}.{name}"] = loose;

                // Remembered for the declaration rewrite - see Rewrite. Keyed on the GLSL type, not
                // the reflection's, because it is the GLSL type the driver compares the upload
                // against.
                if (block.Members[i].Type == "uint")
                    unsigned.Add(loose);
            }

            // The group's last member ends at the next group's start, or at the block's end for the
            // final group. sokol aligns that span up to 16 and compares it against the declared size.
            var groupEnd = end == block.Members.Count ? extent : starts[end];
            blocks.Add(new Block(starts[start], Align(groupEnd - starts[start], 16), members));
        }

        return new Split(Rewrite(source, block, groups, references, unsigned, stage), blocks);
    }

    /// <summary>
    /// The uniform block a stage declares, if any: the struct a <c>uniform</c> declaration names,
    /// with its members.
    ///
    /// <para>
    /// Found by pairing the declarations rather than by taking the first struct, because a stage's
    /// GLSL may hold structs that are not blocks - interface blocks and multi-return functions are
    /// emitted as structs too - and only the one a <c>uniform</c> declaration names is a block. A
    /// <c>uniform sampler2D _1068;</c> has the same line shape, which is why the struct has to have
    /// been declared for the name to count; <c>sampler2D</c> is not a struct, so it is not one.
    /// </para>
    ///
    /// <para>
    /// Scanned line by line rather than matched with a pattern spanning the struct. The reading is
    /// identical for well-formed input, but a scanner sees every line, and seeing every line is what
    /// lets the members of the block it <em>selects</em> be read strictly - an unrecognised member is
    /// the case worth refusing, because skipping it would silently produce a block whose layout is
    /// wrong. Structs that are not the block are only delimited, never parsed, so a stage that grows
    /// an unrelated struct does not break the GL path.
    /// </para>
    /// </summary>
    private static bool FindBlock(string source, ShaderStage stage, out FoundBlock block)
    {
        block = default;

        var structs = new List<FoundStruct>();
        var uniforms = new List<FoundUniform>();

        var lines = source.Split('\n');
        var offset = 0;
        var open = -1; // index into `structs` of the struct being scanned

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineStart = offset;
            offset += line.Length + 1; // the newline Split removed
            var trimmed = line.Trim();
            var content = lineStart + (line.Length - line.TrimStart().Length);

            if (open >= 0)
            {
                if (trimmed == "};")
                {
                    structs[open].End = content + trimmed.Length;
                    open = -1;
                }
                else
                {
                    structs[open].Body.Add((trimmed, content));
                }

                continue;
            }

            if (trimmed.Length == 0 || trimmed == "{" || trimmed == "};")
                continue;

            if (TryReadStructHeader(trimmed, out var structName))
            {
                structs.Add(new FoundStruct(structName, lineStart, []));
                open = structs.Count - 1;
                continue;
            }

            if (TryReadUniformDeclaration(trimmed, out var uniformType, out var uniformName))
                uniforms.Add(new FoundUniform(uniformType, uniformName, lineStart, content + trimmed.Length));
        }

        FoundStruct? found = null;
        FoundUniform? declaration = null;

        foreach (var uniform in uniforms)
        {
            // Only a struct the source declares earlier can be the type of this uniform; anything
            // else is a built-in type such as sampler2D, which is described to sokol separately.
            var match = structs.FirstOrDefault(s => s.Name == uniform.Type && s.Start < uniform.Start);
            if (match is null)
                continue;

            if (declaration is not null)
                throw new InvalidOperationException(
                    $"The {stage} stage's GLSL declares {declaration.Name} and {uniform.Name} as " +
                    $"uniforms of the same struct {match.Name}, so its uniform block is ambiguous. " +
                    "Exactly one uniform of a block type is expected.");

            found = match;
            declaration = uniform;
        }

        if (found is null || declaration is null)
            return false;

        var members = ParseMembers(found, stage);

        block = new FoundBlock(
            found.Name,
            declaration.Name,
            members,
            found.Start,
            found.End - found.Start,
            declaration.Start,
            declaration.End - declaration.Start);
        return true;
    }

    /// <summary>
    /// The selected struct's members, in declaration order, rejecting anything the split cannot
    /// describe.
    /// </summary>
    private static List<(string Type, string Name)> ParseMembers(FoundStruct block, ShaderStage stage)
    {
        var members = new List<(string, string)>();

        foreach (var (line, offset) in block.Body)
        {
            // The opening brace is written on the line after the struct's name, so it is the one line
            // between the name and the members that is neither.
            if (line.Length == 0 || line == "{")
                continue;

            if (line.EndsWith(';'))
            {
                var words = line[..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 2 && IsIdentifier(words[0]) && IsIdentifier(words[1]))
                {
                    members.Add((words[0], words[1]));
                    continue;
                }
            }

            // Guessing past a line like this would produce a block whose layout is wrong rather than
            // one that fails, and a wrong layout on GL is a silently misplaced uniform, not an error.
            throw new InvalidOperationException(
                $"Cannot read the member \"{line}\" at offset {offset} of the {stage} stage's " +
                $"uniform block {block.Name}: it is not a plain `type name;` declaration. A block is " +
                "expected to hold uniform members only, one declaration per line.");
        }

        if (members.Count == 0)
            throw new InvalidOperationException(
                $"The {stage} stage's uniform block {block.Name} declares no members, which cannot be " +
                "split into blocks for sokol's GL backend.");

        return members;
    }

    /// <summary>
    /// Reads the name out of a <c>struct &lt;name&gt;</c> line, which carries no semicolon and may be
    /// followed by the brace on the same line or the next.
    /// </summary>
    private static bool TryReadStructHeader(string line, out string name)
    {
        name = string.Empty;

        const string keyword = "struct ";
        if (!line.StartsWith(keyword, StringComparison.Ordinal))
            return false;

        // Tolerate the brace being written on the same line, which spirv-cross does not do today but
        // which would otherwise read as part of the name.
        var rest = line[keyword.Length..].TrimEnd();
        if (rest.EndsWith('{'))
            rest = rest[..^1].TrimEnd();

        if (!IsIdentifier(rest))
            return false;

        name = rest;
        return true;
    }

    /// <summary>
    /// Reads a <c>uniform &lt;type&gt; &lt;name&gt;;</c> line, rejecting anything with a qualifier or
    /// an array suffix rather than reading a partial name out of it.
    /// </summary>
    private static bool TryReadUniformDeclaration(string line, out string type, out string name)
    {
        type = string.Empty;
        name = string.Empty;

        const string keyword = "uniform ";
        if (!line.StartsWith(keyword, StringComparison.Ordinal) || !line.EndsWith(';'))
            return false;

        var words = line[keyword.Length..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != 2 || !IsIdentifier(words[0]) || !IsIdentifier(words[1]))
            return false;

        type = words[0];
        name = words[1];
        return true;
    }

    private static bool IsIdentifier(string text)
    {
        if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
            return false;

        foreach (var c in text)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Divides the members into contiguous groups of at most <see cref="MaxMembersPerBlock"/>, each
    /// starting on a 16-byte boundary - the condition the class remarks derive for the split to be
    /// uploadable by sokol.
    ///
    /// <para>
    /// The boundary is tested relative to the group's own start, not against the block: a group is
    /// uploaded as a slice beginning at its first member, so a group-local offset of zero has to be
    /// 16-aligned for the members after it to land where the driver expects, whatever the absolute
    /// offset happens to be.
    /// </para>
    /// </summary>
    private static List<(int Start, int End)> Partition(int[] starts, FoundBlock block, ShaderStage stage)
    {
        var groups = new List<(int, int)>();
        var count = starts.Length;
        var start = 0;

        while (start < count)
        {
            var limit = Math.Min(start + MaxMembersPerBlock, count);
            var groupStart = starts[start];
            var end = 0;

            // The last member is always a legal end: only a block's *start* needs a boundary, and
            // sokol aligns the finished size up to 16 itself (:11709).
            for (var candidate = limit; candidate > start; candidate--)
            {
                if (candidate == count || (starts[candidate] - groupStart) % 16 == 0)
                {
                    end = candidate;
                    break;
                }
            }

            if (end == 0)
                throw new InvalidOperationException(
                    $"The {stage} stage's uniform block {block.Name} cannot be split: among the " +
                    $"{limit - start} members starting at {block.Instance}.{block.Members[start].Name}, " +
                    "none begins on a 16-byte boundary this block could be cut at, and sokol's GL " +
                    $"backend allows at most {MaxMembersPerBlock} members per block. The shader " +
                    "compiler would have to order or pad the block's members to make it splittable.");

            groups.Add((start, end));
            start = end;
        }

        return groups;
    }

    /// <summary>
    /// Checks the std140 layout computed from the GLSL against the reflection the command buffer
    /// uploads by, so a divergence fails here - where the offending member can be named - rather than
    /// as a silently misplaced uniform at the first draw.
    ///
    /// <para>
    /// Every program in this repo passes. It is a check rather than an assumption because getting it
    /// wrong does not throw: sokol logs <c>VALIDATE_SHADERDESC_UNIFORMBLOCK_SIZE_MISMATCH</c> for a
    /// size it disagrees with and <c>GL_UNIFORMBLOCK_NAME_NOT_FOUND_IN_SHADER</c> for a name it
    /// cannot resolve, then skips a member whose location is -1 - so the symptom would be a
    /// wrong-looking frame, not an error.
    /// </para>
    /// </summary>
    private static void VerifyLayout(
        FoundBlock block,
        int[] starts,
        Dictionary<string, (int Offset, UniformType Type)> reflected,
        ShaderStage stage)
    {
        for (var i = 0; i < block.Members.Count; i++)
        {
            var (type, name) = block.Members[i];

            // A member the reflection does not describe cannot be given a correct type or size, and
            // sokol requires every declared member to carry a glsl_name (:24354) - so this is
            // unrepresentable rather than merely unexpected.
            if (!reflected.TryGetValue(name, out var expected))
                throw new InvalidOperationException(
                    $"The {stage} stage's GLSL declares {block.Name}.{name}, which the program's " +
                    "reflection does not describe. Every member of a uniform block has to be " +
                    "reflected, because sokol requires a glsl_name for each one and the command " +
                    "buffer uploads by the reflection's offsets.");

            var computedSize = SizeOf(type, stage);
            var expectedSize = UniformSizeOf(expected.Type);

            if (starts[i] != expected.Offset || computedSize != expectedSize)
                throw new InvalidOperationException(
                    $"The {stage} stage's {block.Name}.{name} lays out at {starts[i]}.." +
                    $"{starts[i] + computedSize} in std140, but the reflection says " +
                    $"{expected.Offset}..{expected.Offset + expectedSize}. The GLSL this backend was " +
                    "handed and the reflection the command buffer uploads by have diverged, so " +
                    "uniform values would land in the wrong places.");
        }
    }

    /// <summary>
    /// Emits the rewritten source: the block's struct and uniform declaration replaced by one loose
    /// <c>uniform</c> per member, with every qualified member reference pointed at the loose name.
    /// </summary>
    private static string Rewrite(
        string source,
        FoundBlock block,
        List<(int Start, int End)> groups,
        Dictionary<string, string> references,
        List<string> unsigned,
        ShaderStage stage)
    {
        var declarations = new StringBuilder();
        for (var g = 0; g < groups.Count; g++)
        {
            var (start, end) = groups[g];
            var prefix = Prefix(block.Instance, g);

            // One loose uniform per member. The declaration order and the group boundaries are what
            // sokol's offsets are computed against, so members stay in the block's own order and each
            // group holds a contiguous run - the grouping is not a reordering.
            //
            // `uint` is declared as `int`, because sokol has no unsigned uniform type to declare:
            // its enum stops at MAT4 with FLOAT/INT families only, and there is no `glUniform*uiv`
            // anywhere in the GL backend. A `uniform uint` therefore gets described as INT and
            // uploaded with `glUniform1iv`, and in core GL that combination is either an error or a
            // silent no-op depending on the driver and type - measured on this machine as
            // GL_INVALID_OPERATION (0x0502) from every `sg_apply_uniforms` call, which is what
            // aborted the run. An `int` declaration matches the upload sokol actually issues.
            //
            // This is where every other backend's GLSL goes too, and they are all right for the same
            // reason: the u suffix is never load-bearing. Every use the HLSL frontend produces is
            // `x != 0u`, which computes identically for a signed int because the comparison is
            // against zero - see the class remarks.
            for (var i = start; i < end; i++)
            {
                var (type, name) = block.Members[i];
                var declared = type == "uint" ? "int" : type;
                declarations.AppendLine($"uniform {declared} {prefix}_{name};");
            }
        }

        // The uniform declaration follows the struct, so removing the later one first keeps both
        // recorded positions valid and the splice below needs no re-search. Order against the
        // reference rewriting is free either way: a reference only ever appears inside a function,
        // and spirv-cross emits the struct and its declaration ahead of every function, so changing
        // a reference's length shifts neither position.
        var text = source.Remove(block.UniformIndex, block.UniformLength);
        text = text[..block.StructIndex] + declarations + text[(block.StructIndex + block.StructLength)..];

        // Safe in this order - a rewrite cannot match what this method just wrote - because a
        // reference is the instance name, a dot and a member, while a declaration is the instance
        // name, an underscore and a member. No reference is a substring of any declaration.
        //
        // Longest reference first, because one member's name can be a prefix of another's: this
        // repo's `_Global` holds both `View` and `ViewProj`, and replacing the shorter one first
        // would rewrite the longer one's prefix and leave it naming the shorter one's uniform. It
        // does not bite today - `View` and `ViewProj` are both in group 0, whose prefix is the
        // instance name, so the clobbered name is the right one anyway - but it would the moment a
        // prefix pair straddled a group boundary, since the two prefixes then differ.
        foreach (var (from, to) in references.OrderByDescending(pair => pair.Key.Length))
            text = text.Replace(from, to);

        // The `int` declaration above is only equivalent where the value is compared against zero,
        // which is every use the HLSL frontend produces today. Anything else - a bitwise op, an
        // arithmetic result, a cast back to `uint` - could read the sign differently, and neither
        // that nor a plain compile error is diagnosable from the driver's log. Refused here rather
        // than guessed at, so the next person sees which member and which line forced the decision.
        foreach (var name in unsigned)
        {
            var word = new Regex($@"\b{Regex.Escape(name)}\b");

            foreach (var line in text.Split('\n'))
            {
                if (!word.IsMatch(line) || line.TrimStart().StartsWith("uniform ", StringComparison.Ordinal))
                    continue;

                if (line.Contains($"{name} != 0u", StringComparison.Ordinal) ||
                    line.Contains($"{name} == 0u", StringComparison.Ordinal))
                    continue;

                throw new InvalidOperationException(
                    $"The {stage} stage uses the unsigned uniform {name} somewhere other than a " +
                    $"comparison against zero: \"{line.Trim()}\". This backend declares it as `int`, " +
                    "because sokol has no unsigned uniform type and would upload an int-typed value " +
                    "to it either way, and that is only equivalent for a comparison against zero. So " +
                    "this member needs a different treatment - the compiler would have to stop " +
                    "emitting it as unsigned - not just the declaration rewrite applied here.");
            }
        }

        return text;
    }

    /// <summary>
    /// What a group prefixes its members with. Group 0 keeps the block's own instance name so that
    /// the bulk of the source - whose references are all to that instance - is recognisable in the
    /// output; the later groups are suffixed to keep every name distinct, since two loose uniforms
    /// may not share one.
    /// </summary>
    private static string Prefix(string instance, int group) =>
        group == 0 ? instance : $"{instance}_g{group}";

    /// <summary>
    /// std140 base alignment for the supported member types, matching <c>_sg_uniform_alignment</c>
    /// (<c>sokol_gfx.h:8925</c>).
    ///
    /// <para>
    /// Deliberately narrower than std140: these are the types this compiler's GLSL actually declares.
    /// It is narrower than the reflection's type set, too - a <c>Matrix4x4</c> is a <c>mat4</c>, but
    /// the reflection also collapses 2x2 and 3x3 matrices to <c>float4</c>
    /// (<c>SpirvCrossReflector</c>), which are <c>vec4</c> here. A type outside this set means an
    /// assumption this whole class rests on has changed, which is worth failing loudly for.
    /// </para>
    /// </summary>
    private static int AlignmentOf(string type, ShaderStage stage) => type switch
    {
        "float" or "int" or "uint" => 4,
        "vec2" or "ivec2" or "uvec2" => 8,
        "vec3" or "vec4" or "ivec3" or "ivec4" or "uvec3" or "uvec4" or "mat4" => 16,
        _ => throw new InvalidOperationException(
            $"The {stage} stage's uniform block declares a member of GLSL type {type}, whose std140 " +
            "alignment this backend does not know."),
    };

    /// <summary>
    /// std140 size for the supported member types, matching <c>_sg_uniform_size</c>
    /// (<c>sokol_gfx.h:8955</c>). A <c>vec3</c> occupies 12 bytes in a 16-byte-aligned slot, and a
    /// <c>mat4</c> spans four such slots - so neither the size nor the alignment can be derived from
    /// the other, and both have to be spelled out.
    /// </summary>
    private static int SizeOf(string type, ShaderStage stage) => type switch
    {
        "float" or "int" or "uint" => 4,
        "vec2" or "ivec2" or "uvec2" => 8,
        "vec3" or "ivec3" or "uvec3" => 12,
        "vec4" or "ivec4" or "uvec4" => 16,
        "mat4" => 64,
        _ => throw new InvalidOperationException(
            $"The {stage} stage's uniform block declares a member of GLSL type {type}, whose std140 " +
            "size this backend does not know."),
    };

    /// <summary>
    /// The sokol uniform type for a reflected uniform.
    ///
    /// <para>
    /// sokol's model has no boolean and no matrix type other than <c>MAT4</c> (<c>:2679-2692</c>),
    /// and the reflection already collapsed those cases - a GLSL <c>uint</c> in a block reflects as
    /// <see cref="UniformType.Int"/> (the shader's own booleans arrive as <c>uint</c> through the
    /// HLSL frontend), and a 2x2 or 3x3 matrix is widened to a <c>float4</c> column - so this is a
    /// rename-free pass-through.
    /// </para>
    /// </summary>
    private static sg_uniform_type UniformTypeOf(UniformType type) => type switch
    {
        UniformType.Float => sg_uniform_type.SG_UNIFORMTYPE_FLOAT,
        UniformType.Vector2 => sg_uniform_type.SG_UNIFORMTYPE_FLOAT2,
        UniformType.Vector3 => sg_uniform_type.SG_UNIFORMTYPE_FLOAT3,
        UniformType.Vector4 => sg_uniform_type.SG_UNIFORMTYPE_FLOAT4,
        UniformType.Matrix4x4 => sg_uniform_type.SG_UNIFORMTYPE_MAT4,
        UniformType.Int => sg_uniform_type.SG_UNIFORMTYPE_INT,
        _ => throw new InvalidOperationException($"Unhandled reflected uniform type {type}."),
    };

    /// <summary>
    /// The byte size sokol assigns a reflected uniform, which is the width of its slot in std140.
    /// </summary>
    private static int UniformSizeOf(UniformType type) => type switch
    {
        UniformType.Float or UniformType.Int => 4,
        UniformType.Vector2 => 8,
        UniformType.Vector3 => 12,
        UniformType.Vector4 => 16,
        UniformType.Matrix4x4 => 64,
        _ => throw new InvalidOperationException($"Unhandled reflected uniform type {type}."),
    };

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    /// <summary>
    /// A struct declaration found while scanning, delimited but not yet read. A reference type so that
    /// the scan can hold one open while it accumulates the body, and so that "no struct matched" is
    /// representable as null rather than as a default instance with no name.
    /// </summary>
    private sealed record FoundStruct(string Name, int Start, List<(string Line, int Offset)> Body)
    {
        /// <summary>Where the closing brace is, set once the body has been read.</summary>
        public int End { get; set; }
    }

    /// <summary>A <c>uniform</c> declaration found while scanning.</summary>
    private sealed record FoundUniform(string Type, string Name, int Start, int End);

    /// <summary>
    /// The uniform block a stage declares: what it is called, what its members are, and where its
    /// struct and its uniform declaration are so they can be replaced.
    /// </summary>
    private readonly record struct FoundBlock(
        string Name,
        string Instance,
        IReadOnlyList<(string Type, string Name)> Members,
        int StructIndex,
        int StructLength,
        int UniformIndex,
        int UniformLength);
}
