using System.Text.Json.Serialization;

namespace nfm_world.api;

public struct LocalAccountRequestParams
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
    [JsonPropertyName("password")]
    public string Password { get; set; } 
}