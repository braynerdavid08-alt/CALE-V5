using System.IO.Compression;
using System.Security.Cryptography;

namespace Cale.Api.Middleware;

/// <summary>
/// When the SPA asks for it (X-Cale-Wire header), successful JSON responses from /api are
/// gzipped and AES-GCM encrypted, so DevTools' Network panel shows binary instead of the content.
/// The key ships inside the app bundle: this keeps casual copying out, not a determined reverse engineer.
/// Must match frontend/src/app/core/http/wire.interceptor.ts.
/// </summary>
public sealed class WireEncryptionMiddleware
{
    public const string HeaderName = "X-Cale-Wire";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly byte[] KeyA =
    [
        0x3a, 0x91, 0x5c, 0xe7, 0x12, 0x8f, 0x44, 0xd0, 0x6b, 0x27, 0xf3, 0x09, 0xae, 0x5d, 0x71, 0xc8,
        0x95, 0x1e, 0x60, 0xb4, 0x2f, 0xd9, 0x83, 0x4a, 0x07, 0xec, 0x58, 0x36, 0xa1, 0x7c, 0xf0, 0x1b
    ];
    private static readonly byte[] KeyB =
    [
        0xc4, 0x0d, 0xa7, 0x39, 0xe8, 0x52, 0x1f, 0x9b, 0x70, 0xd6, 0x2c, 0x85, 0x4e, 0xb3, 0x6a, 0x17,
        0xfe, 0x41, 0x98, 0x2d, 0x63, 0xca, 0x05, 0xbf, 0x76, 0x1a, 0xe4, 0x8d, 0x30, 0x59, 0xa2, 0xcf
    ];
    public static readonly byte[] Key = KeyA.Zip(KeyB, (a, b) => (byte)(a ^ b)).ToArray();

    private readonly RequestDelegate _next;

    public WireEncryptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || !context.Request.Headers.TryGetValue(HeaderName, out var mode))
        {
            await _next(context);
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        var response = context.Response;
        var isJson = response.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
        if (response.StatusCode is < 200 or >= 300 || !isJson || buffer.Length == 0)
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(original, context.RequestAborted);
            return;
        }

        var gzip = string.Equals(mode, "gz", StringComparison.OrdinalIgnoreCase);
        var sealedBody = Seal(buffer.ToArray(), gzip);
        response.Headers.Remove("Content-Length");
        response.Headers[HeaderName] = gzip ? "gz" : "1";
        response.Headers.CacheControl = "no-store";
        response.ContentType = "application/octet-stream";
        response.ContentLength = sealedBody.Length;
        await original.WriteAsync(sealedBody, context.RequestAborted);
    }

    public static byte[] Seal(byte[] plain, bool gzip)
    {
        if (gzip)
        {
            using var packed = new MemoryStream();
            using (var zip = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true))
            {
                zip.Write(plain);
            }
            plain = packed.ToArray();
        }

        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(Key, TagSize);
        aes.Encrypt(
            nonce,
            plain,
            output.AsSpan(NonceSize, plain.Length),
            output.AsSpan(NonceSize + plain.Length, TagSize));
        return output;
    }
}
