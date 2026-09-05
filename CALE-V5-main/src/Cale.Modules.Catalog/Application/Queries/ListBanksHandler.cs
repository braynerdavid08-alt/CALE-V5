using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;

namespace Cale.Modules.Catalog.Application.Queries;

public sealed class ListBanksHandler
{
    private readonly ICatalogStore _store;

    public ListBanksHandler(ICatalogStore store) => _store = store;

    public async Task<IReadOnlyList<BankDto>> HandleAsync(
        bool activeOnly,
        CancellationToken ct,
        bool includeThemes = false,
        int? viewerUserId = null,
        bool isAdmin = false)
    {
        var banks = await _store.ListBanksAsync(activeOnly, ct, viewerUserId, isAdmin);
        var themesByBank = includeThemes
            ? await LoadThemesByBankAsync(ct)
            : new Dictionary<int, (string Label, IReadOnlyList<BankThemeDto> Themes)>();
        var difficultiesByBank = includeThemes
            ? await LoadDifficultiesByBankAsync(ct)
            : new Dictionary<int, IReadOnlyList<BankThemeDto>>();

        var counts = await _store.CountActiveQuestionsByBankIdsAsync(
            banks.Select(b => b.Id).ToList(),
            ct);

        var result = new List<BankDto>(banks.Count);
        foreach (var bank in banks)
        {
            counts.TryGetValue(bank.Id, out var count);
            themesByBank.TryGetValue(bank.Id, out var themePack);
            difficultiesByBank.TryGetValue(bank.Id, out var difficulties);
            result.Add(new BankDto(
                bank.Id,
                bank.Name,
                bank.Description,
                bank.IsActive,
                count,
                themePack.Label,
                themePack.Themes,
                difficulties));
        }

        return result;
    }

    private async Task<Dictionary<int, (string Label, IReadOnlyList<BankThemeDto> Themes)>> LoadThemesByBankAsync(
        CancellationToken ct)
    {
        var rows = await _store.ListActiveThemeRowsAsync(ct);
        return rows
            .GroupBy(r => r.BankId)
            .ToDictionary(g => g.Key, g => PickThemes(g.ToList()));
    }

    private static (string Label, IReadOnlyList<BankThemeDto> Themes) PickThemes(
        IReadOnlyList<QuestionThemeRow> rows)
    {
        var topics = Group(rows, r => r.Topic);
        var subjects = Group(rows, r => r.Subject);
        var subtopics = Group(rows, r => r.Subtopic);

        if (IsUseful(topics))
        {
            return ("Temas", topics);
        }

        if (IsUseful(subjects))
        {
            return ("Categorías", subjects);
        }

        if (IsUseful(subtopics))
        {
            return ("Subtemas", subtopics);
        }

        if (topics.Count > 0)
        {
            return ("Temas", topics);
        }

        if (subjects.Count > 0)
        {
            return ("Categorías", subjects);
        }

        return ("Temas", []);
    }

    private static IReadOnlyList<BankThemeDto> Group(
        IReadOnlyList<QuestionThemeRow> rows,
        Func<QuestionThemeRow, string?> selector) =>
        rows
            .Select(selector)
            .Select(Normalize)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BankThemeDto(g.Key, g.Count()))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsUseful(IReadOnlyList<BankThemeDto> groups) =>
        groups.Count is >= 2 and <= 60
        && groups.Average(g => g.QuestionCount) >= 2;

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "General" : value.Trim();

    private async Task<Dictionary<int, IReadOnlyList<BankThemeDto>>> LoadDifficultiesByBankAsync(
        CancellationToken ct)
    {
        var rows = await _store.ListActiveDifficultyRowsAsync(ct);
        return rows
            .GroupBy(r => r.BankId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<BankThemeDto>)g
                    .Select(r => string.IsNullOrWhiteSpace(r.Difficulty) ? "Sin nivel" : r.Difficulty.Trim())
                    .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(grp => new BankThemeDto(grp.Key, grp.Count()))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }
}
