using System.Net;
using NFMWorld.Account.OAuth2;

namespace NFMWorld.Api;

using ApiRes = (System.Net.HttpStatusCode, ApiResponse?);

public static class UserApi
{
    public static async Task<ApiRes> CreateLocalAccount(string username, string password)
    {
        var route = "create_account";
        var body = new LocalAccountRequestParams
        {
            Username = username,
            Password = password
        };

        return await NfmwApi.PostAsync<LocalAccountRequestParams, ApiResponse>(route, body, ApiSerializerContext.Default.LocalAccountRequestParams, ApiSerializerContext.Default.ApiResponse);
    }

    public static async Task<(HttpStatusCode, LogInApiResponse?)> LocalLogIn(string username, string password)
    {
        var route = "login";
        var body = new LocalAccountRequestParams
        {
            Username = username,
            Password = password
        };

        return await NfmwApi.PostAsync<LocalAccountRequestParams, LogInApiResponse>(route, body, ApiSerializerContext.Default.LocalAccountRequestParams, ApiSerializerContext.Default.LogInApiResponse);
    }

    public static async Task<(HttpStatusCode, LogInApiResponse?)> CreateDiscordOauth2Account(string tempToken, string username)
    {
        var route = "discord/create_account";
        var body = new Oauth2CreateAccountRequestParams
        {
            TempToken = tempToken,
            Username = username
        };

        return await NfmwApi.PostAsync<Oauth2CreateAccountRequestParams, LogInApiResponse>(route, body, ApiSerializerContext.Default.Oauth2CreateAccountRequestParams, ApiSerializerContext.Default.LogInApiResponse);
    }

    public static async Task<(HttpStatusCode, Oauth2LogInApiResponse?)> DiscordOauth2LogIn(string oauth2Code, string redirectUri)
    {
        var route = "discord/login";
        var body = new Oauth2LogInRequestParams
        {
            RedirectUri = redirectUri,
            Code = oauth2Code
        };

        return await NfmwApi.PostAsync<Oauth2LogInRequestParams, Oauth2LogInApiResponse>(route, body, ApiSerializerContext.Default.Oauth2LogInRequestParams, ApiSerializerContext.Default.Oauth2LogInApiResponse);
    }
}