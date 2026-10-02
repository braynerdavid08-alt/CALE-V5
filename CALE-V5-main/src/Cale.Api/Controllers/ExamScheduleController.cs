using Cale.Api.Extensions;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Cale.Api.Controllers;

/// <summary>Recurring exam schedule, date exceptions, capacity and bookings of the authenticated school.</summary>
[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school/theory-exams")]
public sealed class SchoolExamScheduleController : ControllerBase
{
    private readonly TheoryExamScheduleService _service;
    private readonly SchoolAuditService _audit;

    public SchoolExamScheduleController(TheoryExamScheduleService service, SchoolAuditService audit)
    {
        _service = service;
        _audit = audit;
    }

    private int SchoolId => CurrentUser.GetId(User);

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates(CancellationToken ct) =>
        Ok(await _service.ListTemplatesAsync(SchoolId, ct));

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplates(CreateExamTemplatesRequest request, CancellationToken ct) =>
        Ok(await _service.CreateTemplatesAsync(SchoolId, SchoolId, request, ct));

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> UpdateTemplate(int id, UpdateExamTemplateRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateTemplateAsync(SchoolId, SchoolId, id, request, ct));

    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        await _service.DeleteTemplateAsync(SchoolId, SchoolId, id, ct);
        return NoContent();
    }

    [HttpGet("week")]
    public async Task<IActionResult> Week([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _service.GetWeekAsync(SchoolId, from, to, ct));

    [HttpPut("slots")]
    public async Task<IActionResult> SaveSlot(SaveExamSlotOverrideRequest request, CancellationToken ct) =>
        Ok(await _service.SaveSlotOverrideAsync(SchoolId, SchoolId, request, ct));

    [HttpDelete("overrides/{id:int}")]
    public async Task<IActionResult> DeleteOverride(int id, CancellationToken ct) =>
        Ok(await _service.DeleteOverrideAsync(SchoolId, SchoolId, id, ct));

    [HttpPut("days/{date}/close")]
    public async Task<IActionResult> CloseDay(
        DateOnly date,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CloseExamDayRequest? request,
        CancellationToken ct) =>
        Ok(await _service.CloseDayAsync(SchoolId, SchoolId, date, request ?? new CloseExamDayRequest(null), ct));

    [HttpDelete("days/{date}/close")]
    public async Task<IActionResult> ReopenDay(DateOnly date, CancellationToken ct) =>
        Ok(await _service.ReopenDayAsync(SchoolId, SchoolId, date, ct));

    [HttpPost("bookings")]
    public async Task<IActionResult> Book(SchoolBookExamRequest request, CancellationToken ct) =>
        Ok(await _service.BookAsSchoolAsync(SchoolId, SchoolId, request, ct));

    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelExamBookingRequest? request,
        CancellationToken ct) =>
        Ok(await _service.CancelAsSchoolAsync(SchoolId, SchoolId, id, request?.Reason, ct));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(
        [FromQuery] int? studentUserId,
        [FromQuery] string? area,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _audit.ListAsync(SchoolId, studentUserId, area, take, ct));
}

/// <summary>Manual hours and the weekly agenda of the authenticated school.</summary>
[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school")]
public sealed class SchoolHoursAgendaController : ControllerBase
{
    private readonly StudentHoursService _hours;
    private readonly SchoolAgendaService _agenda;

    public SchoolHoursAgendaController(StudentHoursService hours, SchoolAgendaService agenda)
    {
        _hours = hours;
        _agenda = agenda;
    }

    private int SchoolId => CurrentUser.GetId(User);

    [HttpGet("apprentices/{studentUserId:int}/hours")]
    public async Task<IActionResult> GetHours(int studentUserId, CancellationToken ct) =>
        Ok(await _hours.GetAsync(SchoolId, studentUserId, ct));

    [HttpPost("apprentices/{studentUserId:int}/hours")]
    public async Task<IActionResult> AdjustHours(
        int studentUserId,
        AdjustStudentHoursRequest request,
        CancellationToken ct) =>
        Ok(await _hours.AdjustAsync(SchoolId, SchoolId, studentUserId, request, ct));

    [HttpGet("agenda")]
    public async Task<IActionResult> Agenda([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _agenda.GetAsync(SchoolId, from, to, ct));
}

/// <summary>Theory-exam self-booking for the authenticated student (school resolved from the account).</summary>
[ApiController]
[Authorize(Policy = "StudentOnly")]
[Route("api/student/theory/exams")]
public sealed class StudentExamBookingController : ControllerBase
{
    private readonly TheoryExamScheduleService _service;

    public StudentExamBookingController(TheoryExamScheduleService service) => _service = service;

    private int StudentId => CurrentUser.GetId(User);

    [HttpGet("availability")]
    public async Task<IActionResult> Availability(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _service.GetStudentAvailabilityAsync(StudentId, from, to, ct));

    [HttpPost("book")]
    public async Task<IActionResult> Book(StudentBookExamRequest request, CancellationToken ct) =>
        Ok(await _service.BookAsStudentAsync(StudentId, request, ct));

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok(await _service.ListStudentBookingsAsync(StudentId, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _service.CancelAsStudentAsync(StudentId, id, ct);
        return NoContent();
    }
}
