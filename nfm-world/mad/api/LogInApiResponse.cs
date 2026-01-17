using System.Text.Json.Serialization;
using nfm_world.mad.api;

public class LogInApiResponse : ApiResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}