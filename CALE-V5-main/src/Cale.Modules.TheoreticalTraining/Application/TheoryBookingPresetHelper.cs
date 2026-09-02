using System.Text.Json;
using Cale.Modules.TheoreticalTraining.Application.DTOs;

namespace Cale.Modules.TheoreticalTraining.Application;

internal sealed class BookingPresetStore
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool WeekdaysEnabled { get; set; } = true;
    public bool SaturdayEnabled { get; set; } = true;
    public int MaxWeekdayClassesPerDay { get; set; } = 1;
    public int MaxSaturdayClassesPerDay { get; set; } = 4;
    public int MaxDailyTheoryMinutes { get; set; }
    public int WeekdayReservationOpenDaysBefore { get; set; } = 1;
    public int SaturdayReservationOpenDaysBefore { get; set; } = 2;
    public string? StudentBookingWindowStart { get; set; }
    public string? StudentBookingWindowEnd { get; set; }
}

public static class TheoryBookingPresetHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<TheoryBookingPresetDto> DeserializeSaved(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
        {
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<BookingPresetStore>>(json, JsonOpts) ?? [];
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Id) && !string.IsNullOrWhiteSpace(r.Name))
                .Select(Map)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeSaved(IReadOnlyList<TheoryBookingPresetDto>? presets)
    {
        if (presets is null || presets.Count == 0)
        {
            return "[]";
        }

        var rows = presets
            .Where(p => !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new BookingPresetStore
            {
                Id = p.Id.Trim(),
                Name = p.Name.Trim(),
                WeekdaysEnabled = p.WeekdaysEnabled,
                SaturdayEnabled = p.SaturdayEnabled,
                MaxWeekdayClassesPerDay = p.MaxWeekdayClassesPerDay,
                MaxSaturdayClassesPerDay = p.MaxSaturdayClassesPerDay,
                MaxDailyTheoryMinutes = p.MaxDailyTheoryMinutes,
                WeekdayReservationOpenDaysBefore = p.WeekdayReservationOpenDaysBefore,
                SaturdayReservationOpenDaysBefore = p.SaturdayReservationOpenDaysBefore,
                StudentBookingWindowStart = p.StudentBookingWindowStart,
                StudentBookingWindowEnd = p.StudentBookingWindowEnd
            })
            .ToList();

        return JsonSerializer.Serialize(rows, JsonOpts);
    }

    public static IReadOnlyList<string> DeserializeHidden(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts)?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeHidden(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            return "[]";
        }

        var normalized = keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(normalized, JsonOpts);
    }

    private static TheoryBookingPresetDto Map(BookingPresetStore row) =>
        new(
            row.Id,
            row.Name,
            row.WeekdaysEnabled,
            row.SaturdayEnabled,
            row.MaxWeekdayClassesPerDay,
            row.MaxSaturdayClassesPerDay,
            row.MaxDailyTheoryMinutes,
            row.WeekdayReservationOpenDaysBefore,
            row.SaturdayReservationOpenDaysBefore,
            row.StudentBookingWindowStart,
            row.StudentBookingWindowEnd);
}
