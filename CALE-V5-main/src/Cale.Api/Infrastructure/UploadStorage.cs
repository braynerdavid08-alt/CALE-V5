namespace Cale.Api.Infrastructure;

/// <summary>
/// Resolves where uploaded files are stored on disk and their public URL paths.
/// On Render, mount a persistent disk at /app/wwwroot/uploads (see render.yaml).
/// </summary>
public sealed class UploadStorage
{
    private readonly string _uploadsRoot;

    public UploadStorage(IWebHostEnvironment env, IConfiguration config)
    {
        var configured = config["Uploads:Root"]
            ?? Environment.GetEnvironmentVariable("UPLOADS_ROOT");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            _uploadsRoot = Path.GetFullPath(configured.Trim());
        }
        else
        {
            var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;
            _uploadsRoot = Path.Combine(webRoot, "uploads");
        }

        Directory.CreateDirectory(_uploadsRoot);
        Directory.CreateDirectory(PresentationsDirectory);
    }

    public string UploadsRoot => _uploadsRoot;

    public string PresentationsDirectory => Path.Combine(_uploadsRoot, "presentations");

    public string PresentationsPublicPrefix => "/uploads/presentations";

    public async Task<string> SavePresentationFileAsync(
        Stream content,
        string fileName,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(PresentationsDirectory);
        var safeName = Path.GetFileName(fileName);
        var path = Path.Combine(PresentationsDirectory, safeName);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, ct);
        return $"{PresentationsPublicPrefix}/{safeName}";
    }

    public bool PresentationFileExists(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            return false;
        }

        var fileName = Path.GetFileName(publicUrl);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return File.Exists(Path.Combine(PresentationsDirectory, fileName));
    }
}
