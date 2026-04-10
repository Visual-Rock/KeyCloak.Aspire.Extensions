using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
///     Carries a <see cref="KeycloakUserResource" /> on its parent realm so the realm-ready callback can discover and
///     provision all registered users.
/// </summary>
internal sealed class KeycloakUserAnnotation(KeycloakUserResource userResource) : IResourceAnnotation
{
    public KeycloakUserResource UserResource { get; } = userResource;
}