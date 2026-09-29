using System.Collections.Concurrent;
using System.Security.Cryptography;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;

namespace Cale.Api.Services.Play;

public sealed record DuelPlayerDto(
    int UserId,
    string Name,
    int Answered,
    int Correct,
    bool Finished,
    int? TimeSeconds);

public sealed record DuelStateDto(
    string Code,
    string Status,
    DuelPlayerDto Me,
    DuelPlayerDto? Opponent,
    IReadOnlyList<PlayQuestionDto> Questions,
    IReadOnlyList<AnsweredQuestionDto> MyAnswers,
    string? Result,
    int TimeLimitSeconds,
    int? SecondsLeft,
    IReadOnlyList<BadgeDto> NewBadges);

public sealed record DuelAnswerResultDto(
    bool Correct,
    int? CorrectOptionId,
    string? Explanation,
    DuelStateDto State);

public sealed record DuelJoinRequest(string Code);

public sealed record DuelQuestion(PlayQuestionDto Question, int? CorrectOptionId, string? Explanation);

/// <summary>
/// In-memory 1v1 duels (single API instance). Clients poll the state; results are persisted
/// as <see cref="GameResult"/> rows when both players finish or time runs out.
/// </summary>
public sealed class DuelService
{
    public const int QuestionCount = 7;
    public const int TimeLimitSeconds = 180;
    private static readonly TimeSpan WaitingTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FinishedTtl = TimeSpan.FromMinutes(30);
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly ConcurrentDictionary<string, Duel> _duels = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DuelService> _logger;

