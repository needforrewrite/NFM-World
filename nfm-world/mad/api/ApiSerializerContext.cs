using System.Text.Json.Serialization;
using nfm_world.account.oauth2;

namespace nfm_world.api;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(LogInApiResponse))]
[JsonSerializable(typeof(Oauth2LogInApiResponse))]
[JsonSerializable(typeof(LocalAccountRequestParams))]
[JsonSerializable(typeof(Oauth2CreateAccountRequestParams))]
[JsonSerializable(typeof(Oauth2LogInRequestParams))]
public partial class ApiSerializerContext : JsonSerializerContext;