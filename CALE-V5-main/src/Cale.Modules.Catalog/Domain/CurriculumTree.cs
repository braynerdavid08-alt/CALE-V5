using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Catalog.Domain;

/// <summary>
/// Closed curriculum used to classify question-bank items.
/// Four common nuclei plus the three licence categories. A subtopic points at the
/// existing lesson that teaches it; a null lesson means the subtopic is not covered yet.
/// </summary>
public static class CurriculumTree
{
    public static IReadOnlyList<CurriculumNucleus> Nuclei { get; } = Build();

    public static void EnsureClassifiable(string? subject, string? topic, string? subtopic)
    {
        var nucleus = Clean(subject);
        var theme = Clean(topic);
        var item = Clean(subtopic);
        if (nucleus is null && item is null)
        {
            return;
        }

        if (nucleus is null || theme is null || item is null || !Contains(nucleus, theme, item))
        {
            throw new DomainException(
                "Elige un núcleo, un tema y un subtema de la malla curricular.",
                400,
                "invalid_curriculum");
        }
    }

    public static bool Contains(string nucleus, string theme, string subtopic) =>
        Nuclei.Any(n => Same(n.Name, nucleus)
            && n.Themes.Any(t => Same(t.Name, theme)
                && t.Subtopics.Any(s => Same(s.Name, subtopic))));

