namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     Represents a Keycloak user managed by the Aspire app host.
/// </summary>
public sealed class KeycloakUserResource(string name, string? id, string username, string email, string firstName, string lastName, string password, KeycloakRealmResource parent)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>
{
    /// <summary>
    ///     The optional fixed ID to assign to this user.
    /// </summary>
    internal string? Id { get; } = id;

    /// <summary>
    ///     The user's login name.
    /// </summary>
    public string Username { get; } = username;

    internal string Email { get; } = email;
    internal string FirstName { get; } = firstName;
    internal string LastName { get; } = lastName;
    internal string Password { get; } = password;

    /// <summary>
    ///     Realm-level roles to assign to this user on provisioning.
    /// </summary>
    internal List<KeycloakRealmRoleResource> RealmRoles { get; } = [];

    /// <summary>
    ///     Client-scoped roles to assign to this user on provisioning.
    /// </summary>
    internal List<KeycloakClientRoleResource> ClientRoles { get; } = [];

    /// <summary>
    ///     The parent <see cref="KeycloakRealmResource" /> that owns this user.
    /// </summary>
    public KeycloakRealmResource Parent { get; } = parent;
}