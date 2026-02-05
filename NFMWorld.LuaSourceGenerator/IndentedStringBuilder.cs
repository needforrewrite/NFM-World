using System.Runtime.CompilerServices;
using System.Text;

namespace NFMWorld.LuaSourceGenerator;

public class IndentedStringBuilder(int indentLevel = 0)
{
    private readonly StringBuilder _sb = new();
    public int IndentLevel { get; set; } = indentLevel;
    
    public void AppendDirective(string directive)
    {
        _sb.AppendLine($"#{directive}");
    }

    public void AppendLine(string line = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0, [CallerMemberName] string callerMemberName = "")
    {
        if (string.IsNullOrEmpty(line))
        {
#if DEBUG
            PadCentered($"{Path.GetFileName(callerFilePath)}:{callerLineNumber} ({callerMemberName})", 60);      
#endif
            _sb.AppendLine();
        }
        else
        {
            foreach (var range in line.AsSpan().Split('\n'))
            {
#if DEBUG
                if (!line.AsSpan(range).TrimStart().StartsWith("#"))
                {
                    PadCentered($"{Path.GetFileName(callerFilePath)}:{callerLineNumber} ({callerMemberName})", 60);
                }
#endif
                _sb.Append(' ', IndentLevel * 4);
                _sb.AppendLine($"{line.AsSpan(range).TrimEnd()}");
            }
        }
    }
    
    private void PadCentered(string str, int amount)
    {
        int totalPadding = amount - str.Length;
        if (totalPadding <= 0)
        {
            _sb.Append("/* ");
            _sb.Append(str);
            _sb.Append(" */ ");
            return;
        }

        int paddingLeft = totalPadding / 2;
        int paddingRight = totalPadding - paddingLeft;

        _sb.Append($"/* ");
        _sb.Append(' ', paddingLeft);
        _sb.Append(str);
        _sb.Append(' ', paddingRight);
        _sb.Append(" */ ");
    }

    public IndentDisposable Indent()
    {
        IndentLevel++;
        return new IndentDisposable(this);
    }

    public readonly struct IndentDisposable(IndentedStringBuilder gen) : IDisposable
    {
        public void Dispose() => gen.IndentLevel--;
    }

    public BlockDisposable Block(string start = "{", string end = "}")
    {
        AppendLine(start);
        IndentLevel++;
        return new BlockDisposable(this, end);
    }

    public readonly struct BlockDisposable(IndentedStringBuilder gen, string end) : IDisposable
    {
        public void Dispose()
        {
            gen.IndentLevel--;
            gen.AppendLine(end);
        }
    }

    public override string ToString() => _sb.ToString();
}