    private static bool Same(string left, string right) =>
        left.Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<CurriculumNucleus> Build() =>
    [
        N("movilidad", "Movilidad segura y sostenible",
            T("mov-sistema", "Seguridad vial y Sistema Seguro",
                L("mov-sistema-concepto", "Concepto de seguridad vial", Mov, "Seguridad vial y Sistema Seguro"),
                L("mov-sistema-enfoque", "Enfoque de Sistema Seguro", Mov, "Seguridad vial y Sistema Seguro"),
                L("mov-sistema-responsabilidad", "Responsabilidad compartida", Mov, "Seguridad vial y Sistema Seguro"),
                L("mov-sistema-vias", "Vías, vehículos y velocidades seguras", Mov, "Seguridad vial y Sistema Seguro"),
                L("mov-sistema-vision", "Visión Cero: ninguna muerte en la vía es aceptable", Mov, "Visión Cero: ninguna muerte en la vía es aceptable"),
                L("mov-sistema-tolerancia", "Tolerancia del cuerpo humano al impacto", Mov, "Tolerancia del cuerpo humano al impacto")),
            T("mov-riesgo", "Factores de riesgo",
                L("mov-riesgo-velocidad", "Velocidad como factor de riesgo", Mov, "Conducción preventiva y gestión del riesgo"),
                L("mov-riesgo-alcohol", "Alcohol y sustancias psicoactivas", Nor, "Factores humanos, alcohol y sustancias"),
                L("mov-riesgo-fatiga", "Fatiga y sueño", Nor, "Factores humanos, alcohol y sustancias"),
                L("mov-riesgo-celular", "Distracciones y uso del celular", Nor, "Autorregulación y responsabilidad"),
                L("mov-riesgo-emociones", "Estrés y emociones", Nor, "Factores humanos, alcohol y sustancias"),
                L("mov-riesgo-agresiva", "Conducción agresiva", Nor, "Restricciones urbanas y conducta de los demás"),
                L("mov-riesgo-confianza", "Exceso de confianza", Nor, "Autorregulación y responsabilidad"),
                L("mov-riesgo-presion", "Presión social", Nor, "Autorregulación y responsabilidad"),
                L("mov-riesgo-clima", "Condiciones climáticas", Nor, "Velocidad segura y adaptación al entorno")),
            T("mov-conducta", "Comportamiento del conductor",
                L("mov-conducta-autocontrol", "Autocontrol", Nor, "Autorregulación y responsabilidad"),
                L("mov-conducta-decisiones", "Toma de decisiones", Nor, "Autorregulación y responsabilidad"),
                L("mov-conducta-percepcion", "Percepción del riesgo", Mov, "Conducción preventiva y gestión del riesgo"),
                L("mov-conducta-anticipacion", "Anticipación", Mov, "Conducción preventiva y gestión del riesgo"),
                L("mov-conducta-respeto", "Respeto por los demás actores", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-conducta-defensiva", "Conducción defensiva", Mov, "Conducción preventiva y gestión del riesgo"),
                L("mov-conducta-conflictos", "Manejo de situaciones conflictivas", Nor, "Restricciones urbanas y conducta de los demás"),
                L("mov-conducta-individual", "Responsabilidad individual", Nor, "Autorregulación y responsabilidad")),
            T("mov-actores", "Actores viales vulnerables",
                L("mov-actores-peatones", "Peatones", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-actores-ciclistas", "Ciclistas", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-actores-motos", "Motociclistas", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-actores-ninos", "Niños", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-actores-mayores", "Personas mayores", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-actores-discapacidad", "Personas con discapacidad", Mov, "Usuarios vulnerables, prioridad y convivencia"),
                L("mov-actores-animales", "Animales en la vía", Sig, "Señales preventivas")),
            T("mov-emergencias", "Emergencias y siniestros viales",
                L("mov-emergencias-victimas", "Víctimas y consecuencias de los siniestros", Mov, "Víctimas y consecuencias de los siniestros"),
                L("mov-emergencias-pas", "Protocolo PAS: proteger, avisar y socorrer", Aux, "El primer respondiente"),
                L("mov-emergencias-proteger", "Proteger la escena y los riesgos secundarios", Aux, "Proteger la escena y avisar"),
                L("mov-emergencias-avisar", "Llamada a emergencias y datos que se informan", Aux, "Proteger la escena y avisar"),
                L("mov-emergencias-limites", "Qué no debe hacer quien no está capacitado", Aux, "El primer respondiente"),
                L("mov-emergencias-no-mover", "No mover lesionados sin necesidad", Aux, "El primer respondiente"),
                L("mov-emergencias-valorar", "Valorar a la víctima dentro de las propias capacidades", Aux, "Valorar a la víctima"),
                L("mov-emergencias-incendio", "Actuación frente a un incendio", Veh, "Equipo de prevención y protección de la escena"),
                L("mov-emergencias-derrame", "Actuación ante derrames o peligros en la vía", Aux, "Actuación ante derrames o peligros en la vía")),
            T("mov-sostenible", "Movilidad sostenible",
                L("mov-sostenible-concepto", "Movilidad sostenible y movilidad activa", Mov, "Movilidad sostenible y conducción responsable"),
                L("mov-sostenible-publico", "Transporte público y movilidad compartida", Mov, "Movilidad sostenible y conducción responsable"),
                L("mov-sostenible-uso", "Uso responsable del vehículo", Mov, "Movilidad sostenible y conducción responsable"),
                L("mov-sostenible-eficiente", "Conducción eficiente, consumo y emisiones", Mov, "Conducción eficiente y eco-conducción"))),
        N("normas", "Normas de tránsito",
            T("nor-marco", "Marco normativo",
                L("nor-marco-codigo", "Código Nacional de Tránsito", Nor, "Autoridades de tránsito y el Código"),
                L("nor-marco-autoridades", "Autoridades de tránsito y sus competencias", Nor, "Autoridades de tránsito y el Código"),
                L("nor-marco-derechos", "Derechos y deberes de los usuarios", Nor, "Autoridades de tránsito y el Código")),
            T("nor-actores", "Actores de la vía",
                L("nor-actores-conductor", "Conductor", Nor, "Autorregulación y responsabilidad"),
                L("nor-actores-peaton", "Peatón", Nor, "Prelación: quién pasa primero"),
                L("nor-actores-ciclista", "Ciclista", Nor, "Prelación: quién pasa primero"),
                L("nor-actores-motociclista", "Motociclista", Nor, "Prelación: quién pasa primero"),
                L("nor-actores-pasajero", "Pasajero", Veh, "Seguridad activa, pasiva y asistencias"),
                L("nor-actores-agente", "Agente de tránsito", Nor, "Autoridades de tránsito y el Código")),
            T("nor-documentos", "Documentación",
                L("nor-documentos-licencia", "Licencia de conducción", Nor, "Documentos y habilitación para circular"),
                L("nor-documentos-transito", "Licencia de tránsito", Nor, "Documentos y habilitación para circular"),
                L("nor-documentos-soat", "SOAT", Nor, "Documentos y habilitación para circular"),
                L("nor-documentos-rtm", "Revisión técnico-mecánica", Nor, "Documentos y habilitación para circular"),
                L("nor-documentos-vigencia", "Documentos exigibles y su vigencia", Nor, "Documentos y habilitación para circular")),
            T("nor-circulacion", "Reglas de circulación",
                L("nor-circulacion-sentido", "Sentido de circulación, calzada y carriles", Nor, "Adelantar, carriles y estacionamiento"),
                L("nor-circulacion-estacionar", "Estacionamiento", Nor, "Adelantar, carriles y estacionamiento")),
            T("nor-prelacion", "Prelación",
                L("nor-prelacion-interseccion", "Intersecciones señalizadas y sin señalizar", Nor, "Prelación: quién pasa primero"),
                L("nor-prelacion-glorieta", "Glorietas", Nor, "Prelación: quién pasa primero"),
                L("nor-prelacion-emergencia", "Vehículos de emergencia", Nor, "Prelación: quién pasa primero"),
                L("nor-prelacion-peaton", "Peatones y ciclistas", Nor, "Prelación: quién pasa primero"),
                L("nor-prelacion-senales", "Señales que modifican la prelación", Nor, "Prelación: quién pasa primero")),
            T("nor-velocidad", "Velocidad",
                L("nor-velocidad-limites", "Límites de velocidad", Nor, "Velocidad segura y adaptación al entorno"),
                L("nor-velocidad-segura", "Velocidad segura y condiciones de la vía", Nor, "Velocidad segura y adaptación al entorno"),
                L("nor-velocidad-distancias", "Distancia de reacción y de frenado", Nor, "Velocidad segura y adaptación al entorno"),
                L("nor-velocidad-zonas", "Zonas especiales y consecuencias del exceso", Nor, "Velocidad segura y adaptación al entorno")),
            T("nor-maniobras", "Maniobras",
                L("nor-maniobras-adelantar", "Adelantamiento", Nor, "Adelantar, carriles y estacionamiento"),
                L("nor-maniobras-carril", "Cambio de carril e incorporación", Via, "Posición en el carril e incorporaciones"),
                L("nor-maniobras-giros", "Giros", B1, "Giros e intersecciones con el automóvil"),
                L("nor-maniobras-reversa", "Reversa", B1, "Reversa y estacionamiento"),
                L("nor-maniobras-arranque", "Arranque y detención", B1, "Embrague, cambios, arranque y detención"),
                L("nor-maniobras-pendiente", "Conducción en pendientes", B1, "Frenado y pendientes")),
            T("nor-sanciones", "Infracciones y sanciones",
                L("nor-sanciones-comparendo", "Comparendos e infracciones", Nor, "Infracciones y comparendos"),
                L("nor-sanciones-medidas", "Inmovilización, suspensión y cancelación", Nor, "Infracciones y comparendos"),
                L("nor-sanciones-responsabilidad", "Responsabilidad del conductor", Nor, "Infracciones y comparendos")),
            T("nor-pasajeros", "Transporte de pasajeros y carga",
                L("nor-pasajeros-cinturon", "Número de pasajeros y cinturones", Veh, "Seguridad activa, pasiva y asistencias"),
                L("nor-pasajeros-trato", "Comportamiento con los pasajeros", Veh, "Equipo de prevención y protección de la escena"),
                L("nor-pasajeros-carga", "Dimensiones, pesos y elementos de seguridad de la carga", Nor, "Dimensiones, pesos y elementos de seguridad de la carga")),
            T("nor-urbano", "Restricciones urbanas",
                L("nor-urbano-medidas", "Medidas locales y cómo verificarlas", Nor, "Restricciones urbanas y conducta de los demás"),
                L("nor-urbano-conducta", "Conductas seguras e inseguras de los demás", Nor, "Restricciones urbanas y conducta de los demás"))),
        N("senalizacion", "Señalización e infraestructura vial",
            T("sen-verticales", "Señales verticales",
                L("sen-verticales-familias", "Reglamentarias, preventivas, informativas y transitorias", Inf, "Las familias de señales verticales"),
                L("sen-verticales-forma", "Colores, formas y significado de cada grupo", Inf, "El sistema de señalización")),
            T("sen-reglamentarias", "Señales reglamentarias",
                L("sen-reg-pare", "PARE y ceda el paso", Sig, "Señales reglamentarias"),
                L("sen-reg-prohibiciones", "Prohibiciones, restricciones y obligaciones", Sig, "Señales reglamentarias"),
                L("sen-reg-limites", "Límites, giros, adelantamiento y estacionamiento", Sig, "Señales reglamentarias")),
            T("sen-preventivas", "Señales preventivas",
                L("sen-prev-curvas", "Curvas, intersecciones y pendientes", Sig, "Señales preventivas"),
                L("sen-prev-cruces", "Cruces, peatones, animales y obras", Sig, "Señales preventivas")),
            T("sen-informativas", "Señales informativas",
                L("sen-inf-destinos", "Destinos, servicios y sitios de interés", Sig, "Señales informativas"),
                L("sen-inf-rutas", "Rutas e identificación de vías", Sig, "Señales informativas")),
            T("sen-transitorias", "Señalización transitoria",
                L("sen-trans-obras", "Obras, desvíos y reducción de carriles", Inf, "Marcas transversales, símbolos y dispositivos"),
                L("sen-trans-dispositivos", "Conos, barreras y dispositivos temporales", Inf, "Marcas transversales, símbolos y dispositivos")),
            T("sen-horizontal", "Señalización horizontal",
                L("sen-hor-lineas", "Líneas continuas, discontinuas y dobles", Inf, "Líneas en el centro y en los bordes"),
                L("sen-hor-marcas", "Cebras, flechas, símbolos y otras marcas", Inf, "Marcas transversales, símbolos y dispositivos")),
            T("sen-semaforos", "Semáforos y dispositivos luminosos",
                L("sen-sem-prioridad", "Prioridad del semáforo frente a otras señales", Inf, "El sistema de señalización"),
                L("sen-sem-fases", "Fases del semáforo vehicular y peatonal", Inf, "Fases del semáforo vehicular y peatonal")),
            T("sen-infra", "Infraestructura vial",
                L("sen-infra-via", "Calzada, carril, berma, andén y separador", Via, "La vía, su función y sus riesgos"),
                L("sen-infra-ciclo", "Ciclovía y convivencia con ciclistas", Via, "Cicloinfraestructura y convivencia con ciclistas"),
                L("sen-infra-nodos", "Intersecciones y glorietas", Via, "La vía, su función y sus riesgos"),
                L("sen-infra-espacio", "Espacio público y conflictos de movilidad", Via, "Espacio público y conflictos de movilidad"),
                L("sen-infra-obras", "Puentes, túneles y cunetas", Via, "Puentes, túneles y cunetas")),
            T("sen-entorno", "Lectura del entorno",
                L("sen-entorno-peligros", "Identificación de peligros y anticipación", Mov, "Conducción preventiva y gestión del riesgo"),
                L("sen-entorno-ciegos", "Puntos ciegos y distancia visual", A2, "Lluvia, calor, fatiga y puntos ciegos"),
                L("sen-entorno-via", "Estado de la vía, iluminación, clima y tráfico", Nor, "Velocidad segura y adaptación al entorno"))),
        N("vehiculo", "El vehículo",
            T("veh-general", "Conocimiento general del vehículo",
                L("veh-general-sistemas", "Motor, transmisión, dirección, suspensión y frenos", Veh, "Reconocimiento y funcionamiento del vehículo"),
                L("veh-general-electrico", "Sistema eléctrico, carrocería e instrumentación", Veh, "Reconocimiento y funcionamiento del vehículo")),
            T("veh-preop", "Revisión preoperacional",
                L("veh-preop-recorrido", "Estado general, luces, espejos, vidrios, bocina y placas", Veh, "Inspección preoperacional"),
                L("veh-preop-fugas", "Fugas y cinturones", Veh, "Inspección preoperacional")),
            T("veh-llantas", "Llantas",
                L("veh-llantas-estado", "Presión, desgaste, labrado y daños", Veh, "Inspección preoperacional"),
                L("veh-llantas-repuesto", "Llanta de repuesto y riesgo de baja presión", Veh, "Inspección preoperacional"),
                L("veh-llantas-reventon", "Reventón", Veh, "Averías frecuentes e inmovilización segura")),
            T("veh-frenos", "Sistema de frenos",
                L("veh-frenos-tipos", "Freno de servicio, de estacionamiento, pastillas y líquido", Veh, "Reconocimiento y funcionamiento del vehículo"),
                L("veh-frenos-abs", "ABS y señales de falla", Veh, "Seguridad activa, pasiva y asistencias")),
            T("veh-fluidos", "Motor y fluidos",
                L("veh-fluidos-niveles", "Aceite, refrigerante, combustible y sobrecalentamiento", Veh, "Reconocimiento y funcionamiento del vehículo"),
                L("veh-fluidos-fugas", "Cómo reconocer una fuga", Veh, "Averías frecuentes e inmovilización segura")),
            T("veh-electronica", "Sistema eléctrico y electrónico",
                L("veh-elec-bateria", "Batería, alternador, fusibles y arranque", Veh, "Averías frecuentes e inmovilización segura"),
                L("veh-elec-testigos", "Testigos del tablero", Veh, "Reconocimiento y funcionamiento del vehículo")),
            T("veh-activa", "Seguridad activa",
                L("veh-activa-asistencias", "ABS, estabilidad, tracción y asistencia de frenado", Veh, "Seguridad activa, pasiva y asistencias"),
                L("veh-activa-luces", "Iluminación y neumáticos", Veh, "Seguridad activa, pasiva y asistencias")),
            T("veh-pasiva", "Seguridad pasiva",
                L("veh-pasiva-ocupantes", "Cinturón, airbag, apoyacabezas y sillas infantiles", Veh, "Seguridad activa, pasiva y asistencias"),
                L("veh-pasiva-casco", "Casco en motocicleta", A2, "Antes de salir: elementos de protección y alistamiento")),
            T("veh-mantenimiento", "Mantenimiento preventivo",
                L("veh-mant-diario", "Qué revisar antes de salir", Veh, "Inspección preoperacional"),
                L("veh-mant-periodico", "Mantenimiento y revisión técnico-mecánica", Veh, "Inspección preoperacional")),
            T("veh-escena", "Averías e inmovilización",
                L("veh-escena-decision", "Cuándo continuar y cuándo detenerse", Veh, "Averías frecuentes e inmovilización segura"),
                L("veh-escena-proteger", "Proteger la escena con el equipo de carretera", Veh, "Equipo de prevención y protección de la escena"))),
        N("a2", "Motocicleta (A2)",
            T("a2-proteccion", "Elementos de protección y alistamiento",
                L("a2-proteccion-epp", "Casco y elementos de protección", A2, "Antes de salir: elementos de protección y alistamiento"),
                L("a2-proteccion-documentos", "Documentos y kit de la moto", A2, "Antes de salir: elementos de protección y alistamiento"),
                L("a2-proteccion-postura", "Postura y preparación del cuerpo", A2, "Antes de salir: elementos de protección y alistamiento")),
            T("a2-revision", "Revisión de la motocicleta",
                L("a2-revision-partes", "Llantas, frenos, luces, cadena y chasis", A2, "Revisión preoperacional de la motocicleta")),
            T("a2-tecnica", "Técnicas de manejo",
                L("a2-tecnica-freno", "Frenado progresivo y de emergencia", A2, "Técnicas de manejo: frenado y curvas"),
                L("a2-tecnica-curvas", "Curvas y obstáculos", A2, "Técnicas de manejo: frenado y curvas")),
            T("a2-riesgo", "Clima, fatiga y visibilidad",
                L("a2-riesgo-clima", "Lluvia, calor y fatiga", A2, "Lluvia, calor, fatiga y puntos ciegos"),
                L("a2-riesgo-ciegos", "Puntos ciegos y vehículos pesados", A2, "Lluvia, calor, fatiga y puntos ciegos")),
            T("a2-carga", "Acompañante y carga",
                L("a2-carga-acompanante", "Acompañante", A2, "Acompañante, carga y fin del recorrido"),
                L("a2-carga-peso", "Carga y fin del recorrido", A2, "Acompañante, carga y fin del recorrido"))),
        N("b1", "Automóvil (B1)",
            T("b1-puesto", "Puesto de conducción y mandos",
                L("b1-puesto-ajuste", "Asiento, volante, espejos y cinturón", B1, "Puesto de conducción y mandos"),
                L("b1-puesto-mandos", "Pedales y demás mandos", B1, "Puesto de conducción y mandos")),
            T("b1-cambios", "Embrague, cambios, arranque y detención",
                L("b1-cambios-mecanico", "Punto de fricción, cambios y detención", B1, "Embrague, cambios, arranque y detención"),
                L("b1-cambios-automatico", "Carro automático", B1, "Embrague, cambios, arranque y detención")),
            T("b1-frenos", "Frenado y pendientes",
                L("b1-frenos-tecnica", "Frenado progresivo y de emergencia", B1, "Frenado y pendientes"),
                L("b1-frenos-subida", "Arranque en subida", B1, "Frenado y pendientes"),
                L("b1-frenos-estacionar", "Estacionar en pendiente", B1, "Frenado y pendientes")),
            T("b1-parqueo", "Reversa y estacionamiento",
                L("b1-parqueo-reversa", "Reversa", B1, "Reversa y estacionamiento"),
                L("b1-parqueo-paralelo", "Estacionamiento en paralelo, en batería y en reversa", B1, "Reversa y estacionamiento")),
            T("b1-giros", "Giros e intersecciones",
                L("b1-giros-tipos", "Giro a la derecha, a la izquierda y en U", B1, "Giros e intersecciones con el automóvil"),
                L("b1-giros-glorieta", "Cómo circular en una glorieta", B1, "Giros e intersecciones con el automóvil"))),
        N("c1", "Servicio público (C1)",
            T("c1-regimen", "Régimen, documentos y seguros",
                L("c1-regimen-servicio", "Régimen del servicio público", C1, "Régimen del servicio público, documentos y seguros"),
                L("c1-regimen-documentos", "Tarjeta de operación y licencias C1, C2 y C3", C1, "Régimen del servicio público, documentos y seguros"),
                L("c1-regimen-seguros", "SOAT y pólizas de responsabilidad civil", C1, "Régimen del servicio público, documentos y seguros")),
            T("c1-usuario", "Atención al usuario",
                L("c1-usuario-trato", "Trato, información y derechos del pasajero", C1, "Atención al usuario y resolución de conflictos"),
                L("c1-usuario-conflicto", "Resolución de conflictos", C1, "Atención al usuario y resolución de conflictos")),
            T("c1-acceso", "Pasajeros vulnerables y ascenso",
                L("c1-acceso-discapacidad", "Personas con discapacidad y perro guía", C1, "Pasajeros con discapacidad, usuarios vulnerables y ascenso seguro"),
                L("c1-acceso-ascenso", "Ascenso y descenso por el andén", C1, "Pasajeros con discapacidad, usuarios vulnerables y ascenso seguro")),
            T("c1-fatiga", "Fatiga y presión por tiempos",
                L("c1-fatiga-sueno", "Fatiga, somnolencia y microsueños", C1, "Fatiga, somnolencia y presión por tiempos"),
                L("c1-fatiga-tiempos", "Presión por tiempos", C1, "Fatiga, somnolencia y presión por tiempos")),
            T("c1-ciudad", "Conducción urbana",
                L("c1-ciudad-rutas", "Rutas, ejes viales y desvíos en Barranquilla", C1, "Conducción urbana intensiva y rutas en Barranquilla"),
                L("c1-ciudad-transmetro", "Convivencia con Transmetro y el transporte público", C1, "Conducción urbana intensiva y rutas en Barranquilla")))
    ];

    private const string Mov = "movilidad-segura";
    private const string Nor = "normas-transito";
    private const string Sig = "senales-transito";
    private const string Inf = "senalizacion-infraestructura";
    private const string Aux = "primeros-auxilios";
    private const string Via = "via-espacio-publico";
    private const string Veh = "el-vehiculo";
    private const string A2 = "motocicleta-a2";
    private const string B1 = "automovil-b1";
    private const string C1 = "servicio-publico-c1";

    private static CurriculumNucleus N(string id, string name, params CurriculumTheme[] themes) =>
        new(id, name, themes);

    private static CurriculumTheme T(string id, string name, params CurriculumSubtopic[] subtopics) =>
        new(id, name, subtopics);

    private static CurriculumSubtopic L(string id, string name, string courseSlug, string lessonTitle) =>
        new(id, name, courseSlug, CourseTitle(courseSlug), lessonTitle);

    private static CurriculumSubtopic G(string id, string name) =>
        new(id, name, null, null, null);

    private static string CourseTitle(string slug) => slug switch
    {
        Mov => "Movilidad segura y sostenible",
        Nor => "Normas de tránsito básicas",
        Sig => "Señales de tránsito",
        Inf => "Señalización vial e infraestructura",
        Aux => "Primeros auxilios en la vía",
        Via => "La vía y el espacio público",
        Veh => "El vehículo: conócelo, revísalo y atiéndelo",
        A2 => "Conducción segura en motocicleta (A2)",
        B1 => "Dominio seguro del automóvil (B1)",
        C1 => "Conducción profesional de servicio público (C1)",
        _ => slug
    };
}

public sealed record CurriculumNucleus(string Id, string Name, IReadOnlyList<CurriculumTheme> Themes);

public sealed record CurriculumTheme(string Id, string Name, IReadOnlyList<CurriculumSubtopic> Subtopics);

public sealed record CurriculumSubtopic(
    string Id,
    string Name,
    string? CourseSlug,
    string? CourseTitle,
    string? LessonTitle);
