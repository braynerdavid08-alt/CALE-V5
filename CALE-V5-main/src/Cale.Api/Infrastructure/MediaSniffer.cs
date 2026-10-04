using System.Text;

namespace Cale.Api.Infrastructure;

/// <summary>
/// Decides an upload's content type from its first bytes. The client's Content-Type and file
/// name are never trusted: a "photo.png" that is really HTML or SVG is rejected.
/// </summary>
public static class MediaSniffer
{
    public sealed record Detected(string Kind, string ContentType, string Extension);

    public static async Task<Detected?> DetectAsync(IFormFile file, CancellationToken ct)
    {
        var head = new byte[32];
        await using var stream = file.OpenReadStream();
        var read = 0;
        while (read < head.Length)
        {
            var n = await stream.ReadAsync(head.AsMemory(read), ct);
            if (n == 0)
            {
                break;
            }
            read += n;
        }

        return Detect(head.AsSpan(0, read));
    }

    public static Detected? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
        {
            return new("image", "image/jpeg", ".jpg");
        }
        if (h.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return new("image", "image/png", ".png");
        }
        if (Ascii(h, 0, "GIF87a") || Ascii(h, 0, "GIF89a"))
        {
            return new("image", "image/gif", ".gif");
        }
        if (Ascii(h, 0, "RIFF") && Ascii(h, 8, "WEBP"))
        {
            return new("image", "image/webp", ".webp");
        }
        if (Ascii(h, 0, "BM") && h.Length >= 14)
        {
            return new("image", "image/bmp", ".bmp");
        }
        if (Ascii(h, 0, "%PDF-"))
        {
            return new("document", "application/pdf", ".pdf");
        }
        if (Ascii(h, 4, "ftyp"))
        {
            if (Ascii(h, 8, "qt  "))
            {
                return new("video", "video/quicktime", ".mov");
            }
            if (Ascii(h, 8, "M4A "))
            {
                return new("audio", "audio/mp4", ".m4a");
            }
            if (Ascii(h, 8, "M4V "))
            {
                return new("video", "video/x-m4v", ".m4v");
            }
            return new("video", "video/mp4", ".mp4");
        }
        if (h.StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return new("video", "video/webm", ".webm");
        }
        if (Ascii(h, 0, "RIFF") && Ascii(h, 8, "AVI "))
        {
            return new("video", "video/x-msvideo", ".avi");
        }
        if (Ascii(h, 0, "RIFF") && Ascii(h, 8, "WAVE"))
        {
            return new("audio", "audio/wav", ".wav");
        }
        if (Ascii(h, 0, "OggS"))
        {
            return new("audio", "audio/ogg", ".ogg");
        }
        if (Ascii(h, 0, "ID3") || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0))
        {
            return new("audio", "audio/mpeg", ".mp3");
        }
        return null;
    }

    private static bool Ascii(ReadOnlySpan<byte> h, int offset, string text) =>
        h.Length >= offset + text.Length
        && h.Slice(offset, text.Length).SequenceEqual(Encoding.ASCII.GetBytes(text));
}

/// <summary>Headers for serving user-uploaded files so they can never run as a page on our origin.</summary>
public static class SafeMediaResponse
{
    private static readonly string[] InlineTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp"];

    public static string Prepare(HttpResponse response, string? storedContentType)
    {
        var type = (storedContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var inline = InlineTypes.Contains(type)
            || type.StartsWith("video/", StringComparison.Ordinal)
            || type.StartsWith("audio/", StringComparison.Ordinal)
            || type == "application/pdf";

        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self' data:; media-src 'self'; style-src 'unsafe-inline'; sandbox";
        if (!inline)
        {
            response.Headers.ContentDisposition = "attachment";
            return "application/octet-stream";
        }

        return type;
    }
}
