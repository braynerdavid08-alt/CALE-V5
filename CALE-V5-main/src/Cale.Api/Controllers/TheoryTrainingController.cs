using Cale.Api.Extensions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school/theory")]
public sealed class SchoolTheoryController : ControllerBase
{
    private readonly TheoryTrainingService _service;

    public SchoolTheoryController(TheoryTrainingService service) => _service = service;

    private int SchoolId => CurrentUser.GetId(User);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await _service.GetSchoolDashboardAsync(SchoolId, ct));

    [HttpGet("topics")]
    public async Task<IActionResult> ListTopics(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default) =>
        Ok(await _service.ListTopicsAsync(SchoolId, activeOnly, ct));

    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic(
        SaveTheoryTopicRequest request,
        CancellationToken ct) =>
        Ok(await _service.SaveTopicAsync(SchoolId, null, request, ct));

    [HttpPut("topics/{id:int}")]
    public async Task<IActionResult> UpdateTopic(
        int id,
        SaveTheoryTopicRequest request,
        CancellationToken ct) =>
        Ok(await _service.SaveTopicAsync(SchoolId, id, request, ct));

    [HttpGet("classrooms")]
    public async Task<IActionResult> ListClassrooms(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default) =>
        Ok(await _service.ListClassroomsAsync(SchoolId, activeOnly, ct));

    [HttpPost("classrooms")]
    public async Task<IActionResult> CreateClassroom(
        SaveTheoryClassroomRequest request,
        CancellationToken ct) =>
        Ok(await _service.SaveClassroomAsync(SchoolId, null, request, ct));

    [HttpPut("classrooms/{id:int}")]
    public async Task<IActionResult> UpdateClassroom(
        int id,
        SaveTheoryClassroomRequest request,
        CancellationToken ct) =>
        Ok(await _service.SaveClassroomAsync(SchoolId, id, request, ct));

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct) =>
        Ok(await _service.GetSettingsAsync(SchoolId, ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(
        TheorySettingsDto request,
        CancellationToken ct) =>
        Ok(await _service.UpdateSettingsAsync(SchoolId, request, ct));

    [HttpGet("schedule")]
    public async Task<IActionResult> Schedule(
        [FromQuery] DateOnly? weekStart,
        CancellationToken ct) =>
        Ok(await _service.GetWeekScheduleAsync(SchoolId, weekStart, null, ct));

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(
        CreateTheoryClassRequest request,
        CancellationToken ct) =>
        Ok(await _service.CreateSessionAsync(SchoolId, request, ct));

    [HttpPost("sessions/{id:int}/cancel")]
    public async Task<IActionResult> CancelSession(
        int id,
        [FromBody] string? reason,
        CancellationToken ct)
    {
        await _service.CancelSessionAsync(SchoolId, id, SchoolId, reason, ct);
        return NoContent();
    }

    [HttpGet("sessions/attendance")]
    public async Task<IActionResult> AttendanceSessions(CancellationToken ct) =>
        Ok(await _service.ListAttendanceSessionsAsync(SchoolId, ct));

    [HttpGet("sessions/{id:int}/attendance")]
    public async Task<IActionResult> Attendance(int id, CancellationToken ct) =>
        Ok(await _service.ListAttendanceAsync(SchoolId, id, ct));

    [HttpPost("sessions/{id:int}/attendance")]
    public async Task<IActionResult> MarkAttendance(
        int id,
        MarkAttendanceRequest request,
        CancellationToken ct)
    {
        await _service.MarkAttendanceAsync(SchoolId, id, SchoolId, request, ct);
        return NoContent();
    }

    [HttpPost("sessions/{id:int}/attendance/batch")]
    public async Task<IActionResult> MarkAttendanceBatch(
        int id,
        MarkAttendanceBatchRequest request,
        CancellationToken ct)
    {
        await _service.MarkAttendanceBatchAsync(SchoolId, id, SchoolId, request, ct);
        return NoContent();
    }

    [HttpGet("enrollments")]
    public async Task<IActionResult> Enrollments(CancellationToken ct) =>
        Ok(await _service.ListEnrollmentsAsync(SchoolId, ct));

    [HttpPut("enrollments/{id:int}")]
    public async Task<IActionResult> UpdateEnrollment(
        int id,
        UpdateEnrollmentRequest request,
        CancellationToken ct) =>
        Ok(await _service.UpdateEnrollmentAsync(SchoolId, id, request, ct));
}

[ApiController]
[Authorize(Policy = "StudentOnly")]
[Route("api/student/theory")]
public sealed class StudentTheoryController : ControllerBase
{
    private readonly TheoryTrainingService _service;

    public StudentTheoryController(TheoryTrainingService service) => _service = service;

    private int StudentId => CurrentUser.GetId(User);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await _service.GetStudentDashboardAsync(StudentId, ct));

    [HttpGet("schedule")]
    public async Task<IActionResult> Schedule(
        [FromQuery] DateOnly? weekStart,
        CancellationToken ct) =>
        Ok(await _service.GetStudentWeekScheduleAsync(StudentId, weekStart, ct));

    [HttpPost("sessions/{id:int}/reserve")]
    public async Task<IActionResult> Reserve(int id, CancellationToken ct) =>
        Ok(await _service.ReserveAsync(StudentId, id, ct));

    [HttpDelete("reservations/{id:int}")]
    public async Task<IActionResult> CancelReservation(int id, CancellationToken ct)
    {
        await _service.CancelReservationAsync(StudentId, id, ct);
        return NoContent();
    }

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(CancellationToken ct)
    {
        await _service.CheckInAsync(StudentId, ct);
        return NoContent();
    }
}
