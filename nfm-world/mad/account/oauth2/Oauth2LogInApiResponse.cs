using System.Text.Json.Serialization;

namespace nfm_world.mad.account.oauth2;

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