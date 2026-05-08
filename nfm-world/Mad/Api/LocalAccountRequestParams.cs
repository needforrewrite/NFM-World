using System.Text.Json.Serialization;

namespace NFMWorld.Api;

public struct LocalAccountRequestParams
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
    [JsonPropertyName("password")]
    public string Password { get; set; } 
}