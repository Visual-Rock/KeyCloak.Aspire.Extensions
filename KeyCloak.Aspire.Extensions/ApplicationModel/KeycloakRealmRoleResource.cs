namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     Represents a realm-level role in a Keycloak realm.
/// </summary>
public sealed class KeycloakRealmRoleResource(string name, string roleName, string? description, KeycloakRealmResource parent)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>
{
    /// <summary>
    ///     The role name used in Keycloak.
    /// </summary>
    public string RoleName { get; } = roleName;

    /// <summary>
    ///     Optional description for the role.
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    ///     The parent <see cref="KeycloakRealmResource" /> that owns this role.
    /// </summary>
    public KeycloakRealmResource Parent { get; } = parent;
}