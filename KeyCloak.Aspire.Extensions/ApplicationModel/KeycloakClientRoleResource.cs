namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     Represents a client-scoped role in a Keycloak client.
/// </summary>
public sealed class KeycloakClientRoleResource(string name, string roleName, string? description, KeycloakClientResource parent)
    : Resource(name), IResourceWithParent<KeycloakClientResource>
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
    ///     The parent <see cref="KeycloakClientResource" /> that owns this role.
    /// </summary>
    public KeycloakClientResource Parent { get; } = parent;
}