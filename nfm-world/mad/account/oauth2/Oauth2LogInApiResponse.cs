using System.Text.Json.Serialization;
using nfm_world.api;

namespace nfm_world.account.oauth2;

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