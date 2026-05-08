using System.Text.Json.Serialization;

namespace NFMWorld.Api;

public class ApiResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }
}