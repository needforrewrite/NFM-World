using System.Text.Json.Serialization;

namespace NFMWorld.Api;

public class LogInApiResponse : ApiResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}