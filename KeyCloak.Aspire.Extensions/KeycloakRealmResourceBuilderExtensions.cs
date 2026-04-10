using Aspire.Hosting.ApplicationModel;
using KeyCloak.Aspire.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Keycloak realm resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class KeycloakRealmResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Keycloak realm resource to the application model.
    /// On startup, once the Keycloak container is ready, the realm is checked for existence
    /// via the Admin REST API and created if it does not yet exist.
    /// </summary>
    /// <param name="builder">The Keycloak resource builder.</param>
    /// <param name="realmName">The name of the realm to ensure exists (e.g. "example-realm").</param>
    /// <returns>A resource builder for the <see cref="KeycloakRealmResource"/>.</returns>
    public static IResourceBuilder<KeycloakRealmResource> AddRealm(
        this IResourceBuilder<KeycloakResource> builder,
        string realmName)
    {
        var resourceName = $"{builder.Resource.Name}-{realmName}";
        var realmResource = new KeycloakRealmResource(resourceName, realmName, builder.Resource);

        var realmBuilder = builder.ApplicationBuilder.AddResource(realmResource).WithParentRelationship(builder);

        builder.OnResourceReady(async (keycloak, evt, ct) =>
        {
            var executionContext = evt.Services.GetRequiredService<DistributedApplicationExecutionContext>();
            if (!executionContext.IsRunMode)
                return;

            var loggerService = evt.Services.GetRequiredService<ResourceLoggerService>();
            var notificationService = evt.Services.GetRequiredService<ResourceNotificationService>();
            var logger = loggerService.GetLogger(realmResource);

            await notificationService.PublishUpdateAsync(realmResource,
                s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Starting, KnownResourceStateStyles.Info) });

            try
            {
                var keycloakBaseUrl = ResolveBaseUrl(notificationService, keycloak);

                var adminUser = keycloak.AdminUserNameParameter is not null
                    ? await keycloak.AdminUserNameParameter.GetValueAsync(ct) ?? "admin"
                    : "admin";

                var adminPassword = await keycloak.AdminPasswordParameter.GetValueAsync(ct) ?? string.Empty;

                using var httpClient = new HttpClient();
                var adminApi = new KeycloakAdminApiClient(httpClient, keycloakBaseUrl, adminUser, adminPassword);

                if (await adminApi.RealmExistsAsync(realmName, ct))
                {
                    logger.LogInformation("Realm '{RealmName}' already exists.", realmName);
                }
                else
                {
                    logger.LogInformation("Realm '{RealmName}' not found, creating...", realmName);
                    await adminApi.CreateRealmAsync(realmName, ct);
                    logger.LogInformation("Realm '{RealmName}' created successfully.", realmName);
                }

                await notificationService.PublishUpdateAsync(realmResource,
                    s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error provisioning realm '{RealmName}'.", realmName);
                await notificationService.PublishUpdateAsync(realmResource,
                    s => s with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
            }
        });

        return realmBuilder;
    }

    private static string ResolveBaseUrl(ResourceNotificationService notificationService, KeycloakResource keycloak)
    {
        if (notificationService.TryGetCurrentState(keycloak.Name, out var state))
        {
            var url = state.Snapshot.Urls.FirstOrDefault(u => !u.IsInternal);
            if (url is not null)
                return url.Url.TrimEnd('/');
        }

        var port = keycloak.Annotations.OfType<EndpointAnnotation>().FirstOrDefault()?.Port ?? 8080;
        return $"http://localhost:{port}";
    }
}