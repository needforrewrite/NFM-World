using System.Text.Json;
using System.Text.Json.Serialization;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Accounts;

/// <summary>Body of <c>POST /auth/login</c>.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>
/// The fields a rename, description change or re-listing touches. A null is left out of the
/// body, so the archive keeps what it had; <c>false</c> is a real value and must be sent.
/// </summary>
public sealed record AssetUpdateRequest(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsPublic
);

/// <summary>An account as <c>/auth/me</c> and the login reply describe it.</summary>
public sealed record AccountResponse(
    string Username,
    bool IsAdmin,
    UserRole Role,
    DateTimeOffset? DeletionPendingUntil,
    double Elo,
    int Wins,
    int Losses,
    int Matches
);

/// <summary>Reply to <c>POST /auth/login</c>.</summary>
public sealed record LoginResponse(string Token, AccountResponse Account);

/// <summary>The session token as it is persisted locally between runs.</summary>
public sealed record StoredAccount(string Token, string Username, UserRole Role, DateTimeOffset? DeletionPendingUntil);

/// <summary>
/// Metadata for the archive's account JSON.
///
/// Reflection cannot supply this in a trimmed build: the metadata builder reads constructor
/// parameter *names*, and trimming strips them, so a reflection-made <c>JsonTypeInfo</c> throws
/// <c>ConstructorContainsNullParameterNames</c> before the first request is even sent. Everything
/// the account surface sends or reads is declared here, and the call sites pass the generated
/// <c>JsonTypeInfo</c> explicitly so the reflection resolver is never consulted.
///
/// The naming policy matches <see cref="System.Text.Json.JsonSerializerDefaults.Web"/>, which is
/// what the archive's endpoints use and what the reflection path behaved as before.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false
)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(AssetUpdateRequest))]
[JsonSerializable(typeof(AccountResponse))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(StoredAccount))]
[JsonSerializable(typeof(JsonElement))]
public partial class AccountJsonContext : JsonSerializerContext;
