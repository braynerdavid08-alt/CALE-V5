namespace Cale.Api.Infrastructure;

public static class UploadLimits
{
    /// <summary>PowerPoint / Excel / Word import with embedded media.</summary>
    public const long PresentationImportBytes = 200L * 1024 * 1024;

    /// <summary>Single image or video upload for presentation editor.</summary>
    public const long PresentationMediaBytes = 100L * 1024 * 1024;
}
