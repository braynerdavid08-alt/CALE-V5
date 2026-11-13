namespace SeedLuzVerdeDemo;

/// <summary>Stable markers so DEMO rows can be found, regenerated and purged safely.</summary>
internal static class DemoMarkers
{
    /// <summary>Primary DEMO mailbox domain on the live platform.</summary>
    public const string EmailDomain = "@cealuzverde.com";

    /// <summary>Legacy local-only domain (still purged if present).</summary>
    public const string LegacyEmailDomain = "@demo.luzverde.test";

    public const string EmailPrefix = "demo.";
    public const string LegalNamePrefix = "CEA DEMO";
    public const string Tag = "DEMO-LV";
    public const string ExamName = "DEMO Examen Teórico Oficial CEA";
    public const string BankName = "DEMO Banco Teórico Luz Verde";
    public const string CourtesyReason = "DEMO Luz Verde — acceso operativo de demostración (sin presencia comercial)";

    public static readonly DemoSchoolSpec[] Schools =
    [
        new(
            Key: "norte",
            LegalName: "CEA DEMO NORTE",
            TaxId: "900700001-6",
            OwnerEmail: "demo.norte@cealuzverde.com",
            OwnerName: "Propietario Demo Norte",
            Phone: "3007001001",
            Address: "Calle 85 # 42-15, Riomar",
            City: "Barranquilla",
            Department: "Atlántico",
            DocPrefix: "11001"),
        new(
            Key: "centro",
            LegalName: "CEA DEMO CENTRO",
            TaxId: "900700002-3",
            OwnerEmail: "demo.centro@cealuzverde.com",
            OwnerName: "Propietario Demo Centro",
            Phone: "3007002002",
            Address: "Carrera 46 # 72-30, Centro Histórico",
            City: "Barranquilla",
            Department: "Atlántico",
            DocPrefix: "11002"),
        new(
            Key: "sur",
            LegalName: "CEA DEMO SUR",
            TaxId: "900700003-0",
            OwnerEmail: "demo.sur@cealuzverde.com",
            OwnerName: "Propietario Demo Sur",
            Phone: "3007003003",
            Address: "Calle 30 # 25-80, Suroriente",
            City: "Barranquilla",
            Department: "Atlántico",
            DocPrefix: "11003")
    ];

    public static readonly string[] InstructorFirstNames =
        ["Andrés", "Camila", "Diego"];

    public static readonly string[] InstructorLastNames =
        ["Pérez Demo", "Ruiz Demo", "Castro Demo"];

    public static readonly (string First, string Last, string License)[] StudentNames =
    [
        ("Ana", "Rojas Demo", "B1"),
        ("Bruno", "Salas Demo", "B1"),
        ("Carla", "Mena Demo", "A2"),
        ("David", "Ochoa Demo", "B1"),
        ("Elena", "Paz Demo", "B1"),
        ("Felipe", "Quintero Demo", "C1"),
        ("Gina", "Restrepo Demo", "B1"),
        ("Hugo", "Soto Demo", "A2"),
        ("Irene", "Toro Demo", "B1"),
        ("Jorge", "Uribe Demo", "B1"),
        ("Karen", "Vargas Demo", "A2"),
        ("Luis", "Wong Demo", "B1"),
        ("María", "Yepes Demo", "B1"),
        ("Nicolás", "Zapata Demo", "C1")
    ];

    public static bool IsDemoEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var e = email.Trim().ToLowerInvariant();
        if (e.EndsWith(LegacyEmailDomain, StringComparison.Ordinal))
        {
            return true;
        }

        // Live DEMO accounts: demo.*@cealuzverde.com (owners, instructors, students).
        return e.EndsWith(EmailDomain, StringComparison.Ordinal)
            && e.StartsWith(EmailPrefix, StringComparison.Ordinal);
    }

    public static string InstructorEmail(string schoolKey, int index1Based) =>
        $"demo.instructor.{index1Based}.{schoolKey}{EmailDomain}";

    public static string StudentEmail(string schoolKey, int scenario) =>
        $"demo.estudiante.{scenario:00}.{schoolKey}{EmailDomain}";

    public static IEnumerable<string> AllPlannedEmails()
    {
        foreach (var school in Schools)
        {
            yield return school.OwnerEmail;
            for (var i = 1; i <= 3; i++)
            {
                yield return InstructorEmail(school.Key, i);
            }

            for (var s = 1; s <= 14; s++)
            {
                yield return StudentEmail(school.Key, s);
            }
        }
    }
}

internal sealed record DemoSchoolSpec(
    string Key,
    string LegalName,
    string TaxId,
    string OwnerEmail,
    string OwnerName,
    string Phone,
    string Address,
    string City,
    string Department,
    string DocPrefix);

/// <summary>1-based academic scenario matching the demo brief.</summary>
internal enum DemoScenario
{
    NewlyEnrolled = 1,
    TheoryStarted = 2,
    TheoryPartial = 3,
    TheoryFinishing = 4,
    TheoryDonePendingExam = 5,
    TheoryExamScheduled = 6,
    TheoryExamPassed = 7,
    TheoryExamFailed = 8,
    PracticalEnabled = 9,
    PracticalPartial = 10,
    PracticalDonePendingExam = 11,
    PracticalExamScheduled = 12,
    PracticalExamPassed = 13,
    PracticalExamFailed = 14
}
