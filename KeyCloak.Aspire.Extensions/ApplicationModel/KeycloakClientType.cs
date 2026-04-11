namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     The protocol a Keycloak client uses.
/// </summary>
public enum KeycloakClientType
{
    /// <summary>OpenID Connect (default).</summary>
    OpenIdConnect,

    /// <summary>SAML 2.0.</summary>
    Saml
}
