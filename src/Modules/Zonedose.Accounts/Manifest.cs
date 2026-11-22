using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Zonedose Accounts",
    Author = "Zonedose",
    Website = "https://github.com/paologiachino/zonedose",
    Version = "0.1.0",
    Description = "Google sign-in, account approval, server-side sessions and user administration for the Angular frontend.",
    Dependencies =
    [
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "Zonedose.Platform",
    ]
)]
