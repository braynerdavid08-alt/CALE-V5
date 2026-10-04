namespace Cale.Api.Services.Courses;

/// <summary>
/// How lessons stored before seed keys existed map to a key, and which keys left the curriculum.
/// Lessons were matched by position until October 2026, so the stored title is the only reliable link.
/// </summary>
public sealed partial class CourseSeed
{
    /// <summary>Keys that are no longer in the curriculum, with the lessons that absorbed their content.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> RetiredLessons = new Dictionary<string, string[]>
    {
        [$"{SignageSlug}/sistema"] = [$"{SignsSlug}/para-que-sirven"],
        [$"{SignageSlug}/familias"] = [$"{SignsSlug}/reglamentarias", $"{SignsSlug}/preventivas", $"{SignsSlug}/informativas"],
        [$"{MobilitySlug}/vision-cero"] = [$"{MobilitySlug}/sistema-seguro"],
        [$"{MobilitySlug}/tolerancia"] = [$"{MobilitySlug}/victimas"],
        [$"{MobilitySlug}/eco-conduccion"] = [$"{MobilitySlug}/movilidad-sostenible"],
        [$"{RulesSlug}/restricciones"] = [$"{RulesSlug}/documentos", $"{MobilitySlug}/conduccion-preventiva"]
    };

