namespace SeedLuzVerdeDemo;

/// <summary>Runtime flags shared by seeder / credentials (set from Program before seed).</summary>
internal static class DemoRuntime
{
    /// <summary>When true, target is cealuzverde.com Postgres; owners get secure passwords.</summary>
    public static bool PromoteProduction { get; set; }

    /// <summary>Inspect only — no writes.</summary>
    public static bool DryRun { get; set; }

    /// <summary>Lab SQLite owner password from env / CLI (never hardcoded).</summary>
    public static string? LabOwnerPassword { get; set; }

    /// <summary>True when lab password was generated because none was provided.</summary>
    public static bool LabOwnerPasswordWasGenerated { get; set; }

    public static string ConfirmedHost { get; set; } = "";
    public static string ConfirmedDatabase { get; set; } = "";
    public static string ConfirmedBackupDate { get; set; } = "";
}
