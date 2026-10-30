using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Play;

public sealed record SignImageMatch(string Code, string Name, string ImageUrl, int QuestionId, string Answer);

public sealed record SignImageUnmatched(int QuestionId, string ExamName, string Answer, string ImageUrl);

public sealed record SignImageReport(
    IReadOnlyList<string> Exams,
    IReadOnlyList<SignImageMatch> Matched,
    IReadOnlyList<SignImageUnmatched> Unmatched,
    IReadOnlyList<string> MissingCodes);

/// <summary>
/// Real sign pictures come from sign exams ("Examen Señales …"), same discovery rules as
/// Señal relámpago: name contains "señal", admin-owned exams or questions in official banks.
/// Each question image is paired with a catalog sign through its correct answer.
/// Requests for the generic /signals/{code}.svg drawings are redirected to those pictures.
/// </summary>
public sealed partial class SignImageOverrides
{
    /// <summary>Short so admin edits and new exam photos show up quickly in the editor tray.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    /// <summary>Same fragments as <c>PlayService</c> Señal relámpago exam discovery.</summary>
    private static readonly string[] ExamKeywords = ["señal", "senal"];

    private readonly IServiceScopeFactory _scopes;
    private readonly PlayContent _content;
    private readonly ILogger<SignImageOverrides> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SignImageReport? _report;
    private Dictionary<string, string> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _loadedAt = DateTime.MinValue;

    public SignImageOverrides(IServiceScopeFactory scopes, PlayContent content, ILogger<SignImageOverrides> logger)
    {
        _scopes = scopes;
        _content = content;
        _logger = logger;
    }

    public async Task<string?> ImageForAsync(string code, CancellationToken ct)
    {
        await EnsureFreshAsync(false, ct);
        return _byCode.GetValueOrDefault(code);
    }

    public async Task<SignImageReport> ReportAsync(bool refresh, CancellationToken ct)
    {
        await EnsureFreshAsync(refresh, ct);
        return _report!;
    }

