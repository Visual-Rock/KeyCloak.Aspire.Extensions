using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting.ApplicationModel;

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

    public async Task<bool> UserExistsAsync(string realmName, string username, CancellationToken ct)
    {
        await Authenticate(ct);

        var url = $"{baseUrl}/admin/realms/{realmName}/users?username={Uri.EscapeDataString(username)}&exact=true";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync(KeycloakJsonContext.Default.ListUserRepresentation, ct);
        return users?.Count > 0;
    }

    public async Task CreateUserAsync(string realmName, KeycloakUserResource user, CancellationToken ct)
    {
        await Authenticate(ct);

        var representation = new UserRepresentation(user.Id, user.Username, user.Email, user.FirstName, user.LastName, true, true,
            [new CredentialRepresentation("password", user.Password, false)]);

        // The standard create-user endpoint ignores the id field
        if (user.Id is not null)
        {
            var import = new PartialImportRequest("SKIP", Users: [representation]);
            var importContent = JsonContent.Create(import, KeycloakJsonContext.Default.PartialImportRequest);
            var importResponse = await client.PostAsync($"{baseUrl}/admin/realms/{realmName}/partialImport", importContent, ct);
            importResponse.EnsureSuccessStatusCode();
        }
        else
        {
            var content = JsonContent.Create(representation, KeycloakJsonContext.Default.UserRepresentation);
            var response = await client.PostAsync($"{baseUrl}/admin/realms/{realmName}/users", content, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<bool> ClientExistsAsync(string realmName, string clientId, CancellationToken ct)
    {
        await Authenticate(ct);

        var url = $"{baseUrl}/admin/realms/{realmName}/clients?clientId={Uri.EscapeDataString(clientId)}&search=false";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var clients = await response.Content.ReadFromJsonAsync(KeycloakJsonContext.Default.ListClientRepresentation, ct);
        return clients?.Count > 0;
    }

    public async Task CreateClientAsync(string realmName, ClientRepresentation clientRepresentation, CancellationToken ct)
    {
        await Authenticate(ct);

        if (clientRepresentation.Secret is not null)
        {
            var import = new PartialImportRequest("SKIP", Clients: [clientRepresentation]);
            var importContent = JsonContent.Create(import, KeycloakJsonContext.Default.PartialImportRequest);
            var importResponse = await client.PostAsync($"{baseUrl}/admin/realms/{realmName}/partialImport", importContent, ct);
            importResponse.EnsureSuccessStatusCode();
        }
        else
        {
            var content = JsonContent.Create(clientRepresentation, KeycloakJsonContext.Default.ClientRepresentation);
            var response = await client.PostAsync($"{baseUrl}/admin/realms/{realmName}/clients", content, ct);
            response.EnsureSuccessStatusCode();
        }
    }
}

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")]
    string? AccessToken);

internal sealed record RealmRepresentation(
    [property: JsonPropertyName("realm")] string Realm,
    [property: JsonPropertyName("enabled")]
    bool Enabled);

internal sealed record UserRepresentation(
    [property: JsonPropertyName("id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id,
    [property: JsonPropertyName("username")]
    string Username,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("firstName")]
    string FirstName,
    [property: JsonPropertyName("lastName")]
    string LastName,
    [property: JsonPropertyName("enabled")]
    bool Enabled,
    [property: JsonPropertyName("emailVerified")]
    bool EmailVerified,
    [property: JsonPropertyName("credentials")]
    List<CredentialRepresentation> Credentials);

internal sealed record CredentialRepresentation(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("temporary")]
    bool Temporary);

internal sealed record PartialImportRequest(
    [property: JsonPropertyName("ifResourceExists")]
    string IfResourceExists,
    [property: JsonPropertyName("users")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<UserRepresentation>? Users = null,
    [property: JsonPropertyName("clients")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<ClientRepresentation>? Clients = null);

internal sealed record ClientRepresentation(
    [property: JsonPropertyName("clientId")]
    string ClientId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description,
    [property: JsonPropertyName("protocol")]
    string Protocol,
    [property: JsonPropertyName("enabled")]
    bool Enabled,
    [property: JsonPropertyName("publicClient")]
    bool PublicClient,
    [property: JsonPropertyName("bearerOnly")]
    bool BearerOnly,
    [property: JsonPropertyName("standardFlowEnabled")]
    bool StandardFlowEnabled,
    [property: JsonPropertyName("implicitFlowEnabled")]
    bool ImplicitFlowEnabled,
    [property: JsonPropertyName("directAccessGrantsEnabled")]
    bool DirectAccessGrantsEnabled,
    [property: JsonPropertyName("serviceAccountsEnabled")]
    bool ServiceAccountsEnabled,
    [property: JsonPropertyName("redirectUris")]
    string[]? RedirectUris,
    [property: JsonPropertyName("webOrigins")]
    string[]? WebOrigins,
    [property: JsonPropertyName("secret")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Secret);

[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(RealmRepresentation))]
[JsonSerializable(typeof(UserRepresentation))]
[JsonSerializable(typeof(List<UserRepresentation>))]
[JsonSerializable(typeof(PartialImportRequest))]
[JsonSerializable(typeof(ClientRepresentation))]
[JsonSerializable(typeof(List<ClientRepresentation>))]
internal sealed partial class KeycloakJsonContext : JsonSerializerContext;