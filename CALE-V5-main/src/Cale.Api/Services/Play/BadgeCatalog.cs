namespace Cale.Api.Services.Play;

public sealed record PlayStats(
    int FinishedAttempts,
    int PassedAttempts,
    int PerfectAttempts,
    int TotalCorrect,
    int BestStreak,
    int DailyCompleted,
    int DailyCorrect,
    int MistakesMastered,
    int SignsBest,
    int SignsCorrect,
    int DuelWins,
    int DuelPlayed)
{
    public int Xp =>
        TotalCorrect * XpRules.AttemptCorrect
        + PassedAttempts * XpRules.AttemptPassed
        + DailyCompleted * XpRules.DailyCompleted
        + DailyCorrect * XpRules.DailyCorrect
        + MistakesMastered * XpRules.MistakeMastered
        + SignsCorrect * XpRules.SignCorrect
        + DuelWins * XpRules.DuelWon
        + DuelPlayed * XpRules.DuelPlayed;
}

public static class XpRules
{
    public const int AttemptCorrect = 2;
    public const int AttemptPassed = 25;
    public const int DailyCompleted = 15;
    public const int DailyCorrect = 2;
    public const int MistakeMastered = 5;
    public const int SignCorrect = 1;
    public const int DuelWon = 20;
    public const int DuelPlayed = 5;
}

public sealed record BadgeDef(
    string Code,
    string Title,
    string Description,
    string Icon,
    int Target,
    Func<PlayStats, int> Current);

public static class BadgeCatalog
{
    public static IReadOnlyList<BadgeDef> All { get; } =
    [
        new("primer_simulacro", "Primer arranque", "Termina tu primer simulacro.", "exam", 1, s => s.FinishedAttempts),
        new("primer_aprobado", "Luz verde", "Aprueba tu primer simulacro.", "star", 1, s => s.PassedAttempts),
        new("cinco_aprobados", "Conductor constante", "Aprueba 5 simulacros.", "star", 5, s => s.PassedAttempts),
        new("perfecto", "Cero errores", "Saca 100 % en un simulacro de 10 preguntas o más.", "graduate", 1, s => s.PerfectAttempts),
        new("racha_3", "Motor encendido", "Practica 3 días seguidos.", "clock", 3, s => s.BestStreak),
        new("racha_7", "Semana al volante", "Practica 7 días seguidos.", "clock", 7, s => s.BestStreak),
        new("racha_30", "Mes imparable", "Practica 30 días seguidos.", "clock", 30, s => s.BestStreak),
        new("reto_10", "Retador", "Completa 10 retos diarios.", "chart", 10, s => s.DailyCompleted),
        new("errores_25", "Aprende de los errores", "Domina 25 preguntas que habías fallado.", "book", 25, s => s.MistakesMastered),
        new("senales_20", "Ojo de águila", "Acierta 20 señales en una partida de Señal relámpago.", "play", 20, s => s.SignsBest),
        new("duelista", "Primer duelo ganado", "Gana un duelo 1 contra 1.", "users", 1, s => s.DuelWins),
        new("duelo_10", "Campeón de duelos", "Gana 10 duelos.", "users", 10, s => s.DuelWins),
        new("cien_aciertos", "Cien respuestas", "Acierta 100 preguntas en simulacros.", "graduate", 100, s => s.TotalCorrect),
        new("mil_aciertos", "Enciclopedia vial", "Acierta 1.000 preguntas en simulacros.", "graduate", 1000, s => s.TotalCorrect)
    ];

    public static IReadOnlyList<(int Xp, string Name)> Levels { get; } =
    [
        (0, "Aprendiz"),
        (100, "Peatón atento"),
        (300, "Copiloto"),
        (700, "Conductor novato"),
        (1500, "Conductor seguro"),
        (3000, "Conductor experto"),
        (6000, "Maestro del volante")
    ];
}
