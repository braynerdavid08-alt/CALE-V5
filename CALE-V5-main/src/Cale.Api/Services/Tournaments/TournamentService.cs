using System.Text.Json;
using Cale.Api.Services.Play;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Tournaments;

public sealed partial class TournamentService
{
    public const int MinCapacity = 4;
    public const int MaxCapacity = 16;
    public const int DefaultQuestions = 10;
    public const int DisconnectGraceSeconds = 60;
    private static readonly int[] AllowedCapacities = [4, 8, 16];
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly PlayService _play;
    private readonly ITournamentBroadcaster _broadcast;
    private readonly ILogger<TournamentService> _logger;

    public TournamentService(
        CaleDbContext db,
        IClock clock,
        PlayService play,
        ITournamentBroadcaster broadcast,
        ILogger<TournamentService> logger)
    {
        _db = db;
        _clock = clock;
        _play = play;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<TournamentListItemDto> CreateAsync(
        int teacherUserId,
        CreateTournamentRequest request,
        CancellationToken ct)
    {
        var teacher = await RequireUserAsync(teacherUserId, ct);
        if (teacher.Role != Roles.Teacher)
        {
            throw new DomainException("Solo un profesor puede crear torneos.", 403, "forbidden");
        }

        if (teacher.SchoolId is not int schoolId)
        {
            throw new DomainException("El profesor debe pertenecer a una escuela.", 400, "no_school");
        }

        var name = (request.Name ?? "").Trim();
        if (name.Length is < 3 or > 120)
        {
            throw new DomainException("El nombre del torneo debe tener entre 3 y 120 caracteres.", 400, "invalid_name");
        }

        var capacity = AllowedCapacities.Contains(request.Capacity)
            ? request.Capacity
            : throw new DomainException("La capacidad debe ser 4, 8 o 16.", 400, "invalid_capacity");

        var seconds = Math.Clamp(request.SecondsPerQuestion is > 0 ? request.SecondsPerQuestion : 30, 10, 120);
        var mode = string.Equals(request.QuestionMode, TournamentQuestionModes.Manual, StringComparison.OrdinalIgnoreCase)
            ? TournamentQuestionModes.Manual
            : TournamentQuestionModes.Random;

        if (mode == TournamentQuestionModes.Manual
            && (request.ManualQuestionIds is null || request.ManualQuestionIds.Count < DefaultQuestions))
        {
            throw new DomainException(
                $"Selecciona al menos {DefaultQuestions} preguntas.",
                400,
                "questions_required");
        }

        var now = _clock.UtcNow;
        var tournament = new Tournament
        {
            SchoolUserId = schoolId,
            CreatedByUserId = teacherUserId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ScheduledAt = request.ScheduledAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc)
                : request.ScheduledAt.ToUniversalTime(),
            Capacity = capacity,
            QuestionsPerMatch = DefaultQuestions,
            SecondsPerQuestion = seconds,
            QuestionMode = mode,
            CategoryFilterJson = request.Categories is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Categories, JsonOpts)
                : null,
            ManualQuestionIdsJson = mode == TournamentQuestionModes.Manual
                ? JsonSerializer.Serialize(request.ManualQuestionIds!.Distinct().Take(40).ToList(), JsonOpts)
                : null,
            Status = TournamentStatuses.RegistrationOpen,
            BracketSize = 0,
            CurrentRoundNumber = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Set<Tournament>().Add(tournament);
        await _db.SaveChangesAsync(ct);
        return await ToListItemAsync(tournament, teacherUserId, ct);
    }

    public async Task<IReadOnlyList<TournamentListItemDto>> ListForActorAsync(
        int actorUserId,
        string role,
        CancellationToken ct)
    {
        var actor = await RequireUserAsync(actorUserId, ct);
        IQueryable<Tournament> query = _db.Set<Tournament>().AsNoTracking();

        if (role is Roles.Teacher or Roles.Student or Roles.School)
        {
            var schoolId = role == Roles.School ? actor.Id : actor.SchoolId;
            if (schoolId is not int sid)
            {
                return [];
            }

            query = query.Where(t => t.SchoolUserId == sid);
        }
        else if (role != Roles.Admin)
        {
            return [];
        }

        var rows = await query
            .OrderByDescending(t => t.ScheduledAt)
            .Take(100)
            .ToListAsync(ct);

        var result = new List<TournamentListItemDto>(rows.Count);
        foreach (var t in rows)
        {
            result.Add(await ToListItemAsync(t, actorUserId, ct));
        }

        return result;
    }

