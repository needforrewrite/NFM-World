using System;
using System.Collections.Generic;
using System.Text;

// ReSharper disable InconsistentNaming

namespace NFMWorld.ShaderSourceGen;

/// <summary>
/// Minimal C preprocessor for HLSL files.
/// Supports: #define (object-like and function-like), #undef, #if, #ifdef, #ifndef,
/// #elif, #else, #endif, #include.
/// #if supports: defined(X), integer literals, !, &&, ||, parentheses.
/// </summary>
public sealed class HLSLPreprocessor
{
    private readonly Dictionary<string, string> _defines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionMacro> _functionMacros = new(StringComparer.Ordinal);
    private readonly Func<string, string?>? _includeResolver;
    private bool _emitLineDirectives = true;

    private readonly struct FunctionMacro(string[] parameters, string body)
    {
        public readonly string[] Parameters = parameters;
        public readonly string Body = body;
    }

    /// <param name="predefined">Pre-set defines (e.g. "OPENGL").</param>
    /// <param name="includeResolver">
    /// Optional callback that receives the path from <c>#include "path"</c> and returns file
    /// contents, or <c>null</c> if the file cannot be found.
    /// </param>
    public HLSLPreprocessor(
        IEnumerable<string>? predefined = null,
        Func<string, string?>? includeResolver = null
    )
    {
        _includeResolver = includeResolver;
        if (predefined != null)
            foreach (var d in predefined)
                _defines[d] = "1";
    }

    /// <summary>Define a macro with an optional replacement value.</summary>
    public void Define(string name, string value = "1") => _defines[name] = value;

    /// <summary>Whether to emit #line directives to map back to original sources. Default: true.</summary>
    public bool EmitLineDirectives
    {
        get => _emitLineDirectives;
        set => _emitLineDirectives = value;
    }

    /// <summary>Process source text through the preprocessor, returning the result.</summary>
    public string Process(string source, string? fileName = null)
    {
        var sb = new StringBuilder(source.Length);
        var lines = SplitLines(source);
        ProcessLines(lines, 0, lines.Length, sb, fileName);
        return sb.ToString();
    }

    // ── Core line processing ────────────────────────────────────────────────

