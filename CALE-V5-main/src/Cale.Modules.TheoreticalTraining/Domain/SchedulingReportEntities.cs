namespace Cale.Modules.TheoreticalTraining.Domain;

public static class SchedulingReportTypes
{
    public const string ManualToday = "ManualToday";
    public const string ManualTomorrow = "ManualTomorrow";
    public const string DailyFull = "DailyFull";
    public const string DailyChanges = "DailyChanges";
}

public static class SchedulingReportStatuses
{
    public const string Generated = "Generated";
    public const string SendSkipped = "SendSkipped";
    public const string Failed = "Failed";
    /// <summary>Reserved for a future WhatsApp activation. Never set in phase 1.</summary>
    public const string Sent = "Sent";
}

public static class SchedulingReportCutKinds
{
    public const string TwoPmBaseline = "TwoPmBaseline";
}

/// <summary>Audit row for a generated (or failed) scheduling report. Does not imply WhatsApp delivery.</summary>
public sealed class SchedulingReportRun
{
    public long Id { get; set; }
    public int SchoolUserId { get; set; }
    public string Type { get; set; } = SchedulingReportTypes.ManualToday;
    public DateOnly TargetDate { get; set; }
    public string Status { get; set; } = SchedulingReportStatuses.Generated;
    public string FileName { get; set; } = "";
    public int ByteLength { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Baseline snapshot used later for the 17:00 changes report (14:00 cut).</summary>
public sealed class SchedulingReportSnapshot
{
    public long Id { get; set; }
    public int SchoolUserId { get; set; }
    public string CutKind { get; set; } = SchedulingReportCutKinds.TwoPmBaseline;
    public DateOnly TargetDate { get; set; }
    public string PayloadJson { get; set; } = "[]";
    public string PayloadHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
