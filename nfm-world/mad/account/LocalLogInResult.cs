using System.Net;

namespace nfm_world.account;

public class LocalLogInResult(string message, HttpStatusCode code) : RequestResult(message, code)
{
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

    public bool NoSuchAccount { get { return StatusCode == HttpStatusCode.NotFound; }}
}