using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NFMWorldLibrary;

namespace NFMWorld.Api;

public class NfmwApi
{
    private static readonly string _baseAddr = "https://nfmwapi.jacher.io";

    private static HttpClient _client = new()
    {
        BaseAddress = new Uri(_baseAddr),
    };

    static NfmwApi() {
        _client.DefaultRequestHeaders.Remove("User-Agent");
        // TODO: get a programmatic version from somewhere?
        _client.DefaultRequestHeaders.Add("User-Agent", "NfmwClientAgent/1");

        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public static void SetAuthorization(string token)
    {
        _client.DefaultRequestHeaders.Remove("Authorization");
        _client.DefaultRequestHeaders.Add("Authorization", token);
    }

    public static async Task<(HttpStatusCode, T?)> GetAsync<T>(string route, JsonTypeInfo<T> responseTypeInfo)
    {
        using HttpResponseMessage response = await _client.GetAsync(route);

        return await ProcessResponse<T>(response, responseTypeInfo);
    }

    public static async Task<(HttpStatusCode, U?)> PostAsync<T, U>(string route, T body, JsonTypeInfo<T> requestTypeInfo, JsonTypeInfo<U> responseTypeInfo)
    {
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(body, requestTypeInfo),
            Encoding.UTF8,
            "application/json"
        );
        Logging.Debug(JsonSerializer.Serialize(body, requestTypeInfo));

        using HttpResponseMessage response = await _client.PostAsync(route, jsonContent);

        return await ProcessResponse<U>(response, responseTypeInfo);
    }

    public static async Task<(HttpStatusCode, U?)> PatchAsync<T, U>(string route, T body, JsonTypeInfo<T> requestTypeInfo, JsonTypeInfo<U> responseTypeInfo)
    {
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(body, requestTypeInfo),
            Encoding.UTF8,
            "application/json"
        );

        using HttpResponseMessage response = await _client.PatchAsync(route, jsonContent);

        return await ProcessResponse<U>(response, responseTypeInfo);
    }

    public static async Task<(HttpStatusCode, T?)> DeleteAsync<T>(string route, JsonTypeInfo<T> responseTypeInfo)
    {
        using HttpResponseMessage response = await _client.DeleteAsync(route);

        return await ProcessResponse<T>(response, responseTypeInfo);
    }

    private static async Task<(HttpStatusCode, T?)> ProcessResponse<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo)
    {
        var status = response.StatusCode;
        var content = await response.Content.ReadAsStreamAsync();

        var res = await JsonSerializer.DeserializeAsync<T>(content, typeInfo);

        return (status, res);
    }
}