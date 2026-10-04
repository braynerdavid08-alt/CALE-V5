using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Application;
using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Courses;

/// <summary>The built-in Luz Verde courses. Texts are original; images come from our own signal catalog.</summary>
public sealed partial class CourseSeed
{
    public const string SignsSlug = "senales-transito";
    public const string RulesSlug = "normas-transito";
    public const string SignageSlug = "senalizacion-infraestructura";
    public const string FirstAidSlug = "primeros-auxilios";
    public const string MobilitySlug = "movilidad-segura";
    public const string RoadSlug = "via-espacio-publico";
    public const string VehicleSlug = "el-vehiculo";
    public const string MotorcycleSlug = "motocicleta-a2";
    public const string CarSlug = "automovil-b1";
    public const string PublicServiceSlug = "servicio-publico-c1";

    private readonly CaleDbContext _db;

    public CourseSeed(CaleDbContext db) => _db = db;

    /// <summary>One built-in course: its stable slug, catalog data and lessons in order.</summary>
    public sealed record SeedCourse(
        string Slug,
        string Title,
        string Description,
        string Category,
        string CoverUrl,
        IReadOnlyList<SeedLesson> Lessons);

    /// <summary>A built-in lesson. <see cref="Content"/> is already normalized, exactly as it is stored.</summary>
    public sealed record SeedLesson(string Key, string Title, string Summary, int Minutes, string Content)
    {
        public string Hash => Fingerprint(Title, Summary, Minutes, Content);
    }

    /// <summary>The current Luz Verde curriculum, in catalog order.</summary>
    public static IReadOnlyList<SeedCourse> Curriculum() =>
    [
        Build(
            SignsSlug,
            "Señales de tránsito",
            "Aprende a reconocer las señales reglamentarias, preventivas e informativas de Colombia, cómo se agrupan y en qué orden se obedecen.",
            "Señales de tránsito",
            "/signals/SR-01.svg",
            SignsLessons),
        Build(
            RulesSlug,
            "Normas de tránsito básicas",
            "Velocidades, prelación, adelantamiento, estacionamiento, documentos, alcohol y comparendos, explicados con situaciones reales y actividades interactivas.",
            "Normas de tránsito",
            "/signals/SR-30.svg",
            RulesLessons),
        Build(
            SignageSlug,
            "Señalización vial e infraestructura",
            "Las líneas del pavimento, las marcas en los cruces, los semáforos y los dispositivos que te guían en la vía.",
            "Señales de tránsito",
            LinesImage,
            SignageLessons),
        Build(
            FirstAidSlug,
            "Primeros auxilios en la vía",
            "Qué hacer si eres el primero en llegar a un siniestro: proteger, avisar al 123, valorar a la víctima, controlar sangrados, atender quemaduras y atragantamientos.",
            "Primeros auxilios",
            "/signals/SI-16.svg",
            FirstAidLessons),
        Build(
            MobilitySlug,
            "Movilidad segura y sostenible",
            "Sistema Seguro, víctimas y consecuencias, usuarios vulnerables, conducción preventiva, visibilidad y clima, y movilidad sostenible, con casos de Barranquilla.",
            "Formación vial",
            $"{MobilityImages}/portada.jpg",
            MobilityLessons),
        Build(
            RoadSlug,
            "La vía y el espacio público",
            "Cómo cambia tu conducción según la vía, la posición en el carril, la convivencia con ciclistas y los conflictos en andenes, paraderos y eventos.",
            "Peatones y ciclistas",
            $"{MobilityImages}/anticipate.jpg",
            RoadLessons),
        Build(
            VehicleSlug,
            "El vehículo: conócelo, revísalo y atiéndelo",
            "Sistemas del vehículo, revisión preoperacional, seguridad activa y pasiva, protección de la escena y averías frecuentes.",
            "Vehículo seguro",
            Img("SI-21"),
            VehicleLessons),
        Build(
            MotorcycleSlug,
            "Conducción segura en motocicleta (A2)",
            "Elementos de protección, revisión de la moto, frenado y curvas, posición en el tráfico y puntos ciegos, clima, fatiga, acompañante y carga.",
            "Motociclistas",
            "/courses/moto/portada.jpg",
            MotorcycleLessons),
        Build(
            CarSlug,
            "Dominio seguro del automóvil (B1)",
            "Puesto de conducción, embrague y cambios, frenado, pendientes, reversa, estacionamiento y giros: la técnica para manejar un carro con seguridad.",
            "Vehículo seguro",
            "/courses/automovil/portada.jpg",
            CarLessons),
        Build(
            PublicServiceSlug,
            "Conducción profesional de servicio público (C1)",
            "Régimen y documentos del servicio público, seguros, atención al usuario, pasajeros vulnerables, fatiga y conducción urbana en Barranquilla.",
            "Formación vial",
            "/courses/servicio-publico/portada.jpg",
            PublicServiceLessons)
    ];

