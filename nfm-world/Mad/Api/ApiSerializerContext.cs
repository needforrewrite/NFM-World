using System.Text.Json.Serialization;
using NFMWorld.Account.OAuth2;

namespace NFMWorld.Api;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(LogInApiResponse))]
[JsonSerializable(typeof(Oauth2LogInApiResponse))]
[JsonSerializable(typeof(LocalAccountRequestParams))]
[JsonSerializable(typeof(Oauth2CreateAccountRequestParams))]
[JsonSerializable(typeof(Oauth2LogInRequestParams))]
public partial class ApiSerializerContext : JsonSerializerContext;