using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Turns the recurring weekly schedule plus date overrides into the effective exam slots of each date.
/// Pure function: no database access, so the rules are easy to test.
/// </summary>
public static class ExamSlotPlanner
{
    public const string SourceTemplate = "template";
    public const string SourceExtra = "extra";
    public const string SourceNone = "none";

    public sealed record EffectiveSlot(
        DateOnly Date,
        TimeOnly Time,
        int Capacity,
        bool IsClosed,
        string Source,
        int? TemplateId,
        TheoryExamScheduleOverride? Override);

    public sealed record EffectiveDay(
        DateOnly Date,
        bool IsClosed,
        TheoryExamScheduleOverride? Closure,
        IReadOnlyList<EffectiveSlot> Slots);

    public static IReadOnlyList<EffectiveDay> Plan(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<TheoryExamScheduleTemplate> templates,
        IReadOnlyCollection<TheoryExamScheduleOverride> overrides)
    {
        var days = new List<EffectiveDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(PlanDay(date, templates, overrides));
        }

        return days;
    }

    public static EffectiveDay PlanDay(
        DateOnly date,
        IReadOnlyCollection<TheoryExamScheduleTemplate> templates,
        IReadOnlyCollection<TheoryExamScheduleOverride> overrides)
    {
        var closure = overrides.FirstOrDefault(o => o.Date == date && o.IsWholeDay);
        var dayClosed = closure?.IsClosed == true;
        var slotOverrides = overrides
            .Where(o => o.Date == date && !o.IsWholeDay)
            .GroupBy(o => o.StartTime)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.UpdatedAt).First());

        var slots = new List<EffectiveSlot>();
        var dayOfWeek = (int)date.DayOfWeek;
        foreach (var template in templates.Where(t => t.IsActive && t.DayOfWeek == dayOfWeek))
        {
            slotOverrides.TryGetValue(template.StartTime, out var ov);
            slots.Add(new EffectiveSlot(
                date,
                template.StartTime,
                ov?.Capacity ?? template.Capacity,
                dayClosed || ov?.IsClosed == true,
                SourceTemplate,
                template.Id,
                ov));
        }

        foreach (var (time, ov) in slotOverrides)
        {
            if (ov.IsClosed || slots.Any(s => s.Time == time))
            {
                continue;
            }

            slots.Add(new EffectiveSlot(date, time, ov.Capacity ?? 1, dayClosed, SourceExtra, null, ov));
        }

        return new EffectiveDay(
            date,
            dayClosed,
            closure,
            slots.OrderBy(s => s.Time).ToList());
    }

    public static string ResolveStatus(int capacity, int occupied, bool isClosed, bool isPast)
    {
        if (isClosed)
        {
            return TheoryExamSlotStatuses.Closed;
        }

        if (isPast)
        {
            return TheoryExamSlotStatuses.Past;
        }

        return occupied >= capacity ? TheoryExamSlotStatuses.Full : TheoryExamSlotStatuses.Available;
    }
}
