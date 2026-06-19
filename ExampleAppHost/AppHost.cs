var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("keycloak-username", "admin");
var password = builder.AddParameter("keycloak-password", "admin");
var keycloak = builder.AddKeycloak("keycloak", 8080, username, password);

var realm = keycloak.AddRealm("example-realm");
var realmAdminRole = realm.WithRole("realm-admin", "Realm-level administrative role");

var user1 = realm.AddUser("user1", "user1@example-realm.com", "User", "One", "123456", "user1").WithRealmRole(realmAdminRole);
var user2 = realm.AddUser("user2", "user2@example-realm.com", "User", "Two", "123456", "user2");

var client = realm.AddClient("example-client", "Example Client", "Example application")
    .WithClientType(KeycloakClientType.OpenIdConnect)
    .WithStandardFlow()
    .WithClientAuthentication()
    .WithRedirectUris("https://localhost:*/authentication/login-callback")
    .WithPostLogoutRedirectUris("https://localhost:*/authentication/logout-callback")
    .WithWebOrigins("https://localhost:*")
    .WithClientSecret("rxz7YdttSweRAMcCj0fIsgCmjHiosZR5")
    .WithTokenExchange()
    .WithJwtAuthorizationGrant()
    .WithDeviceAuthorizationGrant()
    .WithCibaGrant()
    .WithRoleMapper(mapper => mapper.AddToAccessToken().AddToIdToken());

var adminRole = client.WithRole("admin", "Full administrative access", mapper => mapper.AddToAccessToken().AddToIdToken());
var userRole = client.WithRole("user", "Standard user access");

user1.WithClientRole(adminRole);
user2.WithClientRole(userRole);

builder.Build().Run();