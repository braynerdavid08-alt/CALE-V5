using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Application;
using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Courses;

/// <summary>Creates the built-in Luz Verde courses once. Texts are original; images come from our own signal catalog.</summary>
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

    private readonly CaleDbContext _db;

    public CourseSeed(CaleDbContext db) => _db = db;

    public async Task EnsureAsync(ILogger? logger, CancellationToken ct = default)
    {
        await EnsureCourseAsync(
            SignsSlug,
            "Señales de tránsito",
            "Aprende a reconocer las señales reglamentarias, preventivas e informativas de Colombia con ejemplos, tarjetas y preguntas cortas.",
            "Señales de tránsito",
            "/signals/SR-01.svg",
            SignsLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            RulesSlug,
            "Normas de tránsito básicas",
            "Velocidades, prelación, adelantamiento, estacionamiento, documentos, alcohol y comparendos, explicados con situaciones reales y actividades interactivas.",
            "Normas de tránsito",
            "/signals/SR-30.svg",
            RulesLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            SignageSlug,
            "Señalización vial e infraestructura",
            "Las familias de señales verticales, las líneas del pavimento, las marcas en los cruces y los dispositivos que te guían en la vía.",
            "Señales de tránsito",
            LinesImage,
            SignageLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            FirstAidSlug,
            "Primeros auxilios en la vía",
            "Qué hacer si eres el primero en llegar a un siniestro: proteger, avisar al 123, valorar a la víctima, controlar sangrados, atender quemaduras y atragantamientos.",
            "Primeros auxilios",
            "/signals/SI-16.svg",
            FirstAidLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            MobilitySlug,
            "Movilidad segura y sostenible",
            "Sistema Seguro, víctimas y consecuencias, usuarios vulnerables, movilidad sostenible, conducción preventiva y eco-conducción, con casos de Barranquilla.",
            "Seguridad vial",
            $"{MobilityImages}/portada.jpg",
            MobilityLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            RoadSlug,
            "La vía y el espacio público",
            "Cómo cambia tu conducción según la vía, la posición en el carril, la convivencia con ciclistas y los conflictos en andenes, paraderos y eventos.",
            "Seguridad vial",
            $"{MobilityImages}/anticipate.jpg",
            RoadLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            VehicleSlug,
            "El vehículo: conócelo, revísalo y atiéndelo",
            "Sistemas del vehículo, revisión preoperacional, seguridad activa y pasiva, protección de la escena y averías frecuentes.",
            "Mecánica básica",
            Img("SI-21"),
            VehicleLessons,
            logger,
            ct);
        await EnsureCourseAsync(
            MotorcycleSlug,
            "Conducción segura en motocicleta (A2)",
            "Elementos de protección, revisión de la moto, técnicas de frenado y curvas, clima, fatiga, puntos ciegos, acompañante y carga.",
            "Conducción defensiva",
            "/courses/moto/portada.jpg",
            MotorcycleLessons,
            logger,
            ct);
    }

    private async Task EnsureCourseAsync(
        string slug,
        string title,
        string description,
        string category,
        string coverUrl,
        Func<List<(string Title, string Summary, int Minutes, object[] Blocks)>> buildLessons,
        ILogger? logger,
        CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var lessons = buildLessons()
                .Select(l => (l.Title, l.Summary, l.Minutes, Content: CourseContent.Normalize(JsonSerializer.SerializeToElement(l.Blocks))))
                .ToList();

            var existing = await _db.Set<Course>().FirstOrDefaultAsync(c => c.Slug == slug, ct);
            if (existing is not null)
            {
                await UpgradeIfUntouchedAsync(existing, description, lessons, now, logger, ct);
                return;
            }

            var course = Course.Create(null, 0, title, description, category, coverUrl, now, slug);
            _db.Set<Course>().Add(course);
            await _db.SaveChangesAsync(ct);

            for (var i = 0; i < lessons.Count; i++)
            {
                var l = lessons[i];
                var lesson = CourseLesson.Create(course.Id, i, l.Title, now);
                lesson.Update(l.Title, l.Summary, l.Minutes, l.Content, now);
                _db.Set<CourseLesson>().Add(lesson);
            }

            course.Update(course.Title, course.Description, course.Category, course.CoverUrl, true, now);
            await _db.SaveChangesAsync(ct);
            logger?.LogInformation("Seeded platform course {Slug}", slug);
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            logger?.LogError(ex, "Could not seed platform course {Slug}", slug);
        }
    }

    /// <summary>
    /// Brings a seeded course up to date with the current seed, but only while nobody has edited it:
    /// seeding stamps the course and every lesson with the same instant, and every editor action changes the course stamp.
    /// Lessons are updated in place so student progress keeps pointing at them.
    /// </summary>
    private async Task UpgradeIfUntouchedAsync(
        Course course,
        string description,
        List<(string Title, string Summary, int Minutes, string Content)> lessons,
        DateTime now,
        ILogger? logger,
        CancellationToken ct)
    {
        if (!course.IsActive || !course.IsPublished)
        {
            return;
        }

        var stored = await _db.Set<CourseLesson>()
            .Where(l => l.CourseId == course.Id)
            .OrderBy(l => l.Position)
            .ToListAsync(ct);
        if (stored.Any(l => l.UpdatedAt != course.UpdatedAt))
        {
            return;
        }

        var same = stored.Count == lessons.Count
            && course.Description == description
            && stored.Zip(lessons).All(p => p.First.Title == p.Second.Title
                && p.First.Summary == p.Second.Summary
                && p.First.EstimatedMinutes == p.Second.Minutes
                && p.First.ContentJson == p.Second.Content);
        if (same)
        {
            return;
        }

        for (var i = 0; i < lessons.Count; i++)
        {
            var l = lessons[i];
            var lesson = i < stored.Count ? stored[i] : null;
            if (lesson is null)
            {
                lesson = CourseLesson.Create(course.Id, i, l.Title, now);
                _db.Set<CourseLesson>().Add(lesson);
            }

            lesson.MoveTo(i, now);
            lesson.Update(l.Title, l.Summary, l.Minutes, l.Content, now);
        }

        var removed = stored.Skip(lessons.Count).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(l => l.Id).ToList();
            _db.Set<CourseLessonProgress>().RemoveRange(
                await _db.Set<CourseLessonProgress>().Where(p => removedIds.Contains(p.LessonId)).ToListAsync(ct));
            _db.Set<CourseLesson>().RemoveRange(removed);
        }

        course.Update(course.Title, description, course.Category, course.CoverUrl, true, now);
        await _db.SaveChangesAsync(ct);
        logger?.LogInformation("Updated platform course {Slug} to the current seed ({Count} lessons)", course.Slug, lessons.Count);
    }

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

    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> SignsLessons() =>
    [
        (
            "Para qué sirven las señales",
            "Qué es una señal de tránsito, cómo se clasifican y en qué orden se obedecen.",
            8,
            [
                Text(
                    "Un idioma que todos entendemos",
                    "Las señales de tránsito le dicen a cada persona que usa la vía qué debe hacer, qué peligro viene o dónde está lo que busca. "
                    + "Funcionan igual en todo el país, por eso un conductor de Bogotá entiende las mismas señales que uno de la costa.\n\n"
                    + "Se reconocen por tres cosas: la forma, el color y el símbolo. Con práctica, la forma y el color te dicen el tipo de señal antes de que alcances a leerla."),
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
                Tip(
                    "Orden de prioridad: primero el agente de tránsito, luego los semáforos, después las señales verticales y por último las marcas en el pavimento. "
                    + "Si un agente te indica algo distinto a lo que dice una señal, obedece al agente."),
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
            "Señales reglamentarias",
            "Órdenes y prohibiciones: pare, ceda el paso, giros, velocidad y estacionamiento.",
            12,
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
            "Señales preventivas",
            "Avisos de peligro: curvas, intersecciones, peatones, resaltos y cruces de tren.",
            12,
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
                Tip("Una preventiva no prohíbe nada por sí sola, pero si ocurre un accidente por ignorarla, tu responsabilidad aumenta."),
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
            "Señales informativas",
            "Rutas, direcciones y servicios para orientarte en el camino.",
            8,
            [
                Text(
                    "Te ayudan a llegar",
                    "Las señales informativas identifican vías, indican destinos y distancias, y muestran dónde hay servicios como hospitales, talleres o estaciones de combustible.\n\n"
                    + "Las de servicios suelen ser azules con un pictograma blanco; las de dirección y destino suelen ser verdes."),
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
