using System.Globalization;
using System.Text;

namespace NFMWorld.Sentry;

/// <summary>
/// Serializes Sentry events, transactions, and feedback into the Sentry Envelope format.
/// All JSON is constructed manually for AOT compatibility.
/// </summary>
public static class EnvelopeSerializer
{
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>
    /// Serialize a list of envelope items into a complete envelope byte array.
    /// </summary>
    public static byte[] Serialize(IReadOnlyList<SentryEnvelopeItem> items, DsnInfo dsn, DateTimeOffset sentAt, string? release)
    {
        var sb = new StringBuilder(4096);

        // --- Envelope header ---
        sb.Append('{');

        // event_id (use first item with an EventId)
        foreach (var item in items)
        {
            if (item.EventId is { } eid)
            {
                AppendJsonString(sb, "event_id", eid.ToString());
                sb.Append(',');
                break;
            }
        }

        // dsn
        AppendJsonString(sb, "dsn", $"https://{dsn.PublicKey}@{dsn.Host}/{dsn.ProjectId}");
        sb.Append(',');

        // sdk
        sb.Append("\"sdk\":{\"name\":\"NFMWorld.Sentry\",\"version\":\"1.0.0\"},");

        // sent_at
        AppendJsonString(sb, "sent_at", sentAt.ToString("o", CultureInfo.InvariantCulture));

        sb.Append("}\n");

        var headerBytes = Utf8.GetBytes(sb.ToString());
        sb.Clear();

        // --- Items ---
        var payloadChunks = new List<byte[]>(items.Count);

        foreach (var item in items)
        {
            var payload = item.SerializePayload();

            // Item header
            sb.Append("{\"type\":");
            AppendJsonString(sb, item.ItemType);
            sb.Append(",\"length\":");
            sb.Append(payload.Length);
            sb.Append(",\"content_type\":");
            AppendJsonString(sb, item.ContentType);
            sb.Append("}\n");

            var itemHeaderBytes = Utf8.GetBytes(sb.ToString());
            sb.Clear();

            payloadChunks.Add(itemHeaderBytes);
            payloadChunks.Add(payload);
            // Trailing newline after payload
            payloadChunks.Add([(byte)'\n']);
        }

        // Compute total size and concatenate
        var totalSize = headerBytes.Length;
        foreach (var chunk in payloadChunks)
            totalSize += chunk.Length;

        var result = new byte[totalSize];
        var offset = 0;
        Buffer.BlockCopy(headerBytes, 0, result, offset, headerBytes.Length);
        offset += headerBytes.Length;
        foreach (var chunk in payloadChunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    /// <summary>
    /// Serialize an event payload to JSON bytes.
    /// </summary>
    public static byte[] SerializeEvent(SentryEvent evt)
    {
        var sb = new StringBuilder(1024);
        sb.Append('{');

        AppendJsonString(sb, "event_id", evt.EventId.ToString());
        sb.Append(',');

        AppendJsonString(sb, "timestamp", evt.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        sb.Append(',');

        AppendJsonString(sb, "platform", "csharp");
        sb.Append(',');

        AppendJsonString(sb, "level", evt.Level.ToString().ToLowerInvariant());

        if (evt.Release is not null)
        {
            sb.Append(',');
            AppendJsonString(sb, "release", evt.Release);
        }

        if (evt.TransactionName is not null)
        {
            sb.Append(',');
            AppendJsonString(sb, "transaction", evt.TransactionName);
        }

        if (evt.Message is not null)
        {
            sb.Append(",\"logentry\":{\"message\":");
            AppendJsonString(sb, evt.Message);
            sb.Append('}');
        }

        // Exception
        if (evt.Exception is not null)
        {
            sb.Append(",\"exception\":{\"values\":[");
            SerializeExceptionChain(sb, evt.Exception);
            sb.Append("]}");
        }

        // Tags
        if (evt.Tags is { Count: > 0 })
        {
            sb.Append(",\"tags\":{");
            var first = true;
            foreach (var (key, value) in evt.Tags)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, key, value);
            }

            sb.Append('}');
        }

        // Breadcrumbs
        if (evt.Breadcrumbs is { Count: > 0 })
        {
            sb.Append(",\"breadcrumbs\":{\"values\":[");
            var first = true;
            foreach (var bc in evt.Breadcrumbs)
            {
                if (!first) sb.Append(',');
                first = false;
                SerializeBreadcrumb(sb, bc);
            }

            sb.Append("]}");
        }

        sb.Append('}');
        return Utf8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Serialize a transaction payload to JSON bytes.
    /// </summary>
    public static byte[] SerializeTransaction<T>(T tx) where T : ITransaction
    {
        var sb = new StringBuilder(1024);
        sb.Append('{');

        AppendJsonString(sb, "event_id", tx.EventId.ToString());
        sb.Append(',');

        AppendJsonString(sb, "type", "transaction");
        sb.Append(',');

        AppendJsonString(sb, "transaction", tx.Name);
        sb.Append(',');
        sb.Append("\"transaction_info\":{\"source\":\"custom\"},");
        sb.Append("\"contexts\":{\"trace\":{");
        sb.Append($"\"op\":\"{EscapeJsonString(tx.Operation)}\",");
        sb.Append($"\"span_id\":\"{Guid.NewGuid():N}\",");
        sb.Append($"\"trace_id\":\"{Guid.NewGuid():N}\",");
        sb.Append($"\"status\":\"{tx.Status.ToString().ToLowerInvariant()}\"");
        sb.Append("}},");

        AppendJsonString(sb, "start_timestamp", tx.StartTimestamp.ToString("o", CultureInfo.InvariantCulture));
        sb.Append(',');

        AppendJsonString(sb, "timestamp", (tx.EndTimestamp ?? DateTimeOffset.UtcNow).ToString("o", CultureInfo.InvariantCulture));
        sb.Append(',');

        AppendJsonString(sb, "platform", "csharp");

        if (tx.Release is not null)
        {
            sb.Append(',');
            AppendJsonString(sb, "release", tx.Release);
        }

        sb.Append('}');
        return Utf8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Serialize user feedback payload to JSON bytes.
    /// </summary>
    public static byte[] SerializeFeedback(SentryFeedback fb)
    {
        var sb = new StringBuilder(512);
        sb.Append('{');

        AppendJsonString(sb, "event_id", fb.EventId.ToString());

        if (fb.Name is not null)
        {
            sb.Append(',');
            AppendJsonString(sb, "name", fb.Name);
        }

        if (fb.Email is not null)
        {
            sb.Append(',');
            AppendJsonString(sb, "email", fb.Email);
        }

        if (fb.Comments is not null)
        {
            sb.Append(',');
            AppendJsonString(sb, "comments", fb.Comments);
        }

        sb.Append('}');
        return Utf8.GetBytes(sb.ToString());
    }

    private static void SerializeExceptionChain(StringBuilder sb, Exception ex)
    {
        var current = ex;
        var first = true;
        while (current is not null)
        {
            if (!first) sb.Append(',');
            first = false;

            sb.Append("{\"type\":");
            AppendJsonString(sb, current.GetType().FullName ?? current.GetType().Name);
            sb.Append(",\"value\":");
            AppendJsonString(sb, current.Message);

            // Stack trace
            if (current.StackTrace is not null)
            {
                sb.Append(",\"stacktrace\":{\"frames\":[");
                SerializeStackTrace(sb, current.StackTrace);
                sb.Append("]}");
            }

            sb.Append('}');

            current = current.InnerException;
        }
    }

    private static void SerializeBreadcrumb(StringBuilder sb, Breadcrumb bc)
    {
        sb.Append('{');

        AppendJsonString(sb, "timestamp", bc.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        sb.Append(',');

        AppendJsonString(sb, "level", bc.Level.ToString().ToLowerInvariant());
        sb.Append(',');

        AppendJsonString(sb, "category", bc.Category);
        sb.Append(',');

        AppendJsonString(sb, "type", string.IsNullOrEmpty(bc.Type) ? "default" : bc.Type);
        sb.Append(",\"message\":");
        AppendJsonString(sb, bc.Message);

        if (bc.Data is { Count: > 0 })
        {
            sb.Append(",\"data\":{");
            var first = true;
            foreach (var (key, value) in bc.Data)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, key, value);
            }

            sb.Append('}');
        }

        sb.Append('}');
    }

    private static void SerializeStackTrace(StringBuilder sb, string stackTrace)
    {
        var lines = stackTrace.Split('\n');
        var first = true;
        // Process in reverse order (Sentry expects innermost frame first in JSON,
        // but CLR stack traces list innermost first already, so we don't reverse)
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (!first) sb.Append(',');
            first = false;

            // Parse "at Namespace.Method(args) in File:line N"
            // or "at Namespace.Method(args)"
            var atIndex = trimmed.StartsWith("at ") ? 3 : 0;
            var methodPart = trimmed[atIndex..];
            var module = "";
            var function = methodPart;
            var filename = "";
            var lineno = 0;

            var inIndex = methodPart.LastIndexOf(" in ", StringComparison.Ordinal);
            if (inIndex > 0)
            {
                function = methodPart[..inIndex].Trim();
                var filePart = methodPart[(inIndex + 4)..].Trim();
                var lineIndex = filePart.LastIndexOf(":line ", StringComparison.Ordinal);
                if (lineIndex > 0)
                {
                    filename = filePart[..lineIndex].Trim();
                    _ = int.TryParse(filePart[(lineIndex + 6)..].Trim(), out lineno);
                }
                else
                {
                    filename = filePart;
                }
            }
            else
            {
                // Try to extract module from function
                var dotIndex = function.LastIndexOf('.');
                if (dotIndex > 0)
                {
                    module = function[..dotIndex];
                }
            }

            sb.Append("{\"filename\":");
            AppendJsonString(sb, filename);
            sb.Append(",\"function\":");
            AppendJsonString(sb, function);
            if (!string.IsNullOrEmpty(module))
            {
                sb.Append(",\"module\":");
                AppendJsonString(sb, module);
            }

            sb.Append(",\"lineno\":");
            sb.Append(lineno);
            sb.Append('}');
        }
    }

    private static void AppendJsonString(StringBuilder sb, string key, string value)
    {
        sb.Append('"');
        sb.Append(key);
        sb.Append("\":\"");
        sb.Append(EscapeJsonString(value));
        sb.Append('"');
    }

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        sb.Append(EscapeJsonString(value));
        sb.Append('"');
    }

    /// <summary>
    /// Escape a string for inclusion in a JSON string value.
    /// </summary>
    public static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append(@"\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
