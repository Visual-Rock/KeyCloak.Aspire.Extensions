using Aspire.Hosting.ApplicationModel;

namespace KeyCloak.Aspire.Extensions;

internal sealed class KeycloakClientAnnotation(KeycloakClientResource clientResource) : IResourceAnnotation
{
    public KeycloakClientResource ClientResource { get; } = clientResource;
}