using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Exceptions;
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
    private readonly IWebHostEnvironment _env;

    public PresentationsController(
        PresentationCommandHandler commands,
        PresentationQueryHandler queries,
        IWebHostEnvironment env)
    {
        _commands = commands;
        _queries = queries;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _queries.ListMineAsync(CurrentUser.GetId(User), ct));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        Ok(await _queries.SummaryAsync(CurrentUser.GetId(User), ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await _queries.GetAsync(id, CurrentUser.GetId(User), CurrentUser.IsAdmin(User), ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePresentationRequest request,
        CancellationToken ct)
    {
        var detail = await _commands.CreateAsync(
            request,
            CurrentUser.GetId(User),
            schoolId: null,
            ct);
        return Ok(detail);
    }

    [HttpPut("{id:int}/meta")]
    public async Task<IActionResult> UpdateMeta(
        int id,
        [FromBody] UpdatePresentationMetaRequest request,
        CancellationToken ct)
    {
        await _commands.UpdateMetaAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }

    [HttpPut("{id:int}/document")]
    public async Task<IActionResult> SaveDocument(
        int id,
        [FromBody] SavePresentationDocumentRequest request,
        CancellationToken ct) =>
        Ok(await _commands.SaveDocumentAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));

    [HttpPost("{id:int}/duplicate")]
    public async Task<IActionResult> Duplicate(int id, CancellationToken ct) =>
        Ok(await _commands.DuplicateAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _commands.DeleteAsync(id, CurrentUser.GetId(User), CurrentUser.IsAdmin(User), ct);
        return NoContent();
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Selecciona una imagen.", 400, "invalid_file");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            throw new DomainException("La imagen debe pesar 5 MB o menos.", 400, "file_too_large");
        }

        var ext = Path.GetExtension(file.FileName);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };
        if (!allowed.Contains(ext))
        {
            throw new DomainException("Usa jpg, png, gif o webp.", 400, "invalid_file");
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
        return Ok(new { url = $"/uploads/presentations/{name}" });
    }
}
