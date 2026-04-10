var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("keycloak-username", "admin");
var password = builder.AddParameter("keycloak-password", "admin");
var keycloak = builder.AddKeycloak("keycloak", 8080, username, password);

var realm = keycloak.AddRealm("example-realm");
var user1 = realm.AddUser("user1", "user1@example-realm.com", "User", "One", "123456", "user1");
var user2 = realm.AddUser("user2", "user2@example-realm.com", "User", "Two", "123456", "user2");

builder.Build().Run();