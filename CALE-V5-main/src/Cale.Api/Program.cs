using Cale.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddCaleServices();

var app = builder.Build();
await app.UseCalePipelineAsync();
app.Run();
