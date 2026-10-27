using System.IO.Compression;
using NFMWorld.Interop.Browser;

namespace NFMWorld.Browser;

/// <summary>
/// Reads single entries out of the music archive over HTTP, without downloading it.
///
/// The archive is 148 MB - the whole music tree - and one track is played at a time, so only the
/// entries asked for are ever fetched. Any static server honouring <c>Range:</c> can serve one, and
/// that is what makes this possible: the zip's central directory is a small read at the tail, and
/// each entry is then one more read of exactly its bytes.
///
/// The reads are synchronous through <see cref="BrowserHttp"/> rather than <see cref="HttpClient"/>:
/// ZipArchive's parser is synchronous, and the async path cannot be waited on from wasm. That is
/// safe here because the archive is only touched while a track loads.
/// </summary>
internal sealed class MusicFetch : IDisposable
{
    private readonly RangeReadStream _reader;
    private readonly ZipArchive _archive;
    private Dictionary<string, ZipArchiveEntry>? _entries;

    private MusicFetch(RangeReadStream reader, ZipArchive archive)
    {
        _reader = reader;
        _archive = archive;
    }

    public static MusicFetch? Open(string url)
    {
        var reader = new RangeReadStream(url);
        try
        {
            return new MusicFetch(reader, new ZipArchive(reader, ZipArchiveMode.Read, leaveOpen: true));
        }
        catch (Exception e)
        {
            Console.WriteLine($"[nfmw] music archive unavailable: {e.Message}");
            reader.Dispose();
            return null;
        }
    }

    /// <summary>Reads one entry, or null when the archive has no such file.</summary>
    public byte[]? ReadEntry(string path)
    {
        _entries ??= _archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .GroupBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var name = path.Replace('\\', '/').TrimStart('/');
        if (!_entries.TryGetValue(name, out var entry))
            return null;

        var buffer = new byte[entry.Length];
        using var stream = entry.Open();
        stream.ReadExactly(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _reader.Dispose();
    }

    /// <summary>
    /// A read-only seekable stream whose backing bytes live on a server. Every read fetches exactly
    /// the requested range, so the cost is the bytes asked for rather than the bytes nearby.
    /// </summary>
    private sealed class RangeReadStream(string url) : Stream
    {
        private long _position;
        private long _length = -1;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (_length < 0)
                {
                    _length = BrowserHttp.ContentLength(url);
                    if (_length < 0)
                        throw new IOException($"'{url}' did not report a length; a ranged read cannot be served.");
                }
                return _length;
            }
        }

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return 0;

            var remaining = Length - _position;
            if (remaining <= 0) return 0;

            // The interop returns the bytes; a caller-supplied buffer would be copied in only.
            var count = (int)Math.Min(remaining, buffer.Length);
            var bytes = BrowserHttp.ReadRange(url, _position, count);
            if (bytes.Length == 0) return 0;

            bytes.AsSpan(0, Math.Min(bytes.Length, buffer.Length)).CopyTo(buffer);
            _position += bytes.Length;
            return bytes.Length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
