using Cale.Api.Extensions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school")]
public sealed class SchoolApprenticeController : ControllerBase
{
    private readonly ApprenticeRegistryService _registry;
    private readonly SchoolExcelImportService _import;

    public SchoolApprenticeController(
        ApprenticeRegistryService registry,
        SchoolExcelImportService import)
    {
        _registry = registry;
        _import = import;
    }

    private int SchoolId => CurrentUser.GetId(User);

    [HttpGet("apprentices")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? month,
        [FromQuery] bool? withBalance,
        CancellationToken ct) =>
        Ok(await _registry.ListAsync(SchoolId, search, month, withBalance, ct));

    [HttpGet("apprentices/{studentUserId:int}")]
    public async Task<IActionResult> GetDetail(int studentUserId, CancellationToken ct) =>
        Ok(await _registry.GetDetailAsync(SchoolId, studentUserId, ct));

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await _registry.GetDashboardAsync(SchoolId, ct));

    [HttpPut("apprentices/{studentUserId:int}")]
    public async Task<IActionResult> Update(
        int studentUserId,
        SaveApprenticeRequest request,
        CancellationToken ct) =>
        Ok(await _registry.UpdateAsync(SchoolId, studentUserId, request, ct));

    [HttpPost("imports/excel/preview")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ExcelPreview(
        [FromForm] string importType,
        IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Selecciona un archivo Excel." });
        }

        await using var stream = file.OpenReadStream();
        return Ok(await _import.PreviewAsync(
            SchoolId,
            importType,
            file.FileName,
            stream,
            ct));
    }

    [HttpPost("imports/excel/{previewId:guid}/commit")]
    public async Task<IActionResult> ExcelCommit(Guid previewId, CancellationToken ct) =>
        Ok(await _import.CommitAsync(SchoolId, previewId, ct));

    [HttpGet("theory-exams/schedule")]
    public async Task<IActionResult> ListExamSlots(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _registry.ListExamSlotsAsync(SchoolId, from, to, ct));

    [HttpPost("theory-exams/schedule")]
    public async Task<IActionResult> CreateExamSlot(
        SaveTheoryExamSlotRequest request,
        CancellationToken ct) =>
        Ok(await _registry.SaveExamSlotAsync(SchoolId, null, request, ct));

    [HttpPut("theory-exams/schedule/{id:int}")]
    public async Task<IActionResult> UpdateExamSlot(
        int id,
        SaveTheoryExamSlotRequest request,
        CancellationToken ct) =>
        Ok(await _registry.SaveExamSlotAsync(SchoolId, id, request, ct));

    [HttpDelete("theory-exams/schedule/{id:int}")]
    public async Task<IActionResult> DeleteExamSlot(int id, CancellationToken ct)
    {
        await _registry.DeleteExamSlotAsync(SchoolId, id, ct);
        return NoContent();
    }
}