    public async Task<TournamentLobbyDto> GetLobbyAsync(int tournamentId, int actorUserId, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct);
        await EnsureCanViewAsync(t, actorUserId, ct);
        return await ToLobbyAsync(t, actorUserId, ct);
    }

    public async Task<TournamentLobbyDto> JoinAsync(int tournamentId, int studentUserId, CancellationToken ct)
    {
        var student = await RequireUserAsync(studentUserId, ct);
        if (student.Role != Roles.Student)
        {
            throw new DomainException("Solo estudiantes pueden inscribirse.", 403, "forbidden");
        }

        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        if (t.Status != TournamentStatuses.RegistrationOpen)
        {
            throw new DomainException("Las inscripciones están cerradas.", 400, "registration_closed");
        }

        if (student.SchoolId != t.SchoolUserId)
        {
            throw new DomainException("Solo puedes unirte a torneos de tu escuela.", 403, "wrong_school");
        }

        if (t.Participants.Any(p => p.UserId == studentUserId))
        {
            return await ToLobbyAsync(t, studentUserId, ct);
        }

        if (t.Participants.Count >= t.Capacity)
        {
            throw new DomainException("El torneo ya está completo.", 400, "full");
        }

        var participant = new TournamentParticipant
        {
            TournamentId = t.Id,
            UserId = studentUserId,
            DisplayNameSnapshot = PublicName(student.Name),
            JoinedAt = _clock.UtcNow,
            IsConnected = false
        };
        t.Participants.Add(participant);
        t.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        var dto = new TournamentParticipantDto(
            participant.UserId,
            participant.DisplayNameSnapshot,
            false,
            false,
            null);
        await _broadcast.ParticipantJoined(t.Id, dto, ct);
        var lobby = await ToLobbyAsync(t, studentUserId, ct);
        await _broadcast.LobbyUpdated(t.Id, lobby, ct);
        return lobby;
    }

    public async Task CloseRegistrationAsync(int tournamentId, int actorUserId, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        await EnsureTeacherOrSchoolAsync(t, actorUserId, ct);
        if (t.Status is not (TournamentStatuses.RegistrationOpen or TournamentStatuses.RegistrationClosed))
        {
            throw new DomainException("No se pueden cerrar inscripciones en este estado.", 400, "invalid_status");
        }

        t.Status = TournamentStatuses.RegistrationClosed;
        t.UpdatedAt = _clock.UtcNow;
        t.RowVersion++;
        await _db.SaveChangesAsync(ct);
        await _broadcast.LobbyUpdated(t.Id, await ToLobbyAsync(t, actorUserId, ct), ct);
    }

    public async Task CancelAsync(int tournamentId, int actorUserId, CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        await EnsureTeacherOrSchoolAsync(t, actorUserId, ct);
        if (t.Status == TournamentStatuses.Finished)
        {
            throw new DomainException("El torneo ya finalizó.", 400, "already_finished");
        }

        t.Status = TournamentStatuses.Cancelled;
        t.UpdatedAt = _clock.UtcNow;
        t.RowVersion++;
        await _db.SaveChangesAsync(ct);
        await _broadcast.LobbyUpdated(t.Id, await ToLobbyAsync(t, actorUserId, ct), ct);
        await _broadcast.TournamentFinished(t.Id, ct);
    }

    public async Task SetConnectionAsync(
        int tournamentId,
        int userId,
        string connectionId,
        bool connected,
        CancellationToken ct)
    {
        var t = await LoadTournamentAsync(tournamentId, ct, tracking: true);
        var p = t.Participants.FirstOrDefault(x => x.UserId == userId);
        if (p is null)
        {
            // Organizer / spectator may join hub without being a participant.
            return;
        }

        p.IsConnected = connected;
        p.ConnectionId = connected ? connectionId : null;
        p.DisconnectedAt = connected ? null : _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (connected)
        {
            await _broadcast.ParticipantReconnected(tournamentId, userId, ct);
        }
        else
        {
            await _broadcast.ParticipantDisconnected(tournamentId, userId, ct);
            await TryResolveWalkoverOnDisconnectAsync(t, userId, ct);
        }

        await _broadcast.LobbyUpdated(tournamentId, await ToLobbyAsync(t, userId, ct), ct);
    }

    private async Task<Tournament> LoadTournamentAsync(
        int tournamentId,
        CancellationToken ct,
        bool tracking = false)
    {
        var query = tracking
            ? _db.Set<Tournament>().AsQueryable()
            : _db.Set<Tournament>().AsNoTracking();

        var t = await query
            .Include(x => x.Participants)
            .Include(x => x.Rounds)
                .ThenInclude(r => r.Matches)
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
            ?? throw new DomainException("Torneo no encontrado.", 404, "not_found");
        return t;
    }

    private async Task<User> RequireUserAsync(int userId, CancellationToken ct) =>
        await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new DomainException("Usuario no encontrado.", 404, "user_not_found");

    private async Task EnsureCanViewAsync(Tournament t, int actorUserId, CancellationToken ct)
    {
        var actor = await RequireUserAsync(actorUserId, ct);
        if (actor.Role == Roles.Admin)
        {
            return;
        }

        if (actor.Role == Roles.School && actor.Id == t.SchoolUserId)
        {
            return;
        }

        if (actor.SchoolId == t.SchoolUserId)
        {
            return;
        }

        throw new DomainException("No tienes acceso a este torneo.", 403, "forbidden");
    }

    private async Task EnsureTeacherOrSchoolAsync(Tournament t, int actorUserId, CancellationToken ct)
    {
        var actor = await RequireUserAsync(actorUserId, ct);
        if (actor.Role == Roles.Admin)
        {
            return;
        }

        if (actor.Role == Roles.School && actor.Id == t.SchoolUserId)
        {
            return;
        }

        if (actor.Role == Roles.Teacher
            && actor.SchoolId == t.SchoolUserId
            && (t.CreatedByUserId == actorUserId || actor.SchoolId == t.SchoolUserId))
        {
            return;
        }

        throw new DomainException("No puedes administrar este torneo.", 403, "forbidden");
    }

    private static string PublicName(string name)
    {
        var trimmed = (name ?? "").Trim();
        return string.IsNullOrEmpty(trimmed) ? "Estudiante" : trimmed;
    }

    private async Task<string> OrganizerNameAsync(int userId, CancellationToken ct)
    {
        var name = await _db.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct);
        return PublicName(name ?? "");
    }

    private async Task<TournamentListItemDto> ToListItemAsync(
        Tournament t,
        int actorUserId,
        CancellationToken ct)
    {
        var registered = t.Participants?.Count
            ?? await _db.Set<TournamentParticipant>().CountAsync(p => p.TournamentId == t.Id, ct);
        var connected = t.Participants?.Count(p => p.IsConnected)
            ?? await _db.Set<TournamentParticipant>().CountAsync(p => p.TournamentId == t.Id && p.IsConnected, ct);
        var amI = t.Participants?.Any(p => p.UserId == actorUserId)
            ?? await _db.Set<TournamentParticipant>().AnyAsync(p => p.TournamentId == t.Id && p.UserId == actorUserId, ct);
        var myMatchId = await FindActiveMatchIdAsync(t.Id, actorUserId, ct);
        return new TournamentListItemDto(
            t.Id,
            t.Name,
            t.Description,
            t.ScheduledAt,
            t.Status,
            t.Capacity,
            registered,
            connected,
            await OrganizerNameAsync(t.CreatedByUserId, ct),
            amI,
            myMatchId);
    }

    private async Task<TournamentLobbyDto> ToLobbyAsync(Tournament t, int actorUserId, CancellationToken ct)
    {
        var participants = t.Participants
            .OrderBy(p => p.JoinedAt)
            .Select(p => new TournamentParticipantDto(
                p.UserId,
                p.DisplayNameSnapshot,
                p.IsConnected,
                p.IsEliminated,
                p.BracketSlot))
            .ToList();

        string? championName = null;
        if (t.ChampionUserId is int champ)
        {
            championName = participants.FirstOrDefault(p => p.UserId == champ)?.Name
                ?? await OrganizerNameAsync(champ, ct);
        }

        var amI = participants.Any(p => p.UserId == actorUserId);
        return new TournamentLobbyDto(
            t.Id,
            t.Name,
            t.Description,
            t.ScheduledAt,
            t.Status,
            t.Capacity,
            t.BracketSize,
            t.CurrentRoundNumber,
            await OrganizerNameAsync(t.CreatedByUserId, ct),
            t.CreatedByUserId,
            participants,
            amI,
            t.Status == TournamentStatuses.RegistrationOpen && !amI && participants.Count < t.Capacity,
            await FindActiveMatchIdAsync(t.Id, actorUserId, ct),
            t.ChampionUserId,
            championName);
    }

    private async Task<int?> FindActiveMatchIdAsync(int tournamentId, int userId, CancellationToken ct)
    {
        return await _db.Set<TournamentMatch>().AsNoTracking()
            .Where(m => m.TournamentId == tournamentId
                && m.Status == TournamentMatchStatuses.Active
                && (m.Player1UserId == userId || m.Player2UserId == userId))
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);
    }
}
