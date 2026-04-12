namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     Configures the OIDC protocol mapper that maps client's roles into JWT token claims.
/// </summary>
public sealed class KeycloakRoleMapperBuilder
{
    internal KeycloakRoleMapperOptions Options { get; } = new();

    /// <summary>
    ///     Sets the JWT claim name used for the mapped roles. Defaults to <c>"roles"</c>.
    /// </summary>
    public KeycloakRoleMapperBuilder WithClaimName(string claimName)
    {
        Options.ClaimName = claimName;
        return this;
    }

    /// <summary>
    ///     Includes or excludes the roles claim from the access token.
    /// </summary>
    public KeycloakRoleMapperBuilder AddToAccessToken(bool enabled = true)
    {
        Options.AddToAccessToken = enabled;
        return this;
    }

    /// <summary>
    ///     Includes or excludes the roles claim from the ID token.
    /// </summary>
    public KeycloakRoleMapperBuilder AddToIdToken(bool enabled = true)
    {
        Options.AddToIdToken = enabled;
        return this;
    }

    /// <summary>
    ///     Includes or excludes the roles claim from the userinfo endpoint response.
    /// </summary>
    public KeycloakRoleMapperBuilder AddToUserinfo(bool enabled = true)
    {
        Options.AddToUserinfo = enabled;
        return this;
    }

    /// <summary>
    ///     Emits the roles claim as a JSON array.
    /// </summary>
    public KeycloakRoleMapperBuilder WithMultivalued(bool multivalued = true)
    {
        Options.Multivalued = multivalued;
        return this;
    }
}

internal sealed class KeycloakRoleMapperOptions
{
    public string ClaimName { get; set; } = "roles";
    public bool AddToAccessToken { get; set; } = true;
    public bool AddToIdToken { get; set; }
    public bool AddToUserinfo { get; set; }
    public bool Multivalued { get; set; } = true;
}