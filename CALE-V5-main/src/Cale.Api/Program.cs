using Cale.Api.Extensions;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
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
