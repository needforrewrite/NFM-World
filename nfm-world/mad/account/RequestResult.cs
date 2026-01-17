using System.Net;

public class RequestResult
{
    public string? Message;
    public HttpStatusCode StatusCode;

    public bool Success { get { return StatusCode == HttpStatusCode.OK; }}

    public virtual string? ErrorString()
    {
        if (Success) return null;

        switch (StatusCode)
        {
            case HttpStatusCode.InternalServerError:
                {
                    return "Internal Error: " + Message;
                };
            case HttpStatusCode.BadRequest:
                {
                    return "Issue with input: " + Message;
                };
        }

        return null;
    }

    public RequestResult(string? message, HttpStatusCode code)
    {
        Message = message;
        StatusCode = code;
    }
}