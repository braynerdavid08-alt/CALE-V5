using Cale.Modules.Catalog.Application.Abstractions;

namespace Cale.Api.Middleware;

/// <summary>
/// Serves old /uploads/* exam image URLs from disk when the file still exists
/// (pre-DB migration). New uploads use /api/media/{guid}.
/// </summary>
public sealed class LegacyCatalogUploadMiddleware
{
    private readonly RequestDelegate _next;

    public LegacyCatalogUploadMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICatalogMediaStore media)
    {
        var path = context.Request.Path.Value ?? "";
        const string prefix = "/uploads/";
        if (context.Request.Method == HttpMethods.Get
            && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/uploads/presentations/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = path[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(fileName)
                && fileName.IndexOf('/') < 0
                && fileName.IndexOf('\\') < 0)
            {
                var blob = await media.TryReadLegacyDiskAsync(fileName, context.RequestAborted);
                if (blob is not null)
                {
                    context.Response.ContentType = blob.Value.ContentType;
                    context.Response.Headers.CacheControl = "public,max-age=86400,immutable";
                    context.Response.ContentLength = blob.Value.Data.LongLength;
                    await context.Response.Body.WriteAsync(blob.Value.Data, context.RequestAborted);
                    return;
                }
            }
        }

        await _next(context);
    }
}
