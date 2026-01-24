using System.Text;

namespace NFMWorld.LuaSourceGenerator;

public class IndentedStringBuilder(int indentLevel = 0)
{
    private readonly StringBuilder _sb = new();
    public int IndentLevel { get; set; } = indentLevel;

    public void AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
        }
        else
        {
            var indent = new string(' ', IndentLevel * 4);
            foreach (var range in line.AsSpan().Split('\n'))
            {
                _sb.Append(indent);
                _sb.AppendLine($"{line.AsSpan(range).TrimEnd()}");
            }
        }
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