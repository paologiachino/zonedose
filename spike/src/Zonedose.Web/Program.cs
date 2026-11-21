var builder = WebApplication.CreateBuilder(args);

// AutoSetup must be available before a tenant is set up: configuration as code,
// no manual setup screens (phase 1 requirement)
builder.Services.AddOrchardCms().AddSetupFeatures("OrchardCore.AutoSetup");

var app = builder.Build();

app.UseStaticFiles();
app.UseOrchardCore();

await app.RunAsync();
