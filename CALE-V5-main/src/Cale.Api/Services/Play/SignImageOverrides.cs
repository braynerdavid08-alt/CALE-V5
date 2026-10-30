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
/// Official sign bank built from the three template exams in the database:
/// «Examen Señales Reglamentarias», «Examen Señales Preventivas», «Examen Señales Informativas».
/// Each question with a real photo becomes a catalog entry (name = correct answer, image = question photo).
/// No schematic /signals/*.svg drawings are exposed as the bank.
/// </summary>
public sealed partial class SignImageOverrides
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly PlayContent _content;
    private readonly ILogger<SignImageOverrides> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SignImageReport? _report;
    private IReadOnlyList<SignDto> _bank = [];
    private Dictionary<string, string> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _loadedAt = DateTime.MinValue;

    public SignImageOverrides(IServiceScopeFactory scopes, PlayContent content, ILogger<SignImageOverrides> logger)
    {
        _scopes = scopes;
        _content = content;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SignDto>> BankAsync(CancellationToken ct)
    {
        await EnsureFreshAsync(false, ct);
        return _bank;
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
                var built = await BuildBankAsync(db, _content.Signs, ct);
                _bank = built.Bank;
                _byCode = built.Bank
                    .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().ImageUrl, StringComparer.OrdinalIgnoreCase);
                _report = built.Report;
                _logger.LogInformation(
                    "Sign bank from template exams: {Count} signs ({Exams}). Unmatched image questions: {Unmatched}.",
                    _bank.Count, string.Join(", ", _report.Exams), _report.Unmatched.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not build the official sign bank from template exams.");
                _bank = [];
                _byCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _report ??= new SignImageReport([], [], [], []);
            }

            _loadedAt = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task<(IReadOnlyList<SignDto> Bank, SignImageReport Report)> BuildBankAsync(
        CaleDbContext db,
        IReadOnlyList<SignDto> catalog,
        CancellationToken ct)
    {
        var admins = (await db.Set<User>().AsNoTracking()
                .Select(u => new { u.Id, u.Role })
                .ToListAsync(ct))
            .Where(u => Roles.Normalize(u.Role) == Roles.Admin)
            .Select(u => u.Id)
            .ToHashSet();

        var trustedBanks = (await db.Set<Bank>().AsNoTracking()
                .Where(b => b.IsActive)
                .Select(b => new { b.Id, b.CreatedById })
                .ToListAsync(ct))
            .Where(b => b.CreatedById is null || admins.Contains(b.CreatedById.Value))
            .Select(b => b.Id)
            .ToHashSet();

        var exams = (await db.Set<Exam>().AsNoTracking()
                .Where(e => e.IsActive)
                .Select(e => new { e.Id, e.Name, e.CreatedById, e.BankId })
                .ToListAsync(ct))
            .Select(e => (Exam: e, Family: TemplateFamily(e.Name)))
            .Where(x => x.Family is not null)
            .OrderBy(x => x.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Exam.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exams.Count == 0)
        {
            return ([], new SignImageReport([], [], [], catalog.Select(s => s.Code).ToList()));
        }

        var examIds = exams.Select(x => x.Exam.Id).ToList();
        var links = await db.Set<ExamQuestion>().AsNoTracking()
            .Where(l => examIds.Contains(l.ExamId))
            .Select(l => new { l.ExamId, l.QuestionId })
            .ToListAsync(ct);
        var listed = links
            .GroupBy(l => l.ExamId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.QuestionId).ToHashSet());
        var pickedIds = links.Select(l => l.QuestionId).Distinct().ToList();
        var bankFallbackIds = exams
            .Where(x => x.Exam.BankId is not null && !listed.ContainsKey(x.Exam.Id))
            .Select(x => x.Exam.BankId!.Value)
            .Distinct()
            .ToList();

        var questions = await db.Set<Question>().AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.IsActive && (pickedIds.Contains(q.Id) || bankFallbackIds.Contains(q.BankId)))
            .ToListAsync(ct);

        var bank = new List<SignDto>();
        var matched = new List<SignImageMatch>();
        var unmatched = new List<SignImageUnmatched>();
        var usedExams = new List<string>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (exam, family) in exams)
        {
            var adminOwned = admins.Contains(exam.CreatedById);
            IEnumerable<Question> own = listed.TryGetValue(exam.Id, out var set)
                ? questions.Where(q => set.Contains(q.Id))
                : exam.BankId is int bankId
                    ? questions.Where(q => q.BankId == bankId)
                    : [];
            var allowed = (adminOwned ? own : own.Where(q => trustedBanks.Contains(q.BankId))).ToList();
            if (allowed.Count == 0)
            {
                continue;
            }

            usedExams.Add(exam.Name);

            foreach (var q in allowed.OrderBy(q => q.Id))
            {
                var correct = q.Options.FirstOrDefault(o => o.IsCorrect);
                if (correct is null)
                {
                    continue;
                }

                var (image, answer) = !string.IsNullOrWhiteSpace(q.ImageUrl)
                    ? (q.ImageUrl!, correct.Text)
                    : (correct.ImageUrl ?? "", q.Text);
                if (!IsUsableImage(image))
                {
                    continue;
                }

                var name = CleanSignName(answer);
                if (name.Length < 2)
                {
                    unmatched.Add(new SignImageUnmatched(q.Id, exam.Name, answer, image));
                    continue;
                }

                var nameKey = Fold(name);
                if (!usedImages.Add(image) || !usedNames.Add(nameKey))
                {
                    continue; // same photo or same label already in the bank
                }

                var code = AssignCode(answer, name, family!, catalog, usedCodes, q.Id);
                bank.Add(new SignDto(code, family!, name, image));
                matched.Add(new SignImageMatch(code, name, image, q.Id, answer));
            }
        }

        var missing = catalog.Select(s => s.Code).Where(c => !usedCodes.Contains(c)).ToList();
        var report = new SignImageReport(
            usedExams,
            matched.OrderBy(m => m.Code, StringComparer.Ordinal).ToList(),
            unmatched,
            missing);
        return (bank, report);
    }

    /// <summary>Kept for unit tests and optional code assignment against the JSON catalog.</summary>
    public static async Task<SignImageReport> BuildAsync(
        CaleDbContext db,
        IReadOnlyList<SignDto> signs,
        CancellationToken ct)
    {
        var (_, report) = await BuildBankAsync(db, signs, ct);
        return report;
    }

    private static string AssignCode(
        string answer,
        string name,
        string family,
        IReadOnlyList<SignDto> catalog,
        HashSet<string> usedCodes,
        int questionId)
    {
        var familyCandidates = catalog
            .Where(s => s.Family.Equals(family, StringComparison.OrdinalIgnoreCase)
                || (family.Contains("reglament", StringComparison.OrdinalIgnoreCase) && s.Code.StartsWith("SR", StringComparison.OrdinalIgnoreCase))
                || (family.Contains("prevent", StringComparison.OrdinalIgnoreCase) && s.Code.StartsWith("SP", StringComparison.OrdinalIgnoreCase))
                || (family.Contains("informat", StringComparison.OrdinalIgnoreCase) && s.Code.StartsWith("SI", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var pool = familyCandidates.Count > 0 ? familyCandidates : catalog;
        var hit = Match(answer, pool);
        if (hit.Count == 0)
        {
            hit = Match(name, pool);
        }

        if (hit.Count == 1 && !usedCodes.Contains(hit[0].Code))
        {
            usedCodes.Add(hit[0].Code);
            return hit[0].Code;
        }

        var prefix = family.Contains("prevent", StringComparison.OrdinalIgnoreCase) ? "SP"
            : family.Contains("informat", StringComparison.OrdinalIgnoreCase) ? "SI"
            : "SR";
        var fallback = $"{prefix}-Q{questionId}";
        usedCodes.Add(fallback);
        return fallback;
    }

    private static string? TemplateFamily(string examName)
    {
        var folded = Fold(examName);
        if (folded.Length == 0 || !folded.Contains("SENAL", StringComparison.Ordinal))
        {
            return null;
        }

        if (folded.Contains("REGLAMENT", StringComparison.Ordinal))
        {
            return "Señales reglamentarias";
        }

        if (folded.Contains("PREVENT", StringComparison.Ordinal))
        {
            return "Señales preventivas";
        }

        if (folded.Contains("INFORMAT", StringComparison.Ordinal))
        {
            return "Señales informativas";
        }

        return null;
    }

    private static string CleanSignName(string answer)
    {
        var raw = (answer ?? "").Trim().TrimEnd('.', ':', ';');
        if (raw.StartsWith("Señal de ", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw["Señal de ".Length..].Trim();
        }
        else if (raw.StartsWith("Senal de ", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw["Senal de ".Length..].Trim();
        }

        if (raw.Length == 0)
        {
            return "";
        }

        var culture = CultureInfo.GetCultureInfo("es-CO");
        var lower = raw.ToLower(culture);
        return char.ToUpper(lower[0], culture) + lower[1..];
    }

    public static IReadOnlyList<SignDto> Match(string answer, IReadOnlyList<SignDto> candidates)
    {
        var text = Fold(answer);
        if (text.Length == 0 || candidates.Count == 0)
        {
            return [];
        }

        text = LeadInPattern().Replace(text, "").Trim();
        if (text.Length == 0)
        {
            text = Fold(answer);
        }

        var code = CodePattern().Match(text);
        if (!code.Success)
        {
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
        if (words.Count >= 2)
        {
            var subsets = candidates
                .Select(s => (Sign: s, Name: Fold(s.Name), Words: Words(Fold(s.Name))))
                .Where(x => words.All(x.Words.Contains))
                .OrderBy(x => x.Words.Count)
                .ToList();
            if (subsets.Count == 1
                || (subsets.Count > 1 && subsets[0].Words.Count < subsets[1].Words.Count))
            {
                return [subsets[0].Sign];
            }
        }

        var best = candidates
            .Select(s => (Sign: s, Score: Similarity(words, Words(Fold(s.Name)))))
            .Where(x => x.Score >= 0.72)
            .OrderByDescending(x => x.Score)
            .ToList();
        return best.Count > 0 && (best.Count == 1 || best[0].Score > best[1].Score + 0.02)
            ? [best[0].Sign]
            : [];
    }

    private static bool IsUsableImage([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && !url.StartsWith("/signals/", StringComparison.OrdinalIgnoreCase)
        && ((url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

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