    /// <summary>
    /// Creates the built-in courses that do not exist yet. Existing courses are never changed here:
    /// bringing them up to date is an explicit admin action (<see cref="CurriculumSync"/>).
    /// </summary>
    public async Task EnsureAsync(ILogger? logger, CancellationToken ct = default)
    {
        foreach (var seed in Curriculum())
        {
            try
            {
                if (await _db.Set<Course>().AnyAsync(c => c.Slug == seed.Slug && c.SchoolUserId == null, ct))
                {
                    continue;
                }

                var now = DateTime.UtcNow;
                var course = Course.Create(null, 0, seed.Title, seed.Description, seed.Category, seed.CoverUrl, now, seed.Slug);
                _db.Set<Course>().Add(course);
                await _db.SaveChangesAsync(ct);

                for (var i = 0; i < seed.Lessons.Count; i++)
                {
                    _db.Set<CourseLesson>().Add(NewLesson(course.Id, i, seed.Lessons[i], now));
                }

                course.Update(course.Title, course.Description, course.Category, course.CoverUrl, true, now);
                await _db.SaveChangesAsync(ct);
                logger?.LogInformation("Seeded platform course {Slug}", seed.Slug);
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                logger?.LogError(ex, "Could not seed platform course {Slug}", seed.Slug);
            }
        }
    }

    internal static CourseLesson NewLesson(int courseId, int position, SeedLesson seed, DateTime now)
    {
        var lesson = CourseLesson.Create(courseId, position, seed.Title, now);
        lesson.Update(seed.Title, seed.Summary, seed.Minutes, seed.Content, now);
        lesson.MarkSeeded(seed.Key, seed.Hash);
        return lesson;
    }

    /// <summary>Fingerprint of the editable fields of a lesson, as stored.</summary>
    public static string Fingerprint(string title, string? summary, int minutes, string contentJson)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{title}\u001f{summary}\u001f{minutes}\u001f{contentJson}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static SeedCourse Build(
        string slug,
        string title,
        string description,
        string category,
        string coverUrl,
        Func<List<(string Key, string Title, string Summary, int Minutes, object[] Blocks)>> lessons) =>
        new(
            slug,
            title,
            description,
            CourseCategories.Normalize(category),
            coverUrl,
            lessons()
                .Select(l => new SeedLesson(
                    l.Key,
                    l.Title,
                    l.Summary,
                    Math.Clamp(l.Minutes, 1, 240),
                    CourseContent.Normalize(JsonSerializer.SerializeToElement(l.Blocks))))
                .ToList());

    private static string Img(string code) => $"/signals/{code}.svg";

    private static object Text(string title, string body) => new { type = "text", title, body };

    private static object Tip(string body) => new { type = "tip", body };

    private static object Video(string file, string caption) =>
        new { type = "video", url = $"/courses/videos/{file}", caption };

    private static object Sign(string code, string name, string note) => new { code, name, note };

    private static object Quiz(string question, string? imageUrl, string[] options, int correct, string explanation) =>
        new { type = "quiz", question, imageUrl, options, correct, explanation };

    private static object Card(string front, string back, string? imageUrl = null) => new { front, back, imageUrl };

    private static object Pair(string code, string right) => new { left = (string?)null, imageUrl = Img(code), right };

