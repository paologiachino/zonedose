using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Zonedose Spike",
    Author = "Zonedose",
    Version = "0.0.1",
    Description = "Phase 0 spike: session API for the Angular app, approval flow, PostGIS and RLS on a module-owned table.",
    Dependencies =
    [
        "OrchardCore.Users",
        "OrchardCore.Users.ExternalAuthentication",
        "OrchardCore.Roles",
        "OrchardCore.Google.GoogleAuthentication",
    ]
)]
