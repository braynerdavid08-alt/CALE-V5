using System.Text;
using Cale.Api.Extensions;
using Cale.Api.Infrastructure;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Presentation.Application;
using Cale.Modules.Presentation.Application.Commands;
using Cale.Modules.Presentation.Application.DTOs;
using Cale.Modules.Presentation.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "TeacherOrAdmin")]
[Route("api/presentations")]
public sealed class PresentationsController : ControllerBase
{
    private readonly PresentationCommandHandler _commands;
    private readonly PresentationQueryHandler _queries;
    private readonly PresentationExchangeService _exchange;
    private readonly IUserStore _users;
    private readonly IWebHostEnvironment _env;

    public PresentationsController(
        PresentationCommandHandler commands,
        PresentationQueryHandler queries,
        PresentationExchangeService exchange,
        IUserStore users,
        IWebHostEnvironment env)
    {
        _commands = commands;
        _queries = queries;
        _exchange = exchange;
        _users = users;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        return Ok(await _queries.ListMineAsync(userId, schoolUserId, ct));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        return Ok(await _queries.SummaryAsync(userId, schoolUserId, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        return Ok(await _queries.GetAsync(
            id,
            userId,
            schoolUserId,
            CurrentUser.IsAdmin(User),
            ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePresentationRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        var detail = await _commands.CreateAsync(
            request,
            userId,
            schoolUserId,
            ct);
        return Ok(detail);
    }

    [HttpPut("{id:int}/meta")]
    public async Task<IActionResult> UpdateMeta(
        int id,
        [FromBody] UpdatePresentationMetaRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        await _commands.UpdateMetaAsync(
            id,
            request,
            userId,
            schoolUserId,
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }

    [HttpPut("{id:int}/document")]
    public async Task<IActionResult> SaveDocument(
        int id,
        [FromBody] SavePresentationDocumentRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        return Ok(await _commands.SaveDocumentAsync(
            id,
            request,
            userId,
            schoolUserId,
            CurrentUser.IsAdmin(User),
            ct));
    }

    [HttpPost("{id:int}/duplicate")]
    public async Task<IActionResult> Duplicate(int id, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        return Ok(await _commands.DuplicateAsync(
            id,
            userId,
            schoolUserId,
            CurrentUser.IsAdmin(User),
            ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        await _commands.DeleteAsync(
            id,
            userId,
            schoolUserId,
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }

    [HttpGet("import/template")]
    public IActionResult ImportTemplate([FromQuery] string format)
    {
        var f = (format ?? "xlsx").Trim().ToLowerInvariant();
        if (f is "doc" or "docx" or "word")
        {
            var bytes = _exchange.BuildWordTemplate();
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "cale-presentacion-plantilla.docx");
        }

        var excel = _exchange.BuildExcelTemplate();
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cale-presentacion-plantilla.xlsx");
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.PresentationImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.PresentationImportBytes)]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile? file,
        [FromForm] string? title,
        [FromForm] string? description,
        [FromForm] string? category,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Selecciona un archivo Excel, Word o PowerPoint.", 400, "invalid_file");
        }

        if (file.Length > UploadLimits.PresentationImportBytes)
        {
            throw new DomainException(
                "El archivo debe pesar 200 MB o menos.",
                400,
                "import_file_too_large");
        }

        await using var stream = file.OpenReadStream();
        IReadOnlyList<ImportedSlideOutline> outlines;
        try
        {
            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
            var uploadsDir = Path.Combine(webRoot, "uploads", "presentations");
            outlines = _exchange.ParseImport(stream, file.FileName, uploadsDir);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, 400, "invalid_import");
        }

        var deckTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : title.Trim();

        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        var detail = await _commands.ImportFromOutlinesAsync(
            deckTitle,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            outlines,
            userId,
            schoolUserId,
            ct);
        return Ok(detail);
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, [FromQuery] string format, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var schoolUserId = await ResolveSchoolUserIdAsync(userId, ct);
        var detail = await _queries.GetAsync(
            id,
            userId,
            schoolUserId,
            CurrentUser.IsAdmin(User),
            ct);
        var f = (format ?? "xlsx").Trim().ToLowerInvariant();
        var safeName = SanitizeFileName(detail.Title);

        if (f is "doc" or "docx" or "word")
        {
            var bytes = _exchange.ExportWord(detail);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{safeName}.docx");
        }

        if (f is "ppt" or "pptx" or "powerpoint")
        {
            var bytes = _exchange.ExportPowerPoint(detail);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                $"{safeName}.pptx");
        }

        var excel = _exchange.ExportExcel(detail);
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{safeName}.xlsx");
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.PresentationMediaBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.PresentationMediaBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Selecciona un archivo.", 400, "invalid_file");
        }

        if (file.Length > UploadLimits.PresentationMediaBytes)
        {
            throw new DomainException(
                "El archivo debe pesar 100 MB o menos.",
                400,
                "presentation_file_too_large");
        }

        var ext = Path.GetExtension(file.FileName);
        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };
        var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mov", ".m4v"
        };

        string mediaType;
        if (imageExts.Contains(ext))
        {
            mediaType = "image";
        }
        else if (videoExts.Contains(ext))
        {
            mediaType = "video";
        }
        else
        {
            throw new DomainException(
                "Usa jpg, png, gif, webp o video mp4/webm/mov.",
                400,
                "invalid_file");
        }

        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;
        var folder = Path.Combine(webRoot, "uploads", "presentations");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var path = Path.Combine(folder, name);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream, ct);
        return Ok(new { url = $"/uploads/presentations/{name}", mediaType });
    }

    private async Task<int?> ResolveSchoolUserIdAsync(int userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return null;
        }

        var role = Roles.Normalize(user.Role);
        if (role == Roles.School)
        {
            return user.Id;
        }

        return user.SchoolId;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            sb.Append(invalid.Contains(ch) ? '-' : ch);
        }

        var result = sb.ToString().Trim('-', ' ');
        return string.IsNullOrWhiteSpace(result) ? "presentacion" : result;
    }
}
