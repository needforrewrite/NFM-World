using System.Text.Json.Serialization;
using NFMWorld.Api;

namespace NFMWorld.Account.OAuth2;

public class Oauth2LogInApiResponse : LogInApiResponse
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    // This is set if the account does not exist.
    // It allows the create_account endpoint to reference
    // this instead - which saves re-authentication with Discord
    [JsonPropertyName("temp_token")]
    public string? TempToken { get; set; }
}