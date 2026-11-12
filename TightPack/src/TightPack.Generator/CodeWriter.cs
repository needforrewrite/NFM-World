using System;
using System.Text;

namespace TightPack.Generator;

/// <summary>Minimal indenting writer; the generated code is small enough that a StringBuilder is plenty.</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new(4096);
    private int _indent;

    public CodeWriter Line(string text = "")
    {
        if (text.Length != 0)
        {
            for (int i = 0; i < _indent; i++)
            {
                _builder.Append("    ");
            }

            _builder.Append(text);
        }

        _builder.Append('\n');
        return this;
    }

    /// <summary>Writes <paramref name="text"/> followed by an opening brace, then indents.</summary>
    public CodeWriter Open(string text)
    {
        Line(text + " {");
        return Indent();
    }

    /// <summary>Writes a label (e.g. <c>case 0:</c>) without a brace, then indents.</summary>
    public CodeWriter Label(string text)
    {
        Line(text);
        return Indent();
    }


    public CodeWriter Close(string text = "}")
    {
        Dedent();
        return Line(text);
    }

    public CodeWriter Indent()
    {
        _indent++;
        return this;
    }

    public CodeWriter Dedent()
    {
        _indent--;
        return this;
    }

    public CodeWriter Blank()
    {
        _builder.Append('\n');
        return this;
    }

    public override string ToString() => _builder.ToString();
}
