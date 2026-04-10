using Aspire.Hosting.ApplicationModel;
using KeyCloak.Aspire.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
///     Extension methods for adding Keycloak realm and user resources to an <see cref="IDistributedApplicationBuilder" />.
/// </summary>
public static class KeycloakRealmResourceBuilderExtensions
{
    /// <summary>
    ///     Adds a Keycloak realm resource to the application model.
    ///     On startup, once the Keycloak container is ready, the realm is checked for existence
    ///     via the Admin REST API and created if it does not yet exist.
    ///     Any users added via <see cref="AddUser" /> are provisioned immediately after.
    /// </summary>
    /// <param name="builder">The Keycloak resource builder.</param>
    /// <param name="realmName">The name of the realm to ensure exists (e.g. "example-realm").</param>
    /// <returns>A resource builder for the <see cref="KeycloakRealmResource" />.</returns>
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
                    logger.LogInformation("Realm '{RealmName}' already exists.", realmName);
                else
                {
                    logger.LogInformation("Realm '{RealmName}' not found, creating...", realmName);
                    await adminApi.CreateRealmAsync(realmName, ct);
                    logger.LogInformation("Realm '{RealmName}' created successfully.", realmName);
                }

                await notificationService.PublishUpdateAsync(realmResource,
                    s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });

                foreach (var annotation in realmResource.Annotations.OfType<KeycloakUserAnnotation>())
                    await ProvisionUserAsync(adminApi, realmName, annotation.UserResource, loggerService, notificationService, ct);
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

    /// <summary>
    ///     Adds a user to the Keycloak realm. On startup, once the realm is ready, the user is checked for existence via the
    ///     Admin REST API and created if it does not yet exist. Only password-based login is configured.
    /// </summary>
    /// <param name="builder">The realm resource builder.</param>
    /// <param name="username">The user's login name.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="firstName">The user's first name.</param>
    /// <param name="lastName">The user's last name.</param>
    /// <param name="password">The initial (non-temporary) password.</param>
    /// <param name="id">Optional fixed ID to assign to the user.</param>
    /// <returns>A resource builder for the <see cref="KeycloakUserResource" />.</returns>
    public static IResourceBuilder<KeycloakUserResource> AddUser(this IResourceBuilder<KeycloakRealmResource> builder,
        string username, string email, string firstName, string lastName, string password, string? id = null)
    {
        var resourceName = $"{builder.Resource.Name}-{username}";
        var userResource = new KeycloakUserResource(resourceName, id, username, email, firstName, lastName, password, builder.Resource);

        builder.Resource.Annotations.Add(new KeycloakUserAnnotation(userResource));

        return builder.ApplicationBuilder.AddResource(userResource).WithParentRelationship(builder);
    }

    private static async Task ProvisionUserAsync(KeycloakAdminApiClient adminApi, string realmName, KeycloakUserResource user, ResourceLoggerService loggerService,
        ResourceNotificationService notificationService, CancellationToken ct)
    {
        var logger = loggerService.GetLogger(user);

        await notificationService.PublishUpdateAsync(user,
            s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Starting, KnownResourceStateStyles.Info) });

        try
        {
            if (await adminApi.UserExistsAsync(realmName, user.Username, ct))
                logger.LogInformation("User '{Username}' already exists in realm '{RealmName}'.", user.Username, realmName);
            else
            {
                logger.LogInformation("User '{Username}' not found in realm '{RealmName}', creating...", user.Username, realmName);
                await adminApi.CreateUserAsync(realmName, user, ct);
                logger.LogInformation("User '{Username}' created successfully in realm '{RealmName}'.", user.Username, realmName);
            }

            await notificationService.PublishUpdateAsync(user,
                s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error provisioning user '{Username}' in realm '{RealmName}'.", user.Username, realmName);
            await notificationService.PublishUpdateAsync(user,
                s => s with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
        }
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