    public DuelService(IServiceScopeFactory scopes, ILogger<DuelService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public DuelStateDto Create(int userId, string name, IReadOnlyList<DuelQuestion> questions)
    {
        Cleanup();
        if (questions.Count == 0)
        {
            throw new DomainException("There are no questions available yet.", 404, "no_questions");
        }

        foreach (var open in _duels.Values.Where(d => d.Host.UserId == userId && d.Status == "waiting"))
        {
            _duels.TryRemove(open.Code, out _);
        }

        string code;
        do
        {
            code = new string(Enumerable.Range(0, 6)
                .Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)])
                .ToArray());
        }
        while (_duels.ContainsKey(code));

        var duel = new Duel(code, new Player(userId, name), questions, DateTime.UtcNow);
        _duels[code] = duel;
        return Snapshot(duel, userId);
    }

    public DuelStateDto Join(int userId, string name, string code)
    {
        var duel = Find(code);
        lock (duel)
        {
            if (duel.Host.UserId == userId || duel.Guest?.UserId == userId)
            {
                return Snapshot(duel, userId);
            }

            if (duel.Status != "waiting" || duel.Guest is not null)
            {
                throw new DomainException("This duel already has two players.", 409, "duel_full");
            }

            duel.Guest = new Player(userId, name);
            duel.Status = "playing";
            duel.StartedAt = DateTime.UtcNow;
            return Snapshot(duel, userId);
        }
    }

    public async Task<DuelStateDto> GetAsync(int userId, string code, CancellationToken ct)
    {
        var duel = Find(code);
        await FinishIfTimedOutAsync(duel, ct);
        lock (duel)
        {
            EnsurePlayer(duel, userId);
            return Snapshot(duel, userId);
        }
    }

    public async Task<DuelAnswerResultDto> AnswerAsync(
        int userId,
        string code,
        PlayAnswerRequest request,
        CancellationToken ct)
    {
        var duel = Find(code);
        await FinishIfTimedOutAsync(duel, ct);

        bool correct;
        DuelQuestion question;
        bool shouldPersist;
        lock (duel)
        {
            var player = EnsurePlayer(duel, userId);
            if (duel.Status != "playing")
            {
                throw new DomainException("The duel is not in progress.", 409, "duel_not_playing");
            }

            question = duel.Questions.FirstOrDefault(q => q.Question.Id == request.QuestionId)
                ?? throw new DomainException("Question is not part of this duel.", 400, "duel_question_invalid");
            if (player.Answers.ContainsKey(request.QuestionId))
            {
                throw new DomainException("Question already answered.", 409, "duel_already_answered");
            }

            correct = question.CorrectOptionId == request.OptionId;
            player.Answers[request.QuestionId] = (request.OptionId, correct);
            if (player.Answers.Count >= duel.Questions.Count)
            {
                player.FinishedAt = DateTime.UtcNow;
            }

            shouldPersist = duel.Host.FinishedAt is not null && duel.Guest?.FinishedAt is not null;
            if (shouldPersist)
            {
                duel.Status = "finished";
                duel.FinishedAt = DateTime.UtcNow;
            }
        }

        if (shouldPersist)
        {
            await PersistAsync(duel, ct);
        }

        lock (duel)
        {
            return new DuelAnswerResultDto(
                correct,
                question.CorrectOptionId,
                question.Explanation,
                Snapshot(duel, userId));
        }
    }

    public void Cancel(int userId, string code)
    {
        if (_duels.TryGetValue(code, out var duel) && duel.Host.UserId == userId && duel.Status == "waiting")
        {
            _duels.TryRemove(code, out _);
        }
    }

    private async Task FinishIfTimedOutAsync(Duel duel, CancellationToken ct)
    {
        var persist = false;
        lock (duel)
        {
            if (duel.Status == "playing" && duel.StartedAt is { } started
                && DateTime.UtcNow - started > TimeSpan.FromSeconds(TimeLimitSeconds + 5))
            {
                duel.Status = "finished";
                duel.FinishedAt = DateTime.UtcNow;
                persist = true;
            }
        }

        if (persist)
        {
            await PersistAsync(duel, ct);
        }
    }

    private async Task PersistAsync(Duel duel, CancellationToken ct)
    {
        if (duel.Persisted || duel.Guest is null)
        {
            return;
        }

        duel.Persisted = true;
        var outcome = Outcome(duel);
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            foreach (var (player, opponent) in new[] { (duel.Host, duel.Guest), (duel.Guest, duel.Host) })
            {
                db.Set<GameResult>().Add(new GameResult
                {
                    UserId = player.UserId,
                    Game = GameKinds.Duel,
                    Score = player.CorrectCount,
                    Correct = player.CorrectCount,
                    Total = duel.Questions.Count,
                    Won = outcome == player.UserId,
                    OpponentId = opponent.UserId,
                    PlayedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync(ct);

            var play = scope.ServiceProvider.GetRequiredService<PlayService>();
            foreach (var player in new[] { duel.Host, duel.Guest })
            {
                player.NewBadges = await play.CheckAchievementsAsync(player.UserId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist duel {Code}.", duel.Code);
        }
    }

    /// <summary>Winner user id, 0 for a draw.</summary>
    private static int Outcome(Duel duel)
    {
        if (duel.Guest is null)
        {
            return 0;
        }

        var a = duel.Host;
        var b = duel.Guest;
        if (a.CorrectCount != b.CorrectCount)
        {
            return a.CorrectCount > b.CorrectCount ? a.UserId : b.UserId;
        }

        var ta = a.ElapsedSeconds(duel) ?? int.MaxValue;
        var tb = b.ElapsedSeconds(duel) ?? int.MaxValue;
        if (ta == tb)
        {
            return 0;
        }

        return ta < tb ? a.UserId : b.UserId;
    }

    private DuelStateDto Snapshot(Duel duel, int userId)
    {
        var me = duel.Host.UserId == userId ? duel.Host : duel.Guest!;
        var other = duel.Host.UserId == userId ? duel.Guest : duel.Host;
        var showQuestions = duel.Status != "waiting";

        var myAnswers = me.Answers
            .Select(kv =>
            {
                var q = duel.Questions.First(x => x.Question.Id == kv.Key);
                return new AnsweredQuestionDto(kv.Key, kv.Value.OptionId, kv.Value.Correct, q.CorrectOptionId, q.Explanation);
            })
            .ToList();

        string? result = null;
        if (duel.Status == "finished")
        {
            var winner = Outcome(duel);
            result = winner == 0 ? "draw" : winner == userId ? "won" : "lost";
        }

        int? secondsLeft = duel.Status == "playing" && duel.StartedAt is { } s
            ? Math.Max(0, TimeLimitSeconds - (int)(DateTime.UtcNow - s).TotalSeconds)
            : null;

        var badges = duel.Status == "finished" ? me.NewBadges : [];

        return new DuelStateDto(
            duel.Code,
            duel.Status,
            ToPlayer(me, duel),
            other is null ? null : ToPlayer(other, duel),
            showQuestions ? duel.Questions.Select(q => q.Question).ToList() : [],
            myAnswers,
            result,
            TimeLimitSeconds,
            secondsLeft,
            badges);
    }

    private static DuelPlayerDto ToPlayer(Player p, Duel duel) =>
        new(p.UserId, p.Name, p.Answers.Count, p.CorrectCount, p.FinishedAt is not null, p.ElapsedSeconds(duel));

    private Duel Find(string code)
    {
        Cleanup();
        if (string.IsNullOrWhiteSpace(code) || !_duels.TryGetValue(code.Trim(), out var duel))
        {
            throw new DomainException("Duel not found or expired.", 404, "duel_not_found");
        }

        return duel;
    }

    private static Player EnsurePlayer(Duel duel, int userId)
    {
        if (duel.Host.UserId == userId)
        {
            return duel.Host;
        }

        if (duel.Guest?.UserId == userId)
        {
            return duel.Guest;
        }

        throw new ForbiddenException("You are not part of this duel.");
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var duel in _duels.Values)
        {
            var expired = duel.Status switch
            {
                "waiting" => now - duel.CreatedAt > WaitingTtl,
                "finished" => now - (duel.FinishedAt ?? now) > FinishedTtl,
                _ => duel.StartedAt is { } s && now - s > TimeSpan.FromSeconds(TimeLimitSeconds) + FinishedTtl
            };
            if (expired)
            {
                _duels.TryRemove(duel.Code, out _);
            }
        }
    }

    private sealed class Duel(string code, Player host, IReadOnlyList<DuelQuestion> questions, DateTime createdAt)
    {
        public string Code { get; } = code;
        public Player Host { get; } = host;
        public Player? Guest { get; set; }
        public IReadOnlyList<DuelQuestion> Questions { get; } = questions;
        public DateTime CreatedAt { get; } = createdAt;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string Status { get; set; } = "waiting";
        public bool Persisted { get; set; }
    }

    private sealed class Player(int userId, string name)
    {
        public int UserId { get; } = userId;
        public string Name { get; } = name;
        public Dictionary<int, (int OptionId, bool Correct)> Answers { get; } = [];
        public DateTime? FinishedAt { get; set; }
        public IReadOnlyList<BadgeDto> NewBadges { get; set; } = [];
        public int CorrectCount => Answers.Values.Count(a => a.Correct);

        public int? ElapsedSeconds(Duel duel) =>
            FinishedAt is { } f && duel.StartedAt is { } s ? (int)(f - s).TotalSeconds : null;
    }
}
