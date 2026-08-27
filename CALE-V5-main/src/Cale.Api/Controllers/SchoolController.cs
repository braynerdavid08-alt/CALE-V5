using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school")]
public sealed class SchoolController : ControllerBase
{
    private readonly GetSchoolProfileHandler _profile;
    private readonly ListSchoolPlansHandler _plans;
    private readonly ManageSchoolPlanHandler _managePlan;
    private readonly ListSchoolMembersHandler _listMembers;
    private readonly CreateSchoolMemberHandler _createMember;
    private readonly AttachSchoolMemberHandler _attachMember;
    private readonly UpdateSchoolMemberHandler _updateMember;
    private readonly ImportSchoolMembersHandler _importMembers;

    public SchoolController(
        GetSchoolProfileHandler profile,
        ListSchoolPlansHandler plans,
        ManageSchoolPlanHandler managePlan,
        ListSchoolMembersHandler listMembers,
        CreateSchoolMemberHandler createMember,
        AttachSchoolMemberHandler attachMember,
        UpdateSchoolMemberHandler updateMember,
        ImportSchoolMembersHandler importMembers)
    {
        _profile = profile;
        _plans = plans;
        _managePlan = managePlan;
        _listMembers = listMembers;
        _createMember = createMember;
        _attachMember = attachMember;
        _updateMember = updateMember;
        _importMembers = importMembers;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<SchoolProfileDto>> Profile(
        CancellationToken ct) =>
        Ok(await _profile.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpGet("plans")]
    public ActionResult<IReadOnlyList<SchoolPlanDto>> Plans() =>
        Ok(_plans.Handle());

    /// <summary>
    /// School requests a membership plan. Admin must verify payment and activate.
    /// </summary>
    [HttpPost("plan/request")]
    public async Task<ActionResult<SchoolProfileDto>> RequestPlan(
        RequestSchoolMembershipRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.RequestMembershipAsync(
            CurrentUser.GetId(User),
            request ?? new RequestSchoolMembershipRequest(null),
            ct));

    [HttpPost("plan/proof")]
    public async Task<ActionResult<SchoolProfileDto>> SubmitProof(
        SubmitPaymentProofRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.SubmitPaymentProofAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("plan/cancel")]
    public async Task<ActionResult<SchoolProfileDto>> CancelPlan(
        CancelSchoolMembershipRequest? request,
        CancellationToken ct) =>
        Ok(await _managePlan.CancelRequestAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("plan/proof/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<object>> UploadProof(
        [FromForm] IFormFile? file,
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Selecciona el comprobante.",
                Detail = "invalid_file",
                Status = 400
            });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "El archivo debe pesar 5 MB o menos.",
                Detail = "file_too_large",
                Status = 400
            });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf"
        };
        if (!allowed.Contains(ext))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Usa jpg, png, webp o pdf.",
                Detail = "invalid_file",
                Status = 400
            });
        }

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
        var folder = Path.Combine(webRoot, "uploads", "receipts");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(folder, name);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream, ct);
        return Ok(new { url = $"/uploads/receipts/{name}" });
    }

    [HttpGet("plan/history")]
    public async Task<ActionResult<IReadOnlyList<MembershipEventDto>>> History(
        CancellationToken ct) =>
        Ok(await _managePlan.ListHistoryAsync(CurrentUser.GetId(User), ct));

    [HttpPut("billing")]
    public async Task<ActionResult<SchoolProfileDto>> UpdateBilling(
        UpdateSchoolBillingRequest request,
        CancellationToken ct) =>
        Ok(await _managePlan.UpdateBillingAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    /// <summary>
    /// Schools cannot activate their own membership.
    /// </summary>
    [HttpPut("plan")]
    [HttpPost("plan/activate")]
    public IActionResult MembershipActivationForbidden() =>
        StatusCode(
            StatusCodes.Status403Forbidden,
            new ProblemDetails
            {
                Title = "Solo el administrador puede activar o actualizar la membresía tras verificar el pago.",
                Detail = "membership_admin_only",
                Status = StatusCodes.Status403Forbidden
            });

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> Members(
        CancellationToken ct) =>
        Ok(await _listMembers.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpGet("imports/template")]
    public IActionResult ImportTemplate()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(ImportSchoolMembersHandler.TemplateCsv);
        return File(bytes, "text/csv; charset=utf-8", "cale-import-usuarios.csv");
    }

    [HttpPost("imports/preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<ImportPreviewDto>> ImportPreview(
        IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Selecciona un archivo CSV.",
                Detail = "invalid_file",
                Status = 400
            });
        }

        await using var stream = file.OpenReadStream();
        return Ok(await _importMembers.PreviewAsync(
            CurrentUser.GetId(User),
            file.FileName,
            stream,
            ct));
    }

    [HttpPost("imports/{previewId:guid}/commit")]
    public async Task<ActionResult<ImportCommitResultDto>> ImportCommit(
        Guid previewId,
        CancellationToken ct) =>
        Ok(await _importMembers.CommitAsync(
            CurrentUser.GetId(User),
            previewId,
            ct));

    [HttpPost("members")]
    public async Task<ActionResult<UserListItemDto>> CreateMember(
        CreateSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _createMember.HandleAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPost("members/attach")]
    public async Task<ActionResult<UserListItemDto>> AttachMember(
        AttachSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _attachMember.HandleAsync(
            CurrentUser.GetId(User),
            request,
            ct));

    [HttpPut("members/{id:int}")]
    public async Task<ActionResult<UserListItemDto>> UpdateMember(
        int id,
        UpdateSchoolMemberRequest request,
        CancellationToken ct) =>
        Ok(await _updateMember.HandleAsync(
            CurrentUser.GetId(User),
            id,
            request,
            ct));

    // Activar/desactivar y quitar miembros: solo administrador (Usuarios / Escuelas).
}
