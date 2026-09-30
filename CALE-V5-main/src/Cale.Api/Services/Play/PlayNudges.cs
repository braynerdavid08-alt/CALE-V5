namespace Cale.Api.Services.Play;

/// <summary>Playful, insistent practice reminders. Picked deterministically per user and day.</summary>
public static class PlayNudges
{
    private static readonly (string Title, string Body)[] Auto =
    [
        ("¿Ya te rendiste? 🙄", "Llevas {days} sin practicar. Cuando pierdas el examen, no me culpes."),
        ("El examen no se pasa solo", "¿Acaso no vas a practicar más? Son 5 preguntas, no seas perezoso."),
        ("Tu racha está llorando 😢", "Tanto que te cuesta abrir la app… el reto diario te está esperando."),
        ("Te estamos viendo 👀", "{days} sin practicar. ¿Quieres repetir el examen y pagarlo otra vez?"),
        ("Excusas no suman puntos", "Pero el reto diario sí. Ven y demuestra que sí puedes."),
        ("¿Seguro que ya te lo sabes todo?", "Porque tus simulacros dicen otra cosa. Practica un ratico."),
        ("Ni el semáforo en rojo espera tanto 🚦", "{days} sin practicar… el examen no va a tener compasión."),
        ("Alerta de pereza detectada 🚨", "Entra 2 minutos y responde el reto diario. Tu licencia te lo agradecerá."),
        ("Plot twist: el examen sí llega", "Y tú sin practicar. Después no digas que nadie te avisó."),
        ("¿Me estás ignorando? 😒", "Llevas {days} sin aparecer. Una partida rápida y te dejo en paz… por hoy.")
    ];

    private static readonly (string Title, string Body)[] FromStaff =
    [
        ("{actor} te está vigilando 👀", "Dice que ya casi no practicas. Haz el reto diario antes de que te llame la atención."),
        ("{actor} ya perdió la paciencia", "¿Acaso no vas a practicar más? Cuando pierdas el examen, no digas que nadie te avisó."),
        ("Mensaje de {actor}: ¡a practicar!", "Son solo 5 preguntas. Sin excusas, sin \"mañana sí\"."),
        ("{actor} pregunta por ti 🤨", "Tus compañeros practicando y tú desaparecido. Entra y responde el reto diario.")
    ];

    public static (string Title, string Body) ForInactive(int userId, DateOnly day, int? daysInactive)
    {
        var (title, body) = Auto[Pick(userId, day, Auto.Length)];
        var span = daysInactive switch
        {
            null or <= 1 => "un día",
            var d => $"{d} días"
        };
        return (title, body.Replace("{days}", span));
    }

    public static (string Title, string Body) FromActor(int userId, DateOnly day, string actorName)
    {
        var (title, body) = FromStaff[Pick(userId, day, FromStaff.Length)];
        return (title.Replace("{actor}", actorName), body.Replace("{actor}", actorName));
    }

    private static int Pick(int userId, DateOnly day, int count) =>
        (int)(PlayService.StableHash(userId, day.DayNumber) % (uint)count);
}