    private static readonly Dictionary<(string Slug, string Title), string> FormerTitles = new()
    {
        [(SignsSlug, "Para qué sirven las señales")] = "para-que-sirven",
        [(SignsSlug, "Señales reglamentarias")] = "reglamentarias",
        [(SignsSlug, "Señales preventivas")] = "preventivas",
        [(SignsSlug, "Señales informativas")] = "informativas",
        [(SignsSlug, "Repaso final")] = "repaso",
        [(RulesSlug, "Autorregulación y responsabilidad")] = "autorregulacion",
        [(RulesSlug, "Autoridades de tránsito y el Código")] = "autoridades",
        [(RulesSlug, "Documentos y habilitación para circular")] = "documentos",
        [(RulesSlug, "Velocidad segura y adaptación al entorno")] = "velocidad",
        [(RulesSlug, "Prelación: quién pasa primero")] = "prelacion",
        [(RulesSlug, "Adelantar, carriles y estacionamiento")] = "adelantar",
        [(RulesSlug, "Restricciones urbanas y conducta de los demás")] = "restricciones",
        [(RulesSlug, "Factores humanos, alcohol y sustancias")] = "factores-humanos",
        [(RulesSlug, "Dimensiones, pesos y elementos de seguridad de la carga")] = "carga",
        [(RulesSlug, "Infracciones y comparendos")] = "infracciones",
        [(SignageSlug, "El sistema de señalización")] = "sistema",
        [(SignageSlug, "Las familias de señales verticales")] = "familias",
        [(SignageSlug, "Líneas en el centro y en los bordes")] = "lineas",
        [(SignageSlug, "Marcas transversales, símbolos y dispositivos")] = "marcas",
        [(SignageSlug, "Fases del semáforo vehicular y peatonal")] = "semaforos",
        [(SignageSlug, "Repaso final")] = "repaso",
        [(FirstAidSlug, "El primer respondiente")] = "primer-respondiente",
        [(FirstAidSlug, "Proteger la escena y avisar")] = "proteger-avisar",
        [(FirstAidSlug, "Valorar a la víctima")] = "valorar",
        [(FirstAidSlug, "Heridas y hemorragias")] = "hemorragias",
        [(FirstAidSlug, "Quemaduras")] = "quemaduras",
        [(FirstAidSlug, "Atragantamiento: maniobra de Heimlich")] = "atragantamiento",
        [(FirstAidSlug, "Actuación ante derrames o peligros en la vía")] = "derrames",
        [(MobilitySlug, "Seguridad vial y Sistema Seguro")] = "sistema-seguro",
        [(MobilitySlug, "Visión Cero: ninguna muerte en la vía es aceptable")] = "vision-cero",
        [(MobilitySlug, "Tolerancia del cuerpo humano al impacto")] = "tolerancia",
        [(MobilitySlug, "Víctimas y consecuencias de los siniestros")] = "victimas",
        [(MobilitySlug, "Usuarios vulnerables, prioridad y convivencia")] = "usuarios-vulnerables",
        [(MobilitySlug, "Movilidad sostenible y conducción responsable")] = "movilidad-sostenible",
        [(MobilitySlug, "Conducción preventiva y gestión del riesgo")] = "conduccion-preventiva",
        [(MobilitySlug, "Conducción eficiente y eco-conducción")] = "eco-conduccion",
        [(RoadSlug, "La vía, su función y sus riesgos")] = "funcion-riesgos",
        [(RoadSlug, "Posición en el carril e incorporaciones")] = "posicion-carril",
        [(RoadSlug, "Cicloinfraestructura y convivencia con ciclistas")] = "ciclistas",
        [(RoadSlug, "Espacio público y conflictos de movilidad")] = "espacio-publico",
        [(RoadSlug, "Puentes, túneles y cunetas")] = "puentes-tuneles",
        [(VehicleSlug, "Reconocimiento y funcionamiento del vehículo")] = "sistemas",
        [(VehicleSlug, "Inspección preoperacional")] = "preoperacional",
        [(VehicleSlug, "Seguridad activa, pasiva y asistencias")] = "seguridad-activa-pasiva",
        [(VehicleSlug, "Equipo de prevención y protección de la escena")] = "equipo-escena",
        [(VehicleSlug, "Averías frecuentes e inmovilización segura")] = "averias",
        [(MotorcycleSlug, "Antes de salir: elementos de protección y alistamiento")] = "proteccion-alistamiento",
        [(MotorcycleSlug, "Revisión preoperacional de la motocicleta")] = "preoperacional",
        [(MotorcycleSlug, "Técnicas de manejo: frenado y curvas")] = "frenado-curvas",
        [(MotorcycleSlug, "Lluvia, calor, fatiga y puntos ciegos")] = "clima-fatiga",
        [(MotorcycleSlug, "Acompañante, carga y fin del recorrido")] = "acompanante-carga",
        [(CarSlug, "Puesto de conducción y mandos")] = "puesto-mandos",
        [(CarSlug, "Embrague, cambios, arranque y detención")] = "embrague-cambios",
        [(CarSlug, "Frenado y pendientes")] = "frenado-pendientes",
        [(CarSlug, "Reversa y estacionamiento")] = "reversa-estacionamiento",
        [(CarSlug, "Giros e intersecciones con el automóvil")] = "giros",
        [(PublicServiceSlug, "Régimen del servicio público, documentos y seguros")] = "regimen-documentos",
        [(PublicServiceSlug, "Atención al usuario y resolución de conflictos")] = "atencion-usuario",
        [(PublicServiceSlug, "Pasajeros con discapacidad, usuarios vulnerables y ascenso seguro")] = "pasajeros-vulnerables",
        [(PublicServiceSlug, "Fatiga, somnolencia y presión por tiempos")] = "fatiga",
        [(PublicServiceSlug, "Conducción urbana intensiva y rutas en Barranquilla")] = "conduccion-urbana"
    };

    private static readonly Lazy<Dictionary<(string Slug, string Title), string>> CurrentTitles = new(() =>
        Curriculum()
            .SelectMany(c => c.Lessons.Select(l => (c.Slug, l.Title, l.Key)))
            .ToDictionary(x => (x.Slug, x.Title), x => x.Key));

    /// <summary>Key for a lesson stored without one, from its course and title; null if the title is unknown.</summary>
    public static string? LegacyKey(string slug, string title)
    {
        if (CurrentTitles.Value.TryGetValue((slug, title), out var key))
        {
            return key;
        }

        return FormerTitles.TryGetValue((slug, title), out var shortKey) ? $"{slug}/{shortKey}" : null;
    }
}
