using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFMWorldLibrary.FixedMath;

public class fix64Converter : JsonConverter<fix64>
{
    public override fix64 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // read number as string
        if (reader.TokenType == JsonTokenType.Number)
        {
            ReadOnlySpan<byte> span;
            if (reader.HasValueSequence)
            {
                span = reader.ValueSequence.ToArray();
            }
            else
            {
                span = reader.ValueSpan;
            }
            
            var str = Encoding.UTF8.GetString(span);
            if (fix64.TryParse(str, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            
            throw new JsonException($"Failed to parse fix64 value: {str}");
        }
        throw new JsonException("Invalid JSON value for fix64.");
    }

    public override void Write(Utf8JsonWriter writer, fix64 value, JsonSerializerOptions options)
    {
        // idk how to do this better
        writer.WriteNumberValue((decimal)value);
    }
}