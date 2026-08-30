using Cale.Api.Extensions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school/practical")]
public sealed class SchoolPracticalController : ControllerBase
{
    private readonly PracticalTrainingService _service;

    public SchoolPracticalController(PracticalTrainingService service) => _service = service;

    private int SchoolId => CurrentUser.GetId(User);

    [HttpGet("vehicles")]
    public async Task<IActionResult> ListVehicles(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default) =>
        Ok(await _service.ListVehiclesAsync(SchoolId, activeOnly, ct));

    [HttpPost("vehicles")]
    public async Task<IActionResult> CreateVehicle(
        SavePracticalVehicleRequest request,
        CancellationToken ct) =>
        Ok(await _service.SaveVehicleAsync(SchoolId, null, request, ct));

    [HttpPut("vehicles/{id:int}")]
    public async Task<IActionResult> UpdateVehicle(
        int id,
        SavePracticalVehicleRequest request,
        CancellationToken ct) =>
        Ok(await _service.SaveVehicleAsync(SchoolId, id, request, ct));

    [HttpGet("lessons")]
    public async Task<IActionResult> Lessons(
        [FromQuery] DateOnly? weekStart,
        CancellationToken ct) =>
        Ok(await _service.ListSchoolLessonsAsync(SchoolId, weekStart, ct));

    [HttpPost("lessons")]
    public async Task<IActionResult> CreateLesson(
        CreatePracticalLessonRequest request,
        CancellationToken ct) =>
        Ok(await _service.CreateLessonAsync(SchoolId, request, ct));

    [HttpPost("lessons/{id:int}/cancel")]
    public async Task<IActionResult> CancelLesson(int id, CancellationToken ct)
    {
        await _service.CancelLessonAsync(SchoolId, id, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = "StudentOnly")]
[Route("api/student/practical")]
public sealed class StudentPracticalController : ControllerBase
{
    private readonly PracticalTrainingService _service;

    public StudentPracticalController(PracticalTrainingService service) => _service = service;

    private int StudentId => CurrentUser.GetId(User);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await _service.GetStudentDashboardAsync(StudentId, ct));

    [HttpPost("lessons/{id:int}/reserve")]
    public async Task<IActionResult> Reserve(int id, CancellationToken ct) =>
        Ok(await _service.ReserveAsync(StudentId, id, ct));

    [HttpDelete("reservations/{id:int}")]
    public async Task<IActionResult> CancelReservation(int id, CancellationToken ct)
    {
        await _service.CancelReservationAsync(StudentId, id, ct);
        return NoContent();
    }
}
