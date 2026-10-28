namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Master switches for school scheduling reports. Defaults keep WhatsApp and automatic jobs off.
/// </summary>
public sealed class SchedulingReportsOptions
{
    public const string SectionName = "SchedulingReports";

    /// <summary>Module visible/usable for status; downloads still need AllowManualDownload.</summary>
    public bool Enabled { get; set; }

    public bool AllowManualDownload { get; set; } = true;

    public bool AllowManualWhatsAppSend { get; set; }

    public bool EnableDailyFullReportJob { get; set; }

    public bool EnableDailyChangesReportJob { get; set; }

    /// <summary>When true, a future changes job may emit empty reports. Default false.</summary>
    public bool EmptyChangesReports { get; set; }

    public SchedulingReportsWhatsAppOptions WhatsApp { get; set; } = new();

    public bool CanManualDownload => AllowManualDownload;

    public bool CanManualWhatsAppSend =>
        Enabled && AllowManualWhatsAppSend && WhatsApp.IsConfigured;

    public bool DailyJobsArmed =>
        Enabled && (EnableDailyFullReportJob || EnableDailyChangesReportJob);
}

public sealed class SchedulingReportsWhatsAppOptions
{
    public bool Enabled { get; set; }
    public string PhoneNumberId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string ApiVersion { get; set; } = "v21.0";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(PhoneNumberId)
        && !string.IsNullOrWhiteSpace(AccessToken);
}
