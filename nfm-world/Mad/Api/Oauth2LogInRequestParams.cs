using System.Text.Json.Serialization;

namespace NFMWorld.Api;

public struct Oauth2LogInRequestParams
{
    [JsonPropertyName("code")]
    public string Code { get; set; }
    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; set; }
}