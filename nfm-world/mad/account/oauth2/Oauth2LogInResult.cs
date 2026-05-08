using System.Net;

namespace nfm_world.account.oauth2;

public class Oauth2LogInResult(string message, HttpStatusCode code) : RequestResult(message, code)
{
    public string? TempToken;

    public override string? ErrorString()
    {
        var current = base.ErrorString();
        if(current is not null) return current;

        switch (StatusCode)
        {
            case HttpStatusCode.NotFound:
                {
                    return "This account is not registered.";
                }
        }

        return "Unknown error: " + StatusCode;
    }

    public bool NoSuchAccount()
    {
        return StatusCode == HttpStatusCode.NotFound;
    }
}