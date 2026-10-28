using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cale.Modules.TheoreticalTraining.Application.DTOs;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed record SchedulingReportDiffItem(
    string ChangeKind,
    string Kind,
    int Id,
    string Date,
    string StartTime,
    string Title,
    string? InstructorName,
    string Status,
    int Capacity,
    int Occupied,
    string Detail);

public sealed record SchedulingReportDiffResult(
    IReadOnlyList<SchedulingReportDiffItem> Changes,
    bool HasChanges);

/// <summary>
/// Compares a baseline agenda snapshot (14:00 cut) with the current agenda for the same day.
/// Ready for the future 17:00 job; not invoked automatically in phase 1.
/// </summary>
public sealed class SchedulingReportDiffService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string SerializeSnapshot(IReadOnlyList<AgendaItemDto> items) =>
        JsonSerializer.Serialize(Normalize(items), JsonOpts);

    public string HashPayload(string payloadJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public SchedulingReportDiffResult Diff(string baselineJson, IReadOnlyList<AgendaItemDto> current)
    {
        var baseline = Deserialize(baselineJson);
        var currentNorm = Normalize(current);
        var baselineMap = baseline.ToDictionary(Key);
        var currentMap = currentNorm.ToDictionary(Key);

        var changes = new List<SchedulingReportDiffItem>();

        foreach (var (key, item) in currentMap)
        {
            if (!baselineMap.TryGetValue(key, out var before))
            {
                changes.Add(ToChange("Alta", item, "Nuevo ítem en la programación"));
                continue;
            }

            if (!SameSchedule(before, item))
            {
                changes.Add(ToChange(
                    "Reprogramación",
                    item,
                    $"Antes: {before.Date} {before.StartTime} · {before.Title}; ahora: {item.Date} {item.StartTime} · {item.Title}"));
            }
            else if (!SameEnrollment(before, item))
            {
                changes.Add(ToChange(
                    "Cupos",
                    item,
                    $"Ocupados {before.Occupied}/{before.Capacity} → {item.Occupied}/{item.Capacity}; estado {before.Status} → {item.Status}"));
            }
        }

        foreach (var (key, item) in baselineMap)
        {
            if (!currentMap.ContainsKey(key))
            {
                changes.Add(ToChange("Baja", item, "Eliminado o cancelado respecto al corte"));
            }
        }

        return new SchedulingReportDiffResult(
            changes
                .OrderBy(x => x.Date, StringComparer.Ordinal)
                .ThenBy(x => x.StartTime, StringComparer.Ordinal)
                .ThenBy(x => x.ChangeKind, StringComparer.Ordinal)
                .ToList(),
            changes.Count > 0);
    }

    private static List<AgendaItemDto> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AgendaItemDto>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<AgendaItemDto> Normalize(IReadOnlyList<AgendaItemDto> items) =>
        items
            .OrderBy(x => x.Kind, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .ThenBy(x => x.Date, StringComparer.Ordinal)
            .ThenBy(x => x.StartTime, StringComparer.Ordinal)
            .Select(x => x with
            {
                Students = x.Students
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

    // Stable identity: Kind+Id. Exam slots often share Id=0 — fall back to slot fingerprint.
    private static string Key(AgendaItemDto x) =>
        x.Id != 0
            ? $"{x.Kind}:{x.Id}"
            : $"{x.Kind}:0:{x.Date}:{x.StartTime}:{x.Title}";

    private static bool SameSchedule(AgendaItemDto a, AgendaItemDto b) =>
        a.Date == b.Date
        && a.StartTime == b.StartTime
        && a.EndTime == b.EndTime
        && a.Title == b.Title
        && a.InstructorName == b.InstructorName;

    private static bool SameEnrollment(AgendaItemDto a, AgendaItemDto b) =>
        a.Status == b.Status
        && a.Capacity == b.Capacity
        && a.Occupied == b.Occupied
        && a.Available == b.Available;

    private static SchedulingReportDiffItem ToChange(string changeKind, AgendaItemDto item, string detail) =>
        new(
            changeKind,
            item.Kind,
            item.Id,
            item.Date,
            item.StartTime,
            item.Title,
            item.InstructorName,
            item.Status,
            item.Capacity,
            item.Occupied,
            detail);
}
