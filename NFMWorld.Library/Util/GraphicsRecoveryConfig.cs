namespace NFMWorldLibrary.Util;

public static class GraphicsRecoveryConfig
{
    public static string SelectRenderer(string content, string renderer, bool reset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renderer);
        if (renderer.Contains('\r') || renderer.Contains('\n'))
            throw new ArgumentException("Invalid renderer name.", nameof(renderer));
        var lines = new List<string>();
        if (!reset)
        {
            using var reader = new StringReader(content);
            while (reader.ReadLine() is { } line)
            {
                var key = line.TrimStart().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (key != "video_renderer3") lines.Add(line);
            }
        }
        lines.Add($"video_renderer3 {renderer}");
        return string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine;
    }
}
