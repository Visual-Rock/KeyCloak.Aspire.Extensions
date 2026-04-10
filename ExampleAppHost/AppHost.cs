var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("keycloak-username", "admin");
var password = builder.AddParameter("keycloak-password", "admin");
var keycloak = builder.AddKeycloak("keycloak", 8080, username, password);

var realm = keycloak.AddRealm("example-realm");

builder.Build().Run();