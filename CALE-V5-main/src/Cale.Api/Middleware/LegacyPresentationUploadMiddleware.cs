using Cale.Modules.Presentation.Application.Abstractions;

namespace Cale.Api.Middleware;

/// <summary>
/// Serves old /uploads/presentations/* URLs from disk when present (pre-DB migration uploads).
/// </summary>
public sealed class LegacyPresentationUploadMiddleware
{
    private readonly RequestDelegate _next;

    public LegacyPresentationUploadMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IPresentationMediaStore media)
    {
        var path = context.Request.Path.Value ?? "";
        const string prefix = "/uploads/presentations/";
        if (context.Request.Method == HttpMethods.Get
            && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = path[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(fileName) && fileName.IndexOf('/') < 0)
            {
                var blob = await media.TryReadLegacyDiskAsync(fileName, context.RequestAborted);
                if (blob is not null)
                {
                    context.Response.ContentType = blob.Value.ContentType;
                    context.Response.Headers.CacheControl = "public,max-age=86400";
                    await context.Response.Body.WriteAsync(blob.Value.Data, context.RequestAborted);
                    return;
                }
            }
        }

        await _next(context);
    }
}
