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
    ///     Adds a Keycloak realm resource to the application model. On startup, once the Keycloak container is ready, the
    ///     realm is checked for existence via the Admin REST API and created if it does not yet exist.Any users added via
    ///     <see cref="AddUser" /> are provisioned immediately after.
    /// </summary>
    /// <param name="builder">The Keycloak resource builder.</param>
    /// <param name="realmName">The name of the realm to ensure exists (e.g. "example-realm").</param>
    /// <returns>A resource builder for the <see cref="KeycloakRealmResource" />.</returns>
    public static IResourceBuilder<KeycloakRealmResource> AddRealm(this IResourceBuilder<KeycloakResource> builder, string realmName)
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

                foreach (var annotation in realmResource.Annotations.OfType<KeycloakClientAnnotation>())
                    await ProvisionClientAsync(adminApi, realmName, annotation.ClientResource, loggerService, notificationService, ct);
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

    /// <summary>
    ///     Adds a Keycloak client to the realm. On startup, once the realm is ready, the client is checked for existence via
    ///     the Admin REST API and created if it does not yet exist. Use the returned builder to configure client type and
    ///     settings via the <c>With*</c> extension methods.
    /// </summary>
    /// <param name="builder">The realm resource builder.</param>
    /// <param name="clientId">The static client ID (e.g. "my-app").</param>
    /// <param name="name">The display name of the client.</param>
    /// <param name="description">Optional description.</param>
    /// <returns>A resource builder for the <see cref="KeycloakClientResource" />.</returns>
    public static IResourceBuilder<KeycloakClientResource> AddClient(this IResourceBuilder<KeycloakRealmResource> builder, string clientId, string name, string? description = null)
    {
        var resourceName = $"{builder.Resource.Name}-{clientId}";
        var clientResource = new KeycloakClientResource(resourceName, clientId, name, description, builder.Resource);

        builder.Resource.Annotations.Add(new KeycloakClientAnnotation(clientResource));

        return builder.ApplicationBuilder.AddResource(clientResource).WithParentRelationship(builder);
    }

    /// <summary>
    ///     Sets the client protocol. Defaults to <see cref="KeycloakClientType.OpenIdConnect" />.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithClientType(this IResourceBuilder<KeycloakClientResource> builder, KeycloakClientType clientType)
    {
        builder.Resource.Protocol = clientType switch
        {
            KeycloakClientType.Saml => "saml",
            var _ => "openid-connect"
        };
        return builder;
    }

    /// <summary>
    ///     When <paramref name="enabled" /> is <c>true</c> (default), the client is confidential and a secret is required.
    ///     When <c>false</c>, the client is public.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithClientAuthentication(this IResourceBuilder<KeycloakClientResource> builder, bool enabled = true)
    {
        builder.Resource.PublicClient = !enabled;
        builder.Resource.BearerOnly = false;
        return builder;
    }

    /// <summary>
    ///     Configures the client as public (no client secret required).
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithPublicAccess(this IResourceBuilder<KeycloakClientResource> builder)
    {
        builder.Resource.PublicClient = true;
        builder.Resource.BearerOnly = false;
        return builder;
    }

    /// <summary>
    ///     Configures the client as confidential (client secret required).
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithConfidentialAccess(this IResourceBuilder<KeycloakClientResource> builder)
    {
        builder.Resource.PublicClient = false;
        builder.Resource.BearerOnly = false;
        return builder;
    }

    /// <summary>
    ///     Configures the client as bearer-only. The client will only verify tokens, never initiate a login.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithBearerOnly(this IResourceBuilder<KeycloakClientResource> builder)
    {
        builder.Resource.BearerOnly = true;
        return builder;
    }

    /// <summary>
    ///     Enables or disables the standard Authorization Code flow. Enabled by default.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithStandardFlow(this IResourceBuilder<KeycloakClientResource> builder, bool enabled = true)
    {
        builder.Resource.StandardFlowEnabled = enabled;
        return builder;
    }

    /// <summary>
    ///     Enables or disables the Implicit flow. Disabled by default.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithImplicitFlow(this IResourceBuilder<KeycloakClientResource> builder, bool enabled = true)
    {
        builder.Resource.ImplicitFlowEnabled = enabled;
        return builder;
    }

    /// <summary>
    ///     Enables or disables Direct Access Grants (Resource Owner Password Credentials). Disabled by default.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithDirectAccessGrants(this IResourceBuilder<KeycloakClientResource> builder, bool enabled = true)
    {
        builder.Resource.DirectAccessGrantsEnabled = enabled;
        return builder;
    }

    /// <summary>
    ///     Enables or disables Service Accounts (Client Credentials flow). Disabled by default.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithServiceAccounts(this IResourceBuilder<KeycloakClientResource> builder, bool enabled = true)
    {
        builder.Resource.ServiceAccountsEnabled = enabled;
        return builder;
    }

    /// <summary>
    ///     Sets a static client secret. Only applies to confidential clients (<see cref="WithConfidentialAccess" />).
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithClientSecret(this IResourceBuilder<KeycloakClientResource> builder, string secret)
    {
        builder.Resource.Secret = secret;
        return builder;
    }

    /// <summary>
    ///     Sets the allowed redirect URIs for the client.
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithRedirectUris(this IResourceBuilder<KeycloakClientResource> builder, params string[] uris)
    {
        builder.Resource.RedirectUris.Clear();
        builder.Resource.RedirectUris.AddRange(uris);
        return builder;
    }

    /// <summary>
    ///     Sets the allowed web origins for the client (used for CORS).
    /// </summary>
    public static IResourceBuilder<KeycloakClientResource> WithWebOrigins(this IResourceBuilder<KeycloakClientResource> builder, params string[] origins)
    {
        builder.Resource.WebOrigins.Clear();
        builder.Resource.WebOrigins.AddRange(origins);
        return builder;
    }

    private static async Task ProvisionClientAsync(KeycloakAdminApiClient adminApi, string realmName, KeycloakClientResource clientResource, ResourceLoggerService loggerService,
        ResourceNotificationService notificationService, CancellationToken ct)
    {
        var logger = loggerService.GetLogger(clientResource);

        await notificationService.PublishUpdateAsync(clientResource,
            s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Starting, KnownResourceStateStyles.Info) });

        try
        {
            if (await adminApi.ClientExistsAsync(realmName, clientResource.ClientId, ct))
                logger.LogInformation("Client '{ClientId}' already exists in realm '{RealmName}'.", clientResource.ClientId, realmName);
            else
            {
                logger.LogInformation("Client '{ClientId}' not found in realm '{RealmName}', creating...", clientResource.ClientId, realmName);

                var representation = new ClientRepresentation(
                    clientResource.ClientId,
                    clientResource.ClientName,
                    clientResource.Description,
                    clientResource.Protocol,
                    true,
                    clientResource.PublicClient,
                    clientResource.BearerOnly,
                    clientResource.StandardFlowEnabled,
                    clientResource.ImplicitFlowEnabled,
                    clientResource.DirectAccessGrantsEnabled,
                    clientResource.ServiceAccountsEnabled,
                    clientResource.RedirectUris.Count > 0 ? [.. clientResource.RedirectUris] : null,
                    clientResource.WebOrigins.Count > 0 ? [.. clientResource.WebOrigins] : null,
                    clientResource.Secret);

                await adminApi.CreateClientAsync(realmName, representation, ct);
                logger.LogInformation("Client '{ClientId}' created successfully in realm '{RealmName}'.", clientResource.ClientId, realmName);
            }

            await notificationService.PublishUpdateAsync(clientResource,
                s => s with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error provisioning client '{ClientId}' in realm '{RealmName}'.", clientResource.ClientId, realmName);
            await notificationService.PublishUpdateAsync(clientResource,
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