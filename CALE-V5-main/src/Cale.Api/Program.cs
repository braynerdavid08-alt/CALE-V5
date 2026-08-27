using Cale.Api.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

// Render/Free Linux containers hit inotify limits if config files are watched.
Environment.SetEnvironmentVariable(
    "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE",
    "false");

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    // Register before any MVC/antiforgery defaults; JWT auth does not need persisted keys.
    builder.Services.AddDataProtection()
        .SetApplicationName("Cale.Api")
        .UseEphemeralDataProtectionProvider();
}

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Development.local.json",
        optional: true,
        reloadOnChange: false);
}
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});
builder.AddCaleServices();

var app = builder.Build();
await app.UseCalePipelineAsync();
app.Run();
