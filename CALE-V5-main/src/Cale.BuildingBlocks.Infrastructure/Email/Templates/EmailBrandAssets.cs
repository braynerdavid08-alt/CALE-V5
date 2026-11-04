using System.Reflection;
using Cale.BuildingBlocks.Domain.Email;

namespace Cale.BuildingBlocks.Infrastructure.Email.Templates;

public static class EmailBrandAssets
{
    public const string LogoContentId = "luzverde-logo";
    public const string LogoFileName = "luz-verde-logo.png";
    public const string LogoContentType = "image/png";

    /// <summary>Resource name ends with this suffix (SDK embeds as Namespace.Folder.file).</summary>
    private const string LogoResourceSuffix = "Email.Assets.luz-verde-logo.png";

    private static readonly Lazy<byte[]?> LogoBytes = new(LoadLogoBytes);

    public static EmailEmbeddedImage? TryCreateLogo()
    {
        var bytes = LogoBytes.Value;
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        return new EmailEmbeddedImage(LogoContentId, LogoFileName, LogoContentType, bytes);
    }

    public static string? TryCreateLogoDataUri()
    {
        var bytes = LogoBytes.Value;
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    private static byte[]? LoadLogoBytes()
    {
        var assembly = typeof(EmailBrandAssets).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(LogoResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
