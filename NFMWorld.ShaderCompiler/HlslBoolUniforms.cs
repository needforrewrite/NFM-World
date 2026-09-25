// LLM maintained.
//
// Recovers which of a source's uniforms are declared `bool`.
//
// SPIR-V has no boolean type inside a uniform block: HLSL's `bool` is promoted to a 4-byte
// integer before it is ever lowered, so the reflection spirv-cross hands back reports `uint`
// and cannot tell a boolean apart from a genuine integer. The distinction still matters to
// this compiler's output, because it decides which *EffectParameter wrapper a bundle exposes
// and therefore what a caller passes - `p.IsFullbright.SetValue(cb, true)` against
// BoolEffectParameter, rather than `SetValue(cb, 1u)` against UintEffectParameter.
//
// This is the problem HlslSemantics already solves for vertex input semantics, and the same
// answer applies: read it back out of the source text, which is the only place left that says it.
using System.Text.RegularExpressions;

namespace NFMWorld.ShaderCompiler;

/// <summary>
/// Finds the uniforms a shader source declares <c>bool</c>, and marks them in a reflection.
/// </summary>
public static class HlslBoolUniforms
{
    // A global-scope `bool Foo;`. Brace depth is tracked so the function-local and struct-field
    // declarations in Line.fx/Poly.fx/Mad.fxh (getsShadowed, isInLight0, ...) are not mistaken for
    // uniforms. The `#if`/`#else`/`#endif` lines around them are not braces, so they do not
    // disturb the depth.
    private static readonly Regex GlobalBool = new(
        @"^\s*bool\s+([A-Za-z_]\w*)\s*;", RegexOptions.Compiled);

    private static readonly Regex IncludeDirective = new(
        @"^\s*#\s*include\s+""([^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// The names declared <c>bool</c> at global scope in <paramref name="source"/> and in every
    /// file it includes.
    ///
    /// Includes are followed because a shader's uniforms need not be declared in the shader's own
    /// file: four of the eight sources take their shared uniforms from <c>Mad.fxh</c>. An include
    /// the resolver cannot supply is skipped rather than thrown on - a missing include is glslang's
    /// error to report, with its own message, when it compiles the same source.
    /// </summary>
    public static IReadOnlySet<string> Find(string source, Func<string, string?>? resolveInclude = null)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(source, resolveInclude, names, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return names;
    }

    /// <summary>
    /// A copy of <paramref name="reflection"/> in which the members named in
    /// <paramref name="boolNames"/> are typed <see cref="BaseType.Bool"/>.
    ///
    /// Only a member currently typed <see cref="BaseType.Uint"/> is re-typed, that being exactly
    /// the form a <c>bool</c> was promoted to - so a name that collides with an unrelated float or
    /// matrix uniform keeps its real type.
    /// </summary>
    public static StageReflection Mark(StageReflection reflection, IReadOnlySet<string> boolNames)
    {
        if (boolNames.Count == 0)
            return reflection;

        return reflection with
        {
            UniformBlocks = reflection.UniformBlocks.Select(block => block with
            {
                Members = block.Members
                    .Select(member => member.Type == BaseType.Uint && boolNames.Contains(member.Name)
                        ? member with { Type = BaseType.Bool }
                        : member)
                    .ToList(),
            }).ToList(),
        };
    }

    private static void Collect(
        string text,
        Func<string, string?>? resolveInclude,
        HashSet<string> names,
        HashSet<string> visitedIncludes)
    {
        var depth = 0;
        foreach (var line in text.Split('\n'))
        {
            // Only global scope declares a uniform, so anything nested is not a candidate - which
            // is also why the include scan belongs on this side of the check.
            if (depth == 0)
            {
                var include = IncludeDirective.Match(line);
                if (include.Success && resolveInclude is not null)
                {
                    var target = include.Groups[1].Value;
                    if (visitedIncludes.Add(target) && resolveInclude(target) is { } included)
                        Collect(included, resolveInclude, names, visitedIncludes);
                }
                else
                {
                    var declaration = GlobalBool.Match(line);
                    if (declaration.Success)
                        names.Add(declaration.Groups[1].Value);
                }
            }

            depth += line.Count(c => c == '{') - line.Count(c => c == '}');
        }
    }
}