    private static List<(string Key, string Title, string Summary, int Minutes, object[] Blocks)> SignsLessons() =>
    [
        (
            "senales-transito/para-que-sirven",
            "Para qué sirven las señales",
            "Qué es una señal de tránsito, las cuatro formas de señalización, cómo se clasifican y en qué orden se obedecen.",
            12,
            [
                Text(
                    "Un idioma que todos entendemos",
                    "Las señales de tránsito le dicen a cada persona que usa la vía qué debe hacer, qué peligro viene o dónde está lo que busca. "
                    + "Funcionan igual en todo el país, por eso un conductor de Bogotá entiende las mismas señales que uno de la costa.\n\n"
                    + "Se reconocen por tres cosas: la forma, el color y el símbolo. Con práctica, la forma y el color te dicen el tipo de señal antes de que alcances a leerla."),
                Text(
                    "Cuatro herramientas que trabajan juntas",
                    "La señalización vial le habla al conductor de cuatro formas:\n\n"
                    + "Señales verticales: placas en postes o estructuras, al lado o encima de la vía. Son las de este curso.\n\n"
                    + "Señalización horizontal o demarcación: líneas, flechas, símbolos y letras pintados sobre el pavimento y los sardineles.\n\n"
                    + "Semáforos: regulan el paso con luces.\n\n"
                    + "Dispositivos: tachas, delineadores, reductores de velocidad y otros elementos que refuerzan las señales y guían de noche.\n\n"
                    + "Las señales pueden crearse o modificarse según la necesidad del lugar, y en obras o eventos aparecen señales temporales. "
                    + "La demarcación, los semáforos y los dispositivos se estudian a fondo en el curso «Señalización vial e infraestructura»."),
                Classify(
                    "¿A qué tipo de señalización pertenece cada elemento?",
                    ["Vertical", "Horizontal (en el piso)", "Dispositivo"],
                    [
                        ("Placa de PARE en un poste", 0),
                        ("Aviso de velocidad máxima", 0),
                        ("Línea amarilla en el centro de la vía", 1),
                        ("Cebra de paso peatonal", 1),
                        ("Flecha pintada en el carril", 1),
                        ("Tachas reflectivas", 2),
                        ("Resalto o reductor de velocidad", 2)
                    ],
                    "Las verticales están en placas; las horizontales, pintadas en el pavimento; los dispositivos son elementos físicos que refuerzan o guían."),
                Text(
                    "Los tres grupos principales",
                    "Reglamentarias: dan una orden o una prohibición. Casi todas son circulares con borde rojo. Incumplirlas es una infracción.\n\n"
                    + "Preventivas: avisan de un peligro más adelante. Son rombos amarillos con símbolo negro.\n\n"
                    + "Informativas: orientan y muestran servicios. Suelen ser rectangulares, azules o verdes."),
                new
                {
                    type = "signs",
                    title = "Un ejemplo de cada grupo",
                    items = new[]
                    {
                        Sign("SR-01", "Pare", "Reglamentaria: es una orden."),
                        Sign("SP-46A", "Proximidad de cruce peatonal", "Preventiva: avisa de un peligro."),
                        Sign("SI-22", "Estación de servicio", "Informativa: te orienta.")
                    }
                },
                Order(
                    "Cuando no coinciden, ¿cuál manda? Ordena de mayor a menor prioridad.",
                    ["Agente de tránsito", "Semáforo", "Señal vertical", "Marca en el pavimento"],
                    "Siempre manda la indicación más directa y actual: el agente. Después el semáforo, las señales verticales y las demarcaciones."),
                Quiz(
                    "¿Qué forma y color tienen la mayoría de las señales preventivas?",
                    null,
                    ["Círculo blanco con borde rojo", "Rombo amarillo con símbolo negro", "Rectángulo azul", "Octágono rojo"],
                    1,
                    "Las preventivas son rombos amarillos: el color llama la atención para que reduzcas la velocidad y estés alerta."),
                Quiz(
                    "Un semáforo está en verde, pero el agente de tránsito te indica que te detengas. ¿Qué haces?",
                    null,
                    ["Sigo, porque el semáforo está en verde", "Me detengo, porque manda el agente", "Pito para que el agente se aparte", "Sigo despacio"],
                    1,
                    "Las indicaciones del agente están por encima de los semáforos y de cualquier señal.")
            ]
        ),
        (
            "senales-transito/reglamentarias",
            "Señales reglamentarias",
            "Órdenes y prohibiciones: pare, ceda el paso, giros, velocidad y estacionamiento, y cómo se agrupan según lo que hacen.",
            15,
            [
                Text(
                    "Órdenes que se cumplen",
                    "Las señales reglamentarias indican limitaciones, prohibiciones o restricciones. Si no las cumples, cometes una infracción y puedes recibir un comparendo.\n\n"
                    + "Dos de ellas tienen forma propia para reconocerlas incluso de espaldas o cubiertas de barro: el PARE es un octágono rojo y el CEDA EL PASO es un triángulo con un vértice hacia abajo."),
                new
                {
                    type = "signs",
                    title = "Las que más vas a ver",
                    items = new[]
                    {
                        Sign("SR-01", "Pare", "Detén el vehículo por completo antes de la línea y sigue solo cuando la vía esté libre."),
                        Sign("SR-02", "Ceda el paso", "Reduce la velocidad y deja pasar a quien circula por la otra vía; detente si hace falta."),
                        Sign("SR-04", "No pase", "No se puede entrar a esa vía en ese sentido."),
                        Sign("SR-26", "No adelantar", "Está prohibido sobrepasar a otro vehículo en ese tramo."),
                        Sign("SR-30", "Velocidad máxima permitida", "El número indica el límite en kilómetros por hora."),
                        Sign("SR-28", "Prohibido parquear", "No puedes dejar el vehículo estacionado, aunque sí detenerte un momento."),
                        Sign("SR-28A", "Prohibido parquear o detenerse", "Ni estacionar ni detenerse, ni siquiera para recoger a alguien."),
                        Sign("SR-10", "Prohibido girar en U", "No puedes devolverte en ese punto."),
                        Sign("SR-38", "Sentido único de circulación", "Todos los vehículos van en la dirección de la flecha.")
                    }
                },
                Tip("Pare no es lo mismo que ceda el paso: en el PARE siempre hay que detenerse del todo, aunque no venga nadie."),
                Text(
                    "Cómo se agrupan",
                    "Las reglamentarias notifican prioridades, limitaciones, prohibiciones, restricciones, obligaciones y autorizaciones. Según su función, se agrupan en:\n\n"
                    + "Prioridad: PARE y CEDA EL PASO.\n"
                    + "Prohibición de maniobras y giros: no girar, no adelantar, no pase.\n"
                    + "Prohibición de paso por clase de vehículo: carga, motos, bicicletas, buses.\n"
                    + "Obligación: dirección obligada, giro solamente.\n"
                    + "Restricción: velocidad, peso, altura o ancho máximos.\n"
                    + "Otras prohibiciones y autorizaciones: pitar, parquear, zonas de taxi, de cargue y descargue."),
                ClassifySigns(
                    "Clasifica cada señal reglamentaria según lo que hace.",
                    ["Prohíbe una maniobra", "Prohíbe el paso a un vehículo", "Obliga una dirección", "Limita una medida"],
                    [
                        ("SR-06", "Prohibido girar a la izquierda", 0),
                        ("SR-26", "No adelantar", 0),
                        ("SR-23", "Prohibida circulación de motocicletas", 1),
                        ("SR-18", "Prohibida circulación de vehículos de carga", 1),
                        ("SR-03", "Dirección obligada", 2),
                        ("SR-07", "Giro a la derecha solamente", 2),
                        ("SR-31", "Peso máximo permitido", 3),
                        ("SR-32", "Altura máxima permitida", 3)
                    ],
                    "Las de maniobra prohíben una acción; las de clase de vehículo prohíben el paso a ciertos vehículos; las de obligación marcan una sola opción y las de restricción ponen un límite."),
                new
                {
                    type = "flipcards",
                    title = "Toca cada tarjeta para ver qué significa",
                    cards = new[]
                    {
                        Card("Prohibido girar a la izquierda", "No puedes doblar a la izquierda en esa intersección.", Img("SR-06")),
                        Card("Prohibido pitar", "No uses la bocina en esa zona, por ejemplo cerca de hospitales.", Img("SR-29")),
                        Card("Circulación con luces bajas", "Enciende las luces bajas en ese tramo aunque sea de día.", Img("SR-35")),
                        Card("No bloquear intersección", "No entres al cruce si no tienes espacio para salir de él.", Img("SR-47"))
                    }
                },
                Quiz(
                    "¿Qué debes hacer ante esta señal?",
                    Img("SR-01"),
                    ["Reducir la velocidad y seguir si no viene nadie", "Detenerme por completo y seguir cuando la vía esté libre", "Pitar y continuar", "Detenerme solo si hay un agente"],
                    1,
                    "El PARE exige detención total, aunque la vía parezca libre."),
                Quiz(
                    "¿Qué diferencia hay entre «Prohibido parquear» y «Prohibido parquear o detenerse»?",
                    null,
                    ["Ninguna, significan lo mismo", "En la primera puedo detenerme un momento; en la segunda no puedo ni detenerme", "La primera es solo para motos", "La segunda solo aplica de noche"],
                    1,
                    "Con «Prohibido parquear» se permite una detención breve; con «Prohibido parquear o detenerse» no se permite ninguna parada.")
            ]
        ),
        (
            "senales-transito/preventivas",
            "Señales preventivas",
            "Avisos de peligro: curvas, intersecciones, peatones, resaltos y cruces de tren, y cómo se agrupan según lo que anuncian.",
            15,
            [
                Text(
                    "Te avisan antes de llegar",
                    "Las señales preventivas advierten de un peligro o de un cambio en la vía. Se ubican antes del sitio de riesgo para que tengas tiempo de reaccionar.\n\n"
                    + "Ante una preventiva lo correcto es reducir la velocidad, aumentar la atención y prepararte para frenar."),
                new
                {
                    type = "signs",
                    title = "Peligros frecuentes",
                    items = new[]
                    {
                        Sign("SP-01", "Curva cerrada a la izquierda", "Reduce la velocidad antes de entrar a la curva, no dentro de ella."),
                        Sign("SP-11", "Intersección de vías", "Más adelante se cruza otra vía; atento a vehículos que salen."),
                        Sign("SP-20", "Glorieta", "Prepárate para ceder el paso a quien ya circula dentro de la glorieta."),
                        Sign("SP-23", "Semáforo", "Hay un semáforo adelante que puede no verse todavía."),
                        Sign("SP-25", "Proximidad de resalto", "Viene un resalto o policía acostado; frena con tiempo."),
                        Sign("SP-46A", "Proximidad de cruce peatonal", "Puede haber personas cruzando."),
                        Sign("SP-47", "Zona escolar", "Niños cerca de la vía: velocidad muy baja."),
                        Sign("SP-49", "Animales en la vía", "Pueden aparecer animales de forma repentina."),
                        Sign("SP-52", "Cruce ferroviario a nivel sin barrera", "Detente, mira y escucha antes de cruzar la vía férrea.")
                    }
                },
                Tip("Una preventiva no prohíbe nada por sí sola, pero si ocurre un siniestro por ignorarla, tu responsabilidad aumenta."),
                Text(
                    "Cómo se agrupan",
                    "Las preventivas advierten de un riesgo o de una situación imprevista, permanente o temporal. Se agrupan según lo que anuncian: la forma de la vía (curvas), "
                    + "las pendientes, la superficie (resbalosa, rizada, resaltos), las restricciones físicas (puente angosto, altura libre), las intersecciones "
                    + "y la presencia de otros actores (peatones, ciclistas, animales, niños)."),
                Video("ansv-curva-senal-preventiva.mp4", "Una señal preventiva anuncia la curva: reduce la velocidad antes de entrar. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                ClassifySigns(
                    "¿Qué anuncia cada señal preventiva?",
                    ["La forma o pendiente de la vía", "El estado de la superficie", "Una restricción física", "Otros actores en la vía"],
                    [
                        ("SP-02", "Curva cerrada a la derecha", 0),
                        ("SP-27", "Pendiente fuerte de descenso", 0),
                        ("SP-44", "Superficie deslizante", 1),
                        ("SP-24", "Superficie rizada", 1),
                        ("SP-36", "Puente angosto", 2),
                        ("SP-50", "Altura libre", 2),
                        ("SP-59", "Ciclistas en la vía", 3),
                        ("SP-48", "Niños jugando", 3)
                    ],
                    "Agruparlas por lo que anuncian te ayuda a reaccionar: bajar la velocidad, frenar suave, medir tu vehículo o estar atento a personas."),
                new
                {
                    type = "match",
                    instructions = "Une cada señal con lo que anuncia.",
                    pairs = new[]
                    {
                        Pair("SP-29", "Más adelante hay una señal de PARE"),
                        Pair("SP-27", "Viene una bajada fuerte"),
                        Pair("SP-44", "La vía puede estar resbalosa"),
                        Pair("SP-36", "El puente es angosto"),
                        Pair("SP-59", "Hay ciclistas en la vía")
                    }
                },
                Quiz(
                    "¿Qué anuncia esta señal?",
                    Img("SP-25"),
                    ["Fin del pavimento", "Proximidad de un resalto", "Puente levadizo", "Depresión en la vía"],
                    1,
                    "Es la señal de proximidad de resalto: frena antes para no dañar la suspensión ni perder el control."),
                Quiz(
                    "Ves la señal de zona escolar a las 7 de la mañana. ¿Qué es lo más seguro?",
                    Img("SP-47"),
                    ["Mantener la velocidad si no veo niños", "Reducir mucho la velocidad y estar listo para frenar", "Pitar para avisar que paso", "Adelantar rápido para salir de la zona"],
                    1,
                    "En zona escolar los niños pueden salir de repente entre carros estacionados; la velocidad baja te da tiempo para frenar.")
            ]
        ),
        (
            "senales-transito/informativas",
            "Señales informativas",
            "Rutas, direcciones y servicios para orientarte en el camino, y el orden en que aparecen las que te llevan a tu destino.",
            12,
            [
                Text(
                    "Te ayudan a llegar",
                    "Las señales informativas identifican vías, indican destinos y distancias, y muestran dónde hay servicios como hospitales, talleres o estaciones de combustible.\n\n"
                    + "Las de servicios suelen ser azules con un pictograma blanco; las de dirección y destino suelen ser verdes."),
                Text(
                    "Las que te llevan a tu destino",
                    "Aparecen en orden: preseñalización (te avisa con anticipación), dirección (te muestra hacia dónde ir), confirmación (te confirma que vas bien) "
                    + "e identificación de la vía (el número de la ruta). También hay señales de servicios, turísticas y de seguridad vial, como la de radar pedagógico.\n\n"
                    + "Las señales de mensaje variable (SMV) son paneles cuyo texto se cambia en tiempo real para avisarte de un cierre, un trancón, una obra o el clima en tu ruta. "
                    + "Léelas con la misma atención que una señal fija."),
                Order(
                    "Vas por carretera hacia otra ciudad. Ordena las señales informativas en el orden en que las encuentras.",
                    ["Preseñalización: te avisa que se acerca la salida", "Dirección: te indica por dónde tomar", "Confirmación: te confirma el destino y la distancia"],
                    "Primero te preparan, luego te indican el desvío y, una vez en la vía correcta, te confirman que vas bien."),
                Flip(
                    "Informativas que vale la pena conocer",
                    Card("Señal de preseñalización", "Avisa con anticipación los destinos de la próxima intersección o salida.", Img("SI-05D")),
                    Card("Señal de confirmación", "Confirma el destino y la distancia que falta después de un cruce.", Img("SI-06")),
                    Card("Radar pedagógico", "Muestra tu velocidad para que la ajustes; no impone multas.", Img("SI-27B")),
                    Card("Ruta panamericana", "Identifica una vía que hace parte de la red panamericana.", Img("SI-02"))),
                new
                {
                    type = "signs",
                    title = "Algunas informativas",
                    items = new[]
                    {
                        Sign("SI-01", "Ruta nacional", "Identifica el número de la carretera nacional."),
                        Sign("SI-05", "Señal de dirección", "Muestra hacia dónde queda cada destino."),
                        Sign("SI-08", "Paradero de buses", "Sitio donde el transporte público recoge y deja pasajeros."),
                        Sign("SI-16A", "Hospital", "Hay un hospital cerca; conduce con cuidado y sin pitar."),
                        Sign("SI-21", "Taller", "Hay un taller mecánico cerca."),
                        Sign("SI-22", "Estación de servicio", "Puedes cargar combustible."),
                        Sign("SI-25", "Paso o instalación accesible", "Lugar adaptado para personas con discapacidad.")
                    }
                },
                new
                {
                    type = "flipcards",
                    title = "¿Qué servicio indica?",
                    cards = new[]
                    {
                        Card("Primeros auxilios", "Hay un punto de atención básica en salud.", Img("SI-16")),
                        Card("Montallantas", "Puedes reparar o cambiar una llanta.", Img("SI-23")),
                        Card("Estación de carga eléctrica", "Puedes cargar un vehículo eléctrico.", Img("SI-22A")),
                        Card("Vía para ciclistas", "Carril o vía destinada a bicicletas.", Img("SI-11"))
                    }
                },
                Quiz(
                    "¿Qué indica esta señal?",
                    Img("SI-08"),
                    ["Estacionamiento de taxis", "Paradero de buses", "Transporte masivo", "Zona de cargue y descargue"],
                    1,
                    "Es la señal de paradero de buses: los vehículos de servicio público se detienen ahí.")
            ]
        ),
        (
            "senales-transito/repaso",
            "Repaso final",
            "Pon a prueba lo que aprendiste con señales de los tres grupos.",
            10,
            [
                Text(
                    "Antes de empezar",
                    "Responde cada pregunta con calma. Si te equivocas, lee la explicación: en el examen teórico las señales son de las preguntas más frecuentes."),
                new
                {
                    type = "match",
                    instructions = "Une cada señal con su significado.",
                    pairs = new[]
                    {
                        Pair("SR-02", "Ceda el paso"),
                        Pair("SR-26", "No adelantar"),
                        Pair("SP-20", "Glorieta adelante"),
                        Pair("SI-16A", "Hospital"),
                        Pair("SR-29", "Prohibido pitar"),
                        Pair("SP-52A", "Cruce de tren con barrera")
                    }
                },
                Quiz(
                    "¿A qué grupo pertenece esta señal?",
                    Img("SR-30"),
                    ["Preventiva", "Reglamentaria", "Informativa", "Transitoria"],
                    1,
                    "La velocidad máxima es una orden: es reglamentaria, con círculo y borde rojo."),
                Quiz(
                    "¿Qué significa esta señal?",
                    Img("SR-04"),
                    ["No adelantar", "No pase", "Prohibido parquear", "Pare"],
                    1,
                    "La señal de NO PASE indica que no puedes entrar a esa vía en ese sentido."),
                Quiz(
                    "¿Qué te advierte esta señal?",
                    Img("SP-44"),
                    ["Zona de derrumbes", "Superficie deslizante", "Curvas sucesivas", "Final del pavimento"],
                    1,
                    "Superficie deslizante: frena suave, sin giros bruscos, sobre todo con lluvia."),
                Quiz(
                    "Llegas a una intersección con señal de CEDA EL PASO y no viene ningún vehículo. ¿Qué haces?",
                    Img("SR-02"),
                    ["Me detengo siempre por completo", "Reduzco la velocidad y, si la vía está libre, continúo", "Acelero para cruzar rápido", "Pito y sigo"],
                    1,
                    "Ceda el paso obliga a dar prioridad a los demás; si no viene nadie puedes continuar con precaución."),
                Quiz(
                    "¿Cuál es el orden correcto de prioridad?",
                    null,
                    ["Señales, semáforos, agente", "Agente, semáforos, señales verticales, marcas en el piso", "Semáforos, agente, señales", "Marcas en el piso, señales, agente"],
                    1,
                    "Siempre manda primero el agente de tránsito, luego el semáforo, luego las señales verticales y por último las demarcaciones.")
            ]
        )
    ];
}
