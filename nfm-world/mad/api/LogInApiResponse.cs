using System.Text.Json.Serialization;

namespace nfm_world.api;

public class LogInApiResponse : ApiResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}