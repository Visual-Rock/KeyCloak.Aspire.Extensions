# KeyCloak.Aspire.Extensions

KeyCloak.Aspire.Extensions provides extension methods for .NET Aspire to automate the provisioning of Keycloak resources. This library allows you to define realms, clients, users,
and roles directly in your Aspire AppHost.

## Features

- Automatic realm creation.
- Client provisioning (OpenID Connect and SAML).
- User creation with initial passwords.
- Realm and client-scoped role management.
- User-to-role assignments.
- Protocol mapper configuration for mapping roles into token claims.
- Integrated with .NET Aspire resource lifecycle (provisioning starts when the Keycloak container is ready).

## Prerequisites

- .NET 10.0 or later
- .NET Aspire workload
- Keycloak container resource added to your AppHost

## Installation

Add the library to your Aspire AppHost project:

```bash
dotnet add package KeyCloak.Aspire.Extensions
```

## Usage

In your `Program.cs` (or `AppHost.cs`) of the AppHost project, you can configure Keycloak resources as follows:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add the standard Keycloak resource
var keycloak = builder.AddKeycloak("keycloak");

// Define a realm
var realm = keycloak.AddRealm("example-realm");

// Add a realm role
var realmAdminRole = realm.WithRole("realm-admin", "Realm-level administrative role");

// Add users to the realm
var user1 = realm.AddUser("user1", "user1@example.com", "First", "Last", "password123")
                 .WithRealmRole(realmAdminRole);

// Add a client to the realm
var client = realm.AddClient("example-client", "Example Client")
    .WithPublicAccess()
    .WithStandardFlow()
    .WithRedirectUris("https://localhost:5001/signin-oidc")
    .WithRoleMapper(mapper => mapper.AddToAccessToken().AddToIdToken());

// Add client-specific roles
var adminRole = client.WithRole("admin");
var userRole = client.WithRole("user");

// Assign client roles to users
user1.WithClientRole(adminRole);

builder.Build().Run();
```

## API Reference

### Realm Extensions

- `AddRealm(string realmName)`: Adds a realm resource.
- `WithRole(string name, string? description)`: Adds a realm-level role.
- `AddUser(string username, string email, string firstName, string lastName, string password)`: Provisions a user.
- `AddClient(string clientId, string name, string? description)`: Provisions a client.

### Client Extensions

- `WithClientType(KeycloakClientType type)`: Sets protocol (OpenIdConnect or Saml).
- `WithPublicAccess()` / `WithConfidentialAccess()`: Configures client authentication.
- `WithStandardFlow()` / `WithImplicitFlow()`: Configures OIDC flows.
- `WithClientSecret(string secret)`: Sets a static secret for confidential clients.
- `WithRedirectUris(params string[] uris)`: Configures allowed redirect URIs.
- `WithRole(string name, string? description, Action<KeycloakRoleMapperBuilder>? configureMapper)`: Adds a client-scoped role.
- `WithRoleMapper(Action<KeycloakRoleMapperBuilder> configure)`: Configures JWT protocol mappers for roles.

### User Extensions

- `WithRealmRole(IResourceBuilder<KeycloakRealmRoleResource> role)`: Assigns a realm role.
- `WithClientRole(IResourceBuilder<KeycloakClientRoleResource> role)`: Assigns a client role.

## License

This project is licensed under the MIT License.
