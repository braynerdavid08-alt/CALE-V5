using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Cale.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Cale.UnitTests;

public sealed class WireEncryptionMiddlewareTests
{
    private const string Json = "{\"title\":\"Víctimas y consecuencias\"}";

    [Fact]
    public async Task Json_response_is_encrypted_when_the_app_asks_for_it()
    {
        var context = await RunAsync("/api/student/courses/lessons/1", "gz", 200, "application/json; charset=utf-8");

        Assert.Equal("application/octet-stream", context.Response.ContentType);
        Assert.Equal("gz", context.Response.Headers[WireEncryptionMiddleware.HeaderName].ToString());
        var body = ((MemoryStream)context.Response.Body).ToArray();
        Assert.DoesNotContain("Víctimas", Encoding.UTF8.GetString(body));
        Assert.Equal(Json, Open(body, gzip: true));
    }

    [Fact]
    public async Task Response_stays_plain_without_the_header()
    {
        var context = await RunAsync("/api/student/courses/lessons/1", null, 200, "application/json");

        Assert.Equal(Json, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    [Fact]
    public async Task Errors_stay_plain_so_the_app_can_show_the_message()
    {
        var context = await RunAsync("/api/student/courses/lessons/1", "1", 400, "application/json");

        Assert.Equal(Json, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    private static async Task<HttpContext> RunAsync(string path, string? wire, int status, string contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (wire is not null)
        {
            context.Request.Headers[WireEncryptionMiddleware.HeaderName] = wire;
        }
        context.Response.Body = new MemoryStream();

        var middleware = new WireEncryptionMiddleware(async http =>
        {
            http.Response.StatusCode = status;
            http.Response.ContentType = contentType;
            await http.Response.WriteAsync(Json);
        });
        await middleware.InvokeAsync(context);
        return context;
    }

    private static string Open(byte[] sealedBody, bool gzip)
    {
        var nonce = sealedBody.AsSpan(0, 12);
        var cipher = sealedBody.AsSpan(12, sealedBody.Length - 28);
        var tag = sealedBody.AsSpan(sealedBody.Length - 16);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(WireEncryptionMiddleware.Key, 16))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        if (gzip)
        {
            using var zip = new GZipStream(new MemoryStream(plain), CompressionMode.Decompress);
            using var output = new MemoryStream();
            zip.CopyTo(output);
            plain = output.ToArray();
        }
        return Encoding.UTF8.GetString(plain);
    }
}
