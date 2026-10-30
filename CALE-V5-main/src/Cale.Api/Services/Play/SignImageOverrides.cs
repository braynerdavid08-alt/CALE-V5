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
/// Real sign pictures come from sign exams in the database (same pool as Señal relámpago),
/// plus questions in banks owned by the platform/admin. Each photo is paired to a catalog
/// code via the correct answer (name or code). Schematic /signals/{code}.svg is only a fallback.
/// </summary>
public sealed partial class SignImageOverrides
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private static readonly string[] ExamKeywords = ["señal", "senal"];

    /// <summary>Common exam wording → catalog folded name (after <see cref="Fold"/>).</summary>
    private static readonly Dictionary<string, string> AnswerAliases = new(StringComparer.Ordinal)
    {
        ["PROHIBIDO ADELANTAR"] = "NO ADELANTAR",
        ["PROHIBICION DE ADELANTAR"] = "NO ADELANTAR",
        ["PROHIBIDO ESTACIONAR"] = "PROHIBIDO PARQUEAR",
        ["NO ESTACIONAR"] = "PROHIBIDO PARQUEAR",
        ["PROHIBIDO ESTACIONARSE"] = "PROHIBIDO PARQUEAR",
        ["ALTO"] = "PARE",
        ["STOP"] = "PARE",
        ["CEDA EL PASO"] = "CEDA EL PASO",
        ["YIELD"] = "CEDA EL PASO",
        ["SIGA DE FRENTE"] = "DIRECCION OBLIGADA O SIGA DE FRENTE",
        ["DIRECCION OBLIGATORIA"] = "DIRECCION OBLIGADA O SIGA DE FRENTE",
        ["DIRECCION OBLIGADA"] = "DIRECCION OBLIGADA O SIGA DE FRENTE",
        ["GIRO EN U"] = "GIRO EN U SOLAMENTE",
        ["PROHIBIDO GIRO EN U"] = "PROHIBIDO GIRAR EN U",
        ["PROHIBIDO GIRAR EN U"] = "PROHIBIDO GIRAR EN U",
        ["NO GIRAR EN U"] = "PROHIBIDO GIRAR EN U",
        ["NO GIRAR A LA IZQUIERDA"] = "PROHIBIDO GIRAR A LA IZQUIERDA",
        ["NO GIRAR A LA DERECHA"] = "PROHIBIDO GIRAR A LA DERECHA",
        ["VELOCIDAD MAXIMA"] = "VELOCIDAD MAXIMA PERMITIDA",
        ["VELOCIDAD MINIMA"] = "VELOCIDAD MINIMA PERMITIDA",
    };

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

        // Platform banks (null owner) + banks created by an admin account.
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
            .Where(e => IsSignExamName(e.Name))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matched = new Dictionary<string, SignImageMatch>(StringComparer.OrdinalIgnoreCase);
        var unmatched = new List<SignImageUnmatched>();
        var usedExamNames = new List<string>();
        var seenQuestionIds = new HashSet<int>();

        if (exams.Count > 0)
        {
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

            foreach (var exam in exams)
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

                usedExamNames.Add(exam.Name);
                var family = FamilyPrefix(exam.Name);
                var familyCandidates = signs
                    .Where(s => family is null || s.Code.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var q in allowed)
                {
                    seenQuestionIds.Add(q.Id);
                    TryConsumeQuestion(q, exam.Name, familyCandidates, signs, matched, unmatched);
                }
            }
        }

        // Second pass: imaged questions in trusted banks not already consumed from a sign exam
        // (covers bank content that was never linked into an exam named "señal").
        if (trustedBanks.Count > 0)
        {
            var trustedList = trustedBanks.ToList();
            var bankQuestions = await db.Set<Question>().AsNoTracking()
                .Include(q => q.Options)
                .Where(q => q.IsActive && trustedList.Contains(q.BankId))
                .ToListAsync(ct);

            var bankLabel = "Bancos oficiales / admin";
            var usedBankPass = false;
            foreach (var q in bankQuestions)
            {
                if (seenQuestionIds.Contains(q.Id))
                {
                    continue;
                }

                var before = matched.Count;
                var localUnmatched = new List<SignImageUnmatched>();
                TryConsumeQuestion(q, bankLabel, signs, signs, matched, localUnmatched);
                if (matched.Count > before)
                {
                    usedBankPass = true;
                }
            }

            if (usedBankPass && !usedExamNames.Contains(bankLabel, StringComparer.Ordinal))
            {
                usedExamNames.Add(bankLabel);
            }
        }

        var missing = signs.Select(s => s.Code).Where(c => !matched.ContainsKey(c)).ToList();
        return new SignImageReport(
            usedExamNames,
            matched.Values.OrderBy(m => m.Code, StringComparer.Ordinal).ToList(),
            unmatched,
            missing);
    }

    private static void TryConsumeQuestion(
        Question q,
        string examName,
        IReadOnlyList<SignDto> preferred,
        IReadOnlyList<SignDto> allSigns,
        Dictionary<string, SignImageMatch> matched,
        List<SignImageUnmatched> unmatched)
    {
        var correct = q.Options.FirstOrDefault(o => o.IsCorrect);
        if (correct is null)
        {
            return;
        }

        var (image, answer) = !string.IsNullOrWhiteSpace(q.ImageUrl)
            ? (q.ImageUrl!, correct.Text)
            : (correct.ImageUrl ?? "", q.Text);
        if (!IsUsableImage(image))
        {
            return;
        }

        var codes = Match(answer, preferred);
        if (codes.Count == 0 && !ReferenceEquals(preferred, allSigns))
        {
            codes = Match(answer, allSigns);
        }

        if (codes.Count == 0 && !string.IsNullOrWhiteSpace(q.Text) && !string.Equals(q.Text, answer, StringComparison.Ordinal))
        {
            codes = Match(q.Text, preferred);
            if (codes.Count == 0 && !ReferenceEquals(preferred, allSigns))
            {
                codes = Match(q.Text, allSigns);
            }
        }

        if (codes.Count == 0)
        {
            unmatched.Add(new SignImageUnmatched(q.Id, examName, answer, image));
            return;
        }

        foreach (var sign in codes)
        {
            matched.TryAdd(sign.Code, new SignImageMatch(sign.Code, sign.Name, image, q.Id, answer));
        }
    }

    public static bool IsSignExamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (ExamKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var folded = $" {Fold(name)} ";
        return folded.Contains(" REGLAMENT", StringComparison.Ordinal)
            || folded.Contains(" PREVENT", StringComparison.Ordinal)
            || folded.Contains(" INFORMAT", StringComparison.Ordinal)
            || Regex.IsMatch(folded, @"\b(SR|SP|SI)\b");
    }

    /// <summary>Signs the answer names: by code ("SR-01"), by exact name, or by the longest name the text contains.</summary>
    public static IReadOnlyList<SignDto> Match(string answer, IReadOnlyList<SignDto> candidates)
    {
        var text = Fold(answer);
        if (text.Length == 0 || candidates.Count == 0)
        {
            return [];
        }

        text = StripSignLeadIn(text);
        if (AnswerAliases.TryGetValue(text, out var alias))
        {
            text = alias;
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

        // Answer is a shortened official name (all answer words appear in the catalog name).
        var answerWords = Words(text);
        if (answerWords.Count >= 2)
        {
            var subsets = candidates
                .Select(s => (Sign: s, Name: Fold(s.Name), Words: Words(Fold(s.Name))))
                .Where(x => answerWords.All(x.Words.Contains))
                .OrderBy(x => x.Words.Count)
                .ThenByDescending(x => x.Name.Length)
                .ToList();
            if (subsets.Count == 1
                || (subsets.Count > 1 && subsets[0].Words.Count < subsets[1].Words.Count))
            {
                return [subsets[0].Sign];
            }
        }

        var best = candidates
            .Select(s => (Sign: s, Score: Similarity(answerWords, Words(Fold(s.Name)))))
            .Where(x => x.Score >= 0.72)
            .OrderByDescending(x => x.Score)
            .ToList();
        return best.Count > 0 && (best.Count == 1 || best[0].Score > best[1].Score + 0.02)
            ? [best[0].Sign]
            : [];
    }

    private static string StripSignLeadIn(string folded)
    {
        var t = LeadInPattern().Replace(folded, "").Trim();
        return t.Length > 0 ? t : folded;
    }

    private static bool IsUsableImage([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && !url.StartsWith("/signals/", StringComparison.OrdinalIgnoreCase)
        && ((url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

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
