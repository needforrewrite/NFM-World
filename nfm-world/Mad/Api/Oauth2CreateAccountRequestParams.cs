using System.Text.Json.Serialization;

namespace NFMWorld.Api;

public struct Oauth2CreateAccountRequestParams
{
    [JsonPropertyName("temp_token")]
    public string TempToken { get; set; }
    [JsonPropertyName("username")]
    public string Username { get; set; }
}