    private async Task EnsureFreshAsync(bool force, CancellationToken ct)
    {
        if (!force && _report is not null && DateTime.UtcNow - _loadedAt < Ttl)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (!force && _report is not null && DateTime.UtcNow - _loadedAt < Ttl)
            {
                return;
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
                var report = await BuildAsync(db, _content.Signs, ct);
                _byCode = report.Matched.ToDictionary(m => m.Code, m => m.ImageUrl, StringComparer.OrdinalIgnoreCase);
                _report = report;
                _logger.LogInformation(
                    "Sign images: {Matched} matched, {Unmatched} unmatched questions, {Missing} signs without picture.",
                    report.Matched.Count, report.Unmatched.Count, report.MissingCodes.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not load sign images from the sign exams.");
                _report ??= new SignImageReport([], [], [], []);
            }

            _loadedAt = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task<SignImageReport> BuildAsync(CaleDbContext db, IReadOnlyList<SignDto> signs, CancellationToken ct)
    {
        var admins = (await db.Set<User>().AsNoTracking()
                .Select(u => new { u.Id, u.Role })
                .ToListAsync(ct))
            .Where(u => Roles.Normalize(u.Role) == Roles.Admin)
            .Select(u => u.Id)
            .ToHashSet();
        var officialBanks = (await db.Set<Bank>().AsNoTracking()
                .Where(b => b.IsActive && b.CreatedById == null)
                .Select(b => b.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var exams = (await db.Set<Exam>().AsNoTracking()
                .Where(e => e.IsActive)
                .Select(e => new { e.Id, e.Name, e.CreatedById, e.BankId })
                .ToListAsync(ct))
            .Where(e => ExamKeywords.Any(k => e.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (exams.Count == 0)
        {
            return new SignImageReport([], [], [], signs.Select(s => s.Code).ToList());
        }

        var examIds = exams.Select(e => e.Id).ToList();
        var links = await db.Set<ExamQuestion>().AsNoTracking()
            .Where(l => examIds.Contains(l.ExamId))
            .Select(l => new { l.ExamId, l.QuestionId })
            .ToListAsync(ct);
        var listed = links
            .GroupBy(l => l.ExamId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.QuestionId).ToHashSet());
        var pickedIds = links.Select(l => l.QuestionId).Distinct().ToList();
        var bankFallbackIds = exams
            .Where(e => e.BankId is not null && !listed.ContainsKey(e.Id))
            .Select(e => e.BankId!.Value)
            .Distinct()
            .ToList();

        var questions = await db.Set<Question>().AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.IsActive && (pickedIds.Contains(q.Id) || bankFallbackIds.Contains(q.BankId)))
            .ToListAsync(ct);

        var matched = new Dictionary<string, SignImageMatch>(StringComparer.OrdinalIgnoreCase);
        var unmatched = new List<SignImageUnmatched>();
        var usedExamNames = new List<string>();

        foreach (var exam in exams)
        {
            var adminOwned = admins.Contains(exam.CreatedById);
            IEnumerable<Question> own = listed.TryGetValue(exam.Id, out var set)
                ? questions.Where(q => set.Contains(q.Id))
                : exam.BankId is int bankId
                    ? questions.Where(q => q.BankId == bankId)
                    : [];
            var allowed = (adminOwned ? own : own.Where(q => officialBanks.Contains(q.BankId))).ToList();
            if (allowed.Count == 0)
            {
                continue;
            }

            usedExamNames.Add(exam.Name);
            var family = FamilyPrefix(exam.Name);
            var candidates = signs
                .Where(s => family is null || s.Code.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var q in allowed)
            {
                var correct = q.Options.FirstOrDefault(o => o.IsCorrect);
                if (correct is null)
                {
                    continue;
                }

                var (image, answer) = !string.IsNullOrWhiteSpace(q.ImageUrl)
                    ? (q.ImageUrl!, correct.Text)
                    : (correct.ImageUrl, q.Text);
                if (!IsUsableImage(image))
                {
                    continue;
                }

                var codes = Match(answer, candidates);
                if (codes.Count == 0)
                {
                    unmatched.Add(new SignImageUnmatched(q.Id, exam.Name, answer, image));
                    continue;
                }

                foreach (var sign in codes)
                {
                    matched.TryAdd(sign.Code, new SignImageMatch(sign.Code, sign.Name, image, q.Id, answer));
                }
            }
        }

        var missing = signs.Select(s => s.Code).Where(c => !matched.ContainsKey(c)).ToList();
        return new SignImageReport(
            usedExamNames,
            matched.Values.OrderBy(m => m.Code, StringComparer.Ordinal).ToList(),
            unmatched,
            missing);
    }

    /// <summary>Signs the answer names: by code ("SR-01"), by exact name, or by the longest name the text contains.</summary>
    public static IReadOnlyList<SignDto> Match(string answer, IReadOnlyList<SignDto> candidates)
    {
        var text = Fold(answer);
        if (text.Length == 0)
        {
            return [];
        }

        // "Señal de PARE", "La señal …", etc.
        text = StripSignLeadIn(text);

        var code = CodePattern().Match(text);
        if (!code.Success)
        {
            // SR01 / SP25A without separator after Fold still works via \s?; also try glued form.
            code = GluedCodePattern().Match(text.Replace(" ", "", StringComparison.Ordinal));
        }

        if (code.Success)
        {
            var wanted = CodeKey($"{code.Groups[1].Value} {code.Groups[2].Value}");
            var byCode = candidates.Where(s => CodeKey(Fold(s.Code)) == wanted).ToList();
            if (byCode.Count > 0)
            {
                return byCode;
            }
        }

        var exact = candidates.Where(s => Fold(s.Name) == text).ToList();
        if (exact.Count > 0)
        {
            return exact;
        }

        var padded = $" {text} ";
        var contained = candidates
            .Select(s => (Sign: s, Name: Fold(s.Name)))
            .Where(x => x.Name.Length >= 4 && padded.Contains($" {x.Name} ", StringComparison.Ordinal))
            .ToList();
        if (contained.Count > 0)
        {
            var longest = contained.Max(x => x.Name.Length);
            return contained.Where(x => x.Name.Length == longest).Select(x => x.Sign).ToList();
        }

        var words = Words(text);
        var best = candidates
            .Select(s => (Sign: s, Score: Similarity(words, Words(Fold(s.Name)))))
            .Where(x => x.Score >= 0.75)
            .OrderByDescending(x => x.Score)
            .ToList();
        return best.Count > 0 && (best.Count == 1 || best[0].Score > best[1].Score)
            ? [best[0].Sign]
            : [];
    }

    private static string StripSignLeadIn(string folded)
    {
        var t = folded;
        // LA SENAL DE X / SENAL DE X / SENAL X
        t = LeadInPattern().Replace(t, "").Trim();
        return t.Length > 0 ? t : folded;
    }

    private static bool IsUsableImage([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && !url.StartsWith("/signals/", StringComparison.OrdinalIgnoreCase)
        && ((url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static string? FamilyPrefix(string examName)
    {
        var name = $" {Fold(examName)} ";
        if (name.Contains("PREVENT", StringComparison.Ordinal) || name.Contains(" SP ", StringComparison.Ordinal))
        {
            return "SP";
        }

        if (name.Contains("REGLAMENT", StringComparison.Ordinal) || name.Contains(" SR ", StringComparison.Ordinal))
        {
            return "SR";
        }

        return name.Contains("INFORMAT", StringComparison.Ordinal) || name.Contains(" SI ", StringComparison.Ordinal)
            ? "SI"
            : null;
    }

    private static string CodeKey(string folded)
    {
        var m = CodeKeyPattern().Match(folded);
        return m.Success ? $"{m.Groups[1].Value}{int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)}{m.Groups[3].Value}" : folded;
    }

    private static HashSet<string> Words(string folded) =>
        folded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();

    private static double Similarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var common = a.Count(b.Contains);
        return common / (double)(a.Count + b.Count - common);
    }

    /// <summary>Uppercase, no accents, only letters and digits separated by single spaces.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [GeneratedRegex(@"\b(SR|SP|SI)\s?(\d{1,2}[A-Z]?)\b")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^(SR|SP|SI)(\d{1,2}[A-Z]?)$")]
    private static partial Regex GluedCodePattern();

    [GeneratedRegex(@"^(?:LA\s+)?SENAL(?:ES)?(?:\s+DE)?\s+")]
    private static partial Regex LeadInPattern();

    [GeneratedRegex(@"^(SR|SP|SI)\s?(\d{1,2})\s?([A-Z]?)$")]
    private static partial Regex CodeKeyPattern();
}
