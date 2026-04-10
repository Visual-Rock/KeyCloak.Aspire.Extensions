using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KeyCloak.Aspire.Extensions;

internal sealed class KeycloakAdminApiClient(HttpClient client, string baseUrl, string adminUser, string adminPassword)
{
    private async Task Authenticate(CancellationToken ct)
    {
        if (client.DefaultRequestHeaders.Authorization is not null)
            return;

        var response = await client.PostAsync($"{baseUrl}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password", ["client_id"] = "admin-cli", ["username"] = adminUser, ["password"] = adminPassword
            }), ct);

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync(KeycloakJsonContext.Default.TokenResponse, ct) ??
                    throw new InvalidOperationException("Empty token response from Keycloak.");
        var accessToken = token.AccessToken ?? throw new InvalidOperationException("No access_token in Keycloak token response.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<bool> RealmExistsAsync(string realmName, CancellationToken ct)
    {
        await Authenticate(ct);

        var response = await client.GetAsync($"{baseUrl}/admin/realms/{realmName}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task CreateRealmAsync(string realmName, CancellationToken ct)
    {
        await Authenticate(ct);
        
        var content = JsonContent.Create(new RealmRepresentation(realmName, true), KeycloakJsonContext.Default.RealmRepresentation);
        var response = await client.PostAsync($"{baseUrl}/admin/realms", content, ct);
        response.EnsureSuccessStatusCode();
    }
}

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")]
    string? AccessToken);

internal sealed record RealmRepresentation(
    [property: JsonPropertyName("realm")] string Realm,
    [property: JsonPropertyName("enabled")]
    bool Enabled);

[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(RealmRepresentation))]
internal sealed partial class KeycloakJsonContext : JsonSerializerContext;