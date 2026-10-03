using Cale.Api.Services.Play;

namespace Cale.Api.Middleware;

/// <summary>
/// /signals/{code}.svg are generic drawings; when an instructor's sign exam has the real
/// picture for that code, the browser is sent there instead.
/// </summary>
public sealed class SignImageRedirectMiddleware
{
    private const string Prefix = "/signals/";
    private readonly RequestDelegate _next;

    public SignImageRedirectMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, SignImageOverrides overrides)
    {
        var path = context.Request.Path.Value ?? "";
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var code = path[Prefix.Length..^4];
            if (code.Length is > 0 and <= 12 && code.All(c => char.IsLetterOrDigit(c) || c == '-'))
            {
                var image = await overrides.ImageForAsync(code, context.RequestAborted);
                if (image is not null)
                {
                    context.Response.Headers.CacheControl = "public,max-age=300";
                    context.Response.Redirect(image);
                    return;
                }
            }
        }

        await _next(context);
    }
}