    private void ProcessLines(string[] lines, int start, int end, StringBuilder sb, string? fileName)
    {
        int i = start;
        while (i < end)
        {
            var trimmed = lines[i].TrimStart();

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                var directive = ParseDirective(trimmed);

                switch (directive.Name)
                {
                    case "define":
                        HandleDefine(directive.Rest);
                        i++;
                        break;

                    case "undef":
                    {
                        var uname = directive.Rest.Trim();
                        _defines.Remove(uname);
                        _functionMacros.Remove(uname);
                        i++;
                        break;
                    }

                    case "if":
                    case "ifdef":
                    case "ifndef":
                        i = HandleConditionalBlock(lines, i, end, sb, fileName);
                        // Emit #line to resync after the skipped conditional block
                        EmitLine(sb, i, fileName);
                        break;

                    case "include":
                        HandleInclude(directive.Rest.Trim(), sb);
                        i++;
                        // Emit #line to restore original file context after include
                        EmitLine(sb, i, fileName);
                        break;

                    default:
                        // Unknown directive — pass through as-is
                        sb.AppendLine(lines[i]);
                        i++;
                        break;
                }
            }
            else
            {
                sb.AppendLine(ExpandMacros(lines[i]));
                i++;
            }
        }
    }

    private void EmitLine(StringBuilder sb, int nextLineIndex, string? fileName)
    {
        if (!_emitLineDirectives) return;
        // #line uses 1-based line numbers; nextLineIndex is 0-based
        int lineNum = nextLineIndex + 1;
        if (fileName != null)
            sb.AppendLine($"#line {lineNum} \"{fileName}\"");
        else
            sb.AppendLine($"#line {lineNum}");
    }

    // ── #define ──────────────────────────────────────────────────────────────

    private void HandleDefine(string rest)
    {
        rest = rest.Trim();
        if (rest.Length == 0) return;

        // Find end of macro name
        int nameEnd = 0;
        while (nameEnd < rest.Length && IsIdentChar(rest[nameEnd])) nameEnd++;
        var name = rest[..nameEnd];

        // Function-like macro: NAME( immediately after identifier (no space before paren)
        if (nameEnd < rest.Length && rest[nameEnd] == '(')
        {
            int parenStart = nameEnd + 1;
            int parenEnd = rest.IndexOf(')', parenStart);
            if (parenEnd < 0) return; // malformed

            var paramStr = rest.Substring(parenStart, parenEnd - parenStart).Trim();
            string[] parameters;
            if (paramStr.Length == 0)
                parameters = [];
            else
            {
                var parts = paramStr.Split(',');
                parameters = new string[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    parameters[i] = parts[i].Trim();
            }

            var body = (parenEnd + 1 < rest.Length) ? rest[(parenEnd + 1)..].Trim() : "";
            _functionMacros[name] = new FunctionMacro(parameters, body);
            return;
        }

        // Object-like macro
        int sep = IndexOfWhitespace(rest, nameEnd);
        if (sep < 0 || sep == rest.Length)
        {
            _defines[name] = "1";
        }
        else
        {
            var value = rest[(sep + 1)..].Trim();
            _defines[name] = value;
        }
    }

    // ── #include ────────────────────────────────────────────────────────────

    private void HandleInclude(string raw, StringBuilder sb)
    {
        // Strip quotes or angle brackets: "path" or <path>
        string path;
        if (raw.Length >= 2 && ((raw[0] == '"' && raw[^1] == '"') || (raw[0] == '<' && raw[^1] == '>')))
            path = raw.Substring(1, raw.Length - 2);
        else
            path = raw;

        var contents = _includeResolver?.Invoke(path);
        if (contents != null)
        {
            EmitLine(sb, 0, path);
            var included = Process(contents, path); // recursively preprocess with include's filename
            sb.Append(included);
        }
        // silently skip if resolver returns null
    }

    // ── #if / #ifdef / #ifndef / #elif / #else / #endif ─────────────────────

    /// <summary>
    /// Process a conditional block starting at <paramref name="start"/> (the #if/#ifdef/#ifndef line).
    /// Returns the index of the line after the matching #endif.
    /// </summary>
    private int HandleConditionalBlock(string[] lines, int start, int end, StringBuilder sb, string? fileName)
    {
        // Collect all branches: (condition-result, body-start, body-end)
        var branches = new List<(bool condition, int bodyStart, int bodyEnd)>();

        var firstDir = ParseDirective(lines[start].TrimStart());
        bool firstCond = EvaluateConditionDirective(firstDir.Name, firstDir.Rest);

        int depth = 0;
        int branchStart = start + 1;
        bool currentCond = firstCond;

        for (int i = start; i < end; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal)) continue;
            var dir = ParseDirective(trimmed);

            if (dir.Name is "if" or "ifdef" or "ifndef")
            {
                if (i != start) depth++;
            }
            else if (dir.Name == "endif")
            {
                if (depth > 0)
                {
                    depth--;
                    continue;
                }

                // Close last branch
                branches.Add((currentCond, branchStart, i));
                // Pick the first true branch and emit it
                EmitFirstTrueBranch(lines, branches, sb, fileName);
                return i + 1;
            }
            else if (depth == 0 && dir.Name == "elif")
            {
                branches.Add((currentCond, branchStart, i));
                currentCond = EvaluateConditionDirective("if", dir.Rest);
                branchStart = i + 1;
            }
            else if (depth == 0 && dir.Name == "else")
            {
                branches.Add((currentCond, branchStart, i));
                currentCond = true;
                branchStart = i + 1;
            }
        }

        // Unterminated #if — just skip everything
        return end;
    }

    private void EmitFirstTrueBranch(
        string[] lines,
        List<(bool condition, int bodyStart, int bodyEnd)> branches,
        StringBuilder sb,
        string? fileName
    )
    {
        foreach (var (cond, bodyStart, bodyEnd) in branches)
        {
            if (cond)
            {
                EmitLine(sb, bodyStart, fileName);
                ProcessLines(lines, bodyStart, bodyEnd, sb, fileName);
                return;
            }
        }
    }

    private bool EvaluateConditionDirective(string directive, string rest)
    {
        rest = rest.Trim();
        return directive switch
        {
            "ifdef" => _defines.ContainsKey(rest),
            "ifndef" => !_defines.ContainsKey(rest),
            _ => EvaluateExpression(rest), // #if / #elif
        };
    }

    // ── Expression evaluator for #if ────────────────────────────────────────
    // Supports: defined(X), integer literals, !, &&, ||, parentheses, identifiers (0 if undefined).

    private bool EvaluateExpression(string expr)
    {
        int pos = 0;
        long result = ParseOr(expr, ref pos);
        return result != 0;
    }

    private long ParseOr(string s, ref int pos)
    {
        long left = ParseAnd(s, ref pos);
        SkipSpaces(s, ref pos);
        while (pos + 1 < s.Length && s[pos] == '|' && s[pos + 1] == '|')
        {
            pos += 2;
            long right = ParseAnd(s, ref pos);
            left = (left != 0 || right != 0) ? 1 : 0;
            SkipSpaces(s, ref pos);
        }

        return left;
    }

    private long ParseAnd(string s, ref int pos)
    {
        long left = ParseUnary(s, ref pos);
        SkipSpaces(s, ref pos);
        while (pos + 1 < s.Length && s[pos] == '&' && s[pos + 1] == '&')
        {
            pos += 2;
            long right = ParseUnary(s, ref pos);
            left = (left != 0 && right != 0) ? 1 : 0;
            SkipSpaces(s, ref pos);
        }

        return left;
    }

    private long ParseUnary(string s, ref int pos)
    {
        SkipSpaces(s, ref pos);
        if (pos < s.Length && s[pos] == '!')
        {
            pos++;
            long val = ParseUnary(s, ref pos);
            return val == 0 ? 1 : 0;
        }

        return ParsePrimary(s, ref pos);
    }

    private long ParsePrimary(string s, ref int pos)
    {
        SkipSpaces(s, ref pos);
        if (pos >= s.Length) return 0;

        // Parenthesized expression
        if (s[pos] == '(')
        {
            pos++;
            long val = ParseOr(s, ref pos);
            SkipSpaces(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;
            return val;
        }

        // Integer literal
        if (char.IsDigit(s[pos]))
        {
            int start = pos;
            while (pos < s.Length
                   && (char.IsDigit(s[pos])
                       || s[pos] == 'x'
                       || s[pos] == 'X'
                       || (s[pos] >= 'a' && s[pos] <= 'f')
                       || (s[pos] >= 'A' && s[pos] <= 'F')))
                pos++;
            var numStr = s.Substring(start, pos - start);
            if (numStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToInt64(numStr[2..], 16);
            return long.TryParse(numStr, out long n) ? n : 0;
        }

        // Identifier or defined(...)
        if (IsIdentStart(s[pos]))
        {
            int start = pos;
            while (pos < s.Length && IsIdentChar(s[pos])) pos++;
            var ident = s.Substring(start, pos - start);

            if (ident == "defined")
            {
                SkipSpaces(s, ref pos);
                bool hasParen = pos < s.Length && s[pos] == '(';
                if (hasParen) pos++;
                SkipSpaces(s, ref pos);
                int idStart = pos;
                while (pos < s.Length && IsIdentChar(s[pos])) pos++;
                var macro = s.Substring(idStart, pos - idStart);
                SkipSpaces(s, ref pos);
                if (hasParen && pos < s.Length && s[pos] == ')') pos++;
                return _defines.ContainsKey(macro) ? 1 : 0;
            }

            // Look up macro value — undefined = 0
            if (_defines.TryGetValue(ident, out var val) && long.TryParse(val, out long v))
                return v;
        }

        return 0;
    }

    // ── Macro expansion ─────────────────────────────────────────────────────

    private string ExpandMacros(string line)
    {
        if (_defines.Count == 0 && _functionMacros.Count == 0) return line;

        var sb = new StringBuilder(line.Length);
        int i = 0;
        while (i < line.Length)
        {
            if (IsIdentStart(line[i]))
            {
                int start = i;
                while (i < line.Length && IsIdentChar(line[i])) i++;
                var token = line.Substring(start, i - start);

                // Function-like macro: NAME(...)
                if (_functionMacros.TryGetValue(token, out var fm))
                {
                    // Skip whitespace between name and '('
                    int peek = i;
                    while (peek < line.Length && (line[peek] == ' ' || line[peek] == '\t')) peek++;

                    if (peek < line.Length && line[peek] == '(')
                    {
                        var args = ParseMacroArguments(line, peek, out int afterClose);
                        if (args != null)
                        {
                            i = afterClose;
                            var expanded = SubstituteFunctionMacro(fm, args);
                            // Recursively expand the result (handles nested macros)
                            sb.Append(ExpandMacros(expanded));
                            continue;
                        }
                    }
                }

                // Object-like macro
                if (_defines.TryGetValue(token, out var replacement))
                    sb.Append(replacement);
                else
                    sb.Append(token);
            }
            else
            {
                sb.Append(line[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parse comma-separated arguments from a function-macro invocation, respecting nested parens.
    /// <paramref name="openParen"/> points at the '('. Returns null if malformed.
    /// <paramref name="afterClose"/> is set to the position after the closing ')'.
    /// </summary>
    private static List<string>? ParseMacroArguments(string line, int openParen, out int afterClose)
    {
        afterClose = openParen;
        if (openParen >= line.Length || line[openParen] != '(') return null;

        var args = new List<string>();
        int depth = 1;
        int pos = openParen + 1;
        int argStart = pos;

        while (pos < line.Length && depth > 0)
        {
            char c = line[pos];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    args.Add(line.Substring(argStart, pos - argStart).Trim());
                    afterClose = pos + 1;
                    // If the only "argument" is empty, treat as zero args
                    if (args.Count == 1 && args[0].Length == 0)
                        args.Clear();
                    return args;
                }
            }
            else if (c == ',' && depth == 1)
            {
                args.Add(line.Substring(argStart, pos - argStart).Trim());
                argStart = pos + 1;
            }

            pos++;
        }

        return null; // unmatched paren
    }

    private static string SubstituteFunctionMacro(FunctionMacro macro, List<string> args)
    {
        var body = macro.Body;
        if (macro.Parameters.Length == 0) return body;

        // Token-level replacement of parameter names with arguments
        var sb = new StringBuilder(body.Length);
        int i = 0;
        while (i < body.Length)
        {
            if (IsIdentStart(body[i]))
            {
                int start = i;
                while (i < body.Length && IsIdentChar(body[i])) i++;
                var token = body.Substring(start, i - start);
                int paramIdx = Array.IndexOf(macro.Parameters, token);
                if (paramIdx >= 0 && paramIdx < args.Count)
                    sb.Append(args[paramIdx]);
                else
                    sb.Append(token);
            }
            else
            {
                // Handle ## token pasting: trim whitespace and ## between tokens
                if (i + 1 < body.Length && body[i] == '#' && body[i + 1] == '#')
                {
                    // Remove trailing whitespace already in sb
                    while (sb.Length > 0 && (sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t'))
                        sb.Remove(sb.Length - 1, 1);
                    i += 2;
                    // Skip leading whitespace after ##
                    while (i < body.Length && (body[i] == ' ' || body[i] == '\t')) i++;
                }
                else
                {
                    sb.Append(body[i]);
                    i++;
                }
            }
        }

        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private readonly ref struct Directive(string name, string rest)
    {
        public readonly string Name = name;
        public readonly string Rest = rest;
    }

    private static Directive ParseDirective(string trimmedLine)
    {
        // trimmedLine starts with '#'; skip '#' and optional whitespace
        int i = 1;
        while (i < trimmedLine.Length && trimmedLine[i] == ' ') i++;
        int nameStart = i;
        while (i < trimmedLine.Length && char.IsLetter(trimmedLine[i])) i++;
        var name = trimmedLine.Substring(nameStart, i - nameStart);
        // Skip whitespace between directive name and rest
        while (i < trimmedLine.Length && trimmedLine[i] == ' ') i++;
        // Strip trailing single-line comment
        var rest = i < trimmedLine.Length ? trimmedLine[i..] : "";
        int commentIdx = rest.IndexOf("//", StringComparison.Ordinal);
        if (commentIdx >= 0) rest = rest[..commentIdx];
        return new Directive(name, rest);
    }

    private static string[] SplitLines(string source)
    {
        return source.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
    }

    private static int IndexOfWhitespace(string s, int start = 0)
    {
        for (int i = start; i < s.Length; i++)
            if (s[i] == ' ' || s[i] == '\t')
                return i;
        return -1;
    }

    private static void SkipSpaces(string s, ref int pos)
    {
        while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t')) pos++;
    }

    private static bool IsIdentStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
    private static bool IsIdentChar(char c) => IsIdentStart(c) || (c >= '0' && c <= '9');
}
