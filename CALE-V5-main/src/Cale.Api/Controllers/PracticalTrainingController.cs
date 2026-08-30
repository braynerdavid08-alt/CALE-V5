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
        [FromQuery] int? instructorUserId,
        [FromQuery] int? vehicleId,
        CancellationToken ct) =>
        Ok(await _service.ListSchoolLessonsAsync(SchoolId, weekStart, instructorUserId, vehicleId, ct));

    [HttpGet("students")]
    public async Task<IActionResult> SchedulingStudents(CancellationToken ct) =>
        Ok(await _service.ListSchedulingStudentsAsync(SchoolId, ct));

    [HttpPost("lessons/quick-assign")]
    public async Task<IActionResult> QuickAssign(
        QuickAssignPracticalRequest request,
        CancellationToken ct) =>
        Ok(await _service.QuickAssignAsync(SchoolId, request, ct));

    [HttpPost("lessons/{id:int}/unassign")]
    public async Task<IActionResult> UnassignStudent(int id, CancellationToken ct)
    {
        await _service.UnassignStudentAsync(SchoolId, id, ct);
        return NoContent();
    }

    [HttpPost("schedule/duplicate-week")]
    public async Task<IActionResult> DuplicateWeek(
        DuplicatePracticalWeekRequest request,
        CancellationToken ct) =>
        Ok(new { created = await _service.DuplicatePreviousWeekAsync(SchoolId, request, ct) });

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

    [HttpGet("lessons/attendance")]
    public async Task<IActionResult> AttendanceLessons(CancellationToken ct) =>
        Ok(await _service.ListAttendanceLessonsAsync(SchoolId, ct));

    [HttpGet("lessons/{id:int}/attendance")]
    public async Task<IActionResult> Attendance(int id, CancellationToken ct) =>
        Ok(await _service.ListAttendanceAsync(SchoolId, id, ct));

    [HttpPost("lessons/{id:int}/attendance")]
    public async Task<IActionResult> MarkAttendance(
        int id,
        MarkAttendanceRequest request,
        CancellationToken ct)
    {
        await _service.MarkAttendanceAsync(SchoolId, id, request, ct);
        return NoContent();
    }

    [HttpPost("lessons/{id:int}/attendance/batch")]
    public async Task<IActionResult> MarkAttendanceBatch(
        int id,
        MarkAttendanceBatchRequest request,
        CancellationToken ct)
    {
        await _service.MarkAttendanceBatchAsync(SchoolId, id, request, ct);
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
