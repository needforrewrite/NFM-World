using System.Net;

namespace NFMWorld.Account;

public class CreateLocalAccountResult(string message, HttpStatusCode code) : RequestResult(message, code)
{
    public override string? ErrorString()
    {
        var current = base.ErrorString();
        if(current is not null) return current;

        switch (StatusCode)
        {
            case HttpStatusCode.Conflict:
                {
                    return "An account already exists with this username, or this account is already registered.";
                }
        }

        return "Unknown error: " + StatusCode;
    }
}