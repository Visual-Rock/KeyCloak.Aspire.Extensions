namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     Represents a Keycloak client (application registration) that is provisioned inside a realm.
/// </summary>
public sealed class KeycloakClientResource(string name, string clientId, string clientName, string? description, KeycloakRealmResource parent)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>
{
    /// <summary>
    ///     The static client ID used to identify the client in Keycloak.
    /// </summary>
    public string ClientId { get; } = clientId;

    /// <summary>
    ///     The display name of the client.
    /// </summary>
    public string ClientName { get; } = clientName;

    /// <summary>
    ///     Optional description for the client.
    /// </summary>
    public string? Description { get; } = description;

    internal string? Secret { get; set; }

    internal string Protocol { get; set; } = "openid-connect";
    internal bool PublicClient { get; set; } = true;
    internal bool BearerOnly { get; set; } = false;
    internal bool StandardFlowEnabled { get; set; } = true;
    internal bool ImplicitFlowEnabled { get; set; } = false;
    internal bool DirectAccessGrantsEnabled { get; set; } = false;
    internal bool ServiceAccountsEnabled { get; set; } = false;
    internal string? AdminUrl { get; set; }
    internal string? RootUrl { get; set; }
    internal string? HomeUrl { get; set; }
    internal List<string> RedirectUris { get; } = [];
    internal List<string> PostLogoutRedirectUris { get; } = [];
    internal List<string> WebOrigins { get; } = [];
    internal List<KeycloakClientRoleResource> Roles { get; } = [];
    internal KeycloakRoleMapperOptions? RoleMapper { get; set; }

    /// <inheritdoc />
    public KeycloakRealmResource Parent { get; } = parent;
}