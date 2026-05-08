using System.Text.Json.Serialization;

namespace nfm_world.api;

public class ApiResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }
}