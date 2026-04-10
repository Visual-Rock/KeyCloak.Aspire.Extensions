namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     Represents a Keycloak realm managed by the Aspire app host.
/// </summary>
public sealed class KeycloakRealmResource(string name, string realmName, KeycloakResource parent) : Resource(name), IResourceWithParent<KeycloakResource>
{
    /// <summary>
    ///     The Keycloak realm name.
    /// </summary>
    public string RealmName { get; } = realmName;

    /// <summary>
    ///     The parent <see cref="KeycloakResource" /> that hosts this realm.
    /// </summary>
    public KeycloakResource Parent { get; } = parent;
}