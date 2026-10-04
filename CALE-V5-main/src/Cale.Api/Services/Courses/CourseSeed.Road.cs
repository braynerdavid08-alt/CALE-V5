namespace Cale.Api.Services.Courses;

/// <summary>
/// "La vía y el espacio público": núcleo 1-A-3 of the school's curriculum (temas B, C, D y E; el tema A está en los cursos de señales).
/// </summary>
public sealed partial class CourseSeed
{
    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> RoadLessons() =>
    [
        (
            "La vía, su función y sus riesgos",
            "Vías arterias, colectoras y locales; curvas, pendientes, intersecciones, obras y zonas escolares: cómo cambia tu conducta según la vía.",
            14,
            [
                Text(
                    "Cada vía tiene una función",
                    "No todas las vías sirven para lo mismo, y tu forma de conducir debe cambiar según su función:\n\n"
                    + "Vías arterias: mueven grandes volúmenes de tráfico entre sectores de la ciudad, con velocidades más altas y varios carriles. En Barranquilla, la Circunvalar, la Vía 40 o la Avenida Murillo.\n\n"
                    + "Vías colectoras: recogen el tráfico de los barrios y lo llevan a las arterias. Tienen más cruces, paraderos y comercio.\n\n"
                    + "Vías locales: dan acceso a las casas. Hay niños, peatones, carros estacionados y entradas de garaje; aquí la velocidad debe ser baja.\n\n"
                    + "En carretera, las vías se clasifican en primarias, secundarias y terciarias, según conecten regiones, municipios o veredas."),
                Picture($"{MobilityImages}/anticipate.jpg", "Una intersección de alto flujo en Barranquilla: muchos actores y muchas decisiones al mismo tiempo."),
                Classify(
                    "¿A qué tipo de vía urbana corresponde cada descripción?",
                    ["Arteria", "Colectora", "Local"],
                    [
                        ("Corredor de varios carriles que atraviesa la ciudad", 0),
                        ("Vía de alta velocidad con intercambiadores y puentes peatonales", 0),
                        ("Calle con paraderos y comercio que lleva el tráfico del barrio a la avenida", 1),
                        ("Vía que reúne el tráfico de varias calles residenciales", 1),
                        ("Calle residencial con niños jugando y carros parqueados", 2),
                        ("Calle de acceso a un conjunto de casas", 2)
                    ],
                    "Las arterias mueven mucho tráfico rápido; las colectoras lo distribuyen; las locales dan acceso y son las más compartidas con peatones."),
                Text(
                    "La geometría de la vía",
                    "Curvas: la fuerza que te empuja hacia afuera crece con la velocidad. Reduce antes de entrar, nunca dentro de la curva, y no invadas el carril contrario.\n\n"
                    + "Pendientes: en bajada el vehículo gana velocidad solo y los frenos trabajan más; usa una marcha baja para aprovechar el freno de motor. En subida, cuidado con quien viene detrás y con arrancar sin devolverte.\n\n"
                    + "Intersecciones: es donde se cruzan las trayectorias y donde ocurren muchos siniestros. Llega despacio, mira a ambos lados y anticipa a quien no respete la prelación.\n\n"
                    + "Superficie: huecos, parches, arena, aceite, tapas de alcantarilla y pintura mojada reducen el agarre."),
                Flip(
                    "Cómo adaptar tu conducción",
                    Card("Curva cerrada", "Frena antes de entrar, en línea recta. Dentro de la curva mantén una velocidad constante y mira hacia la salida."),
                    Card("Bajada larga", "Usa una marcha baja y frena por tramos. Si frenas todo el tiempo, los frenos se recalientan y pierden fuerza."),
                    Card("Intersección sin semáforo", "Reduce, mira a la izquierda, a la derecha y otra vez a la izquierda. La prelación no te protege de quien no la respeta."),
                    Card("Obra en la vía", "Sigue la señalización temporal y las indicaciones del personal. Reduce: hay trabajadores, maquinaria y cambios de carril."),
                    Card("Zona escolar o residencial", "Circula a 30 km/h o menos y espera que un niño salga entre los carros parqueados.")),
                Text(
                    "Una ciudad que cambia",
                    "Barranquilla tiene intervenciones frecuentes: nuevos puentes y pasos peatonales en la Circunvalar, intercambiadores, obras de canalización de arroyos y cierres temporales. "
                    + "Por eso el conductor no debe memorizar rutas, sino aprender a leer la vía: su función, su estado y su señalización del día."),
                Signs(
                    "Señales que te anuncian la vía",
                    Sign("SP-02", "Curva cerrada a la derecha", "Reduce la velocidad antes de la curva."),
                    Sign("SP-27", "Pendiente fuerte de descenso", "Usa una marcha baja y frena por tramos."),
                    Sign("SP-44", "Superficie deslizante", "Evita frenadas y giros bruscos."),
                    Sign("SP-47", "Zona escolar", "Circula a 30 km/h o menos.")),
                Scenario(
                    "Bajas por una vía con pendiente pronunciada y curvas. Notas que el pedal del freno se siente más blando y el carro no frena igual que al principio. ¿Qué haces?",
                    [
                        Choice("Sigo frenando con más fuerza hasta llegar abajo.", "Los frenos recalentados pierden eficacia: seguir abusando de ellos puede dejarte sin frenos en la siguiente curva."),
                        Choice("Bajo a una marcha más corta, freno por tramos y, si puedo, me detengo en un lugar seguro para que se enfríen.", "Correcto: el freno de motor alivia los frenos y una parada permite que se recuperen.", true),
                        Choice("Pongo neutro para que el carro ruede libre.", "En neutro pierdes el freno de motor y el vehículo gana velocidad aún más rápido.")
                    ]),
                Quiz(
                    "¿En qué tipo de vía debe ser más baja tu velocidad por la presencia de niños, peatones y carros parqueados?",
                    null,
                    ["Vía arteria", "Vía colectora", "Vía local o residencial", "Carretera primaria"],
                    2,
                    "En las vías locales la mayoría de los usuarios son peatones y vecinos. La velocidad segura es baja aunque no haya una señal que lo diga.")
            ]
        ),
        (
            "Posición en el carril e incorporaciones",
            "Elegir el carril correcto, cambiar de carril con la secuencia segura e incorporarte sin crear conflictos de trayectoria.",
            13,
            [
                Text(
                    "Tres tipos de carril",
                    "Carril de tráfico mixto: lo comparten carros, motos, buses y bicicletas.\n\n"
                    + "Carril preferencial: da prioridad a un tipo de vehículo (por ejemplo, el transporte público), pero otros pueden usarlo en ciertos casos, como para girar.\n\n"
                    + "Carril exclusivo: solo puede usarlo el vehículo autorizado, como el carril de Transmetro o una ciclorruta. Invadirlo es una infracción.\n\n"
                    + "Las líneas, las flechas y las señales verticales te dicen qué tipo de carril es y qué maniobras están permitidas."),
                Text(
                    "Elige tu carril con anticipación",
                    "En una vía de varios carriles, el carril derecho es para circular más despacio, para quien va a girar a la derecha y para detenerse en paraderos. "
                    + "Los carriles de la izquierda son para circular más rápido y para adelantar.\n\n"
                    + "Si vas a girar o tomar una salida, ubícate en el carril correcto varias cuadras antes. Cruzar varios carriles en el último momento es una de las maniobras más peligrosas en una avenida."),
                Order(
                    "Ordena la secuencia segura para cambiar de carril.",
                    ["Mirar el espejo retrovisor central", "Mirar el espejo lateral del lado al que vas", "Activar la direccional con anticipación", "Girar la cabeza para revisar el punto ciego", "Cambiar de carril de forma gradual, sin frenar"],
                    "Primero te informas con los espejos, luego avisas, compruebas el punto ciego que los espejos no muestran y ejecutas la maniobra con suavidad."),
                Text(
                    "Incorporarte a una vía rápida",
                    "Al entrar a una vía como la Circunvalar desde una vía de acceso, usa el carril de aceleración para alcanzar una velocidad parecida a la del tráfico. "
                    + "Busca un espacio, señaliza y entra sin obligar a otros a frenar. Si no hay espacio, ajusta tu velocidad dentro del carril de aceleración; "
                    + "detenerte al final es peligroso, porque luego tendrás que entrar desde cero.\n\n"
                    + "Quien ya circula por la vía rápida tiene prelación, pero un buen conductor facilita la entrada cuando es seguro hacerlo."),
                Text(
                    "Cuando el corredor cambia",
                    "Durante el Carnaval, los conciertos, los partidos o las obras, un corredor puede funcionar en contraflujo o con carriles cerrados. "
                    + "Lo que valía ayer puede no valer hoy: sigue la señalización temporal y las indicaciones de los agentes, aunque contradigan tu costumbre."),
                Pairs(
                    "Une cada situación con la posición correcta.",
                    ("Vas a girar a la derecha en dos cuadras", "Carril derecho, con anticipación"),
                    ("Vas a adelantar a un camión lento", "Carril izquierdo, y regresar a la derecha al terminar"),
                    ("Vas a tomar el retorno de la izquierda", "Carril izquierdo, varias cuadras antes"),
                    ("Circulas más despacio que el resto", "Carril derecho"),
                    ("Hay un carril exclusivo de Transmetro", "Nunca lo uses, salvo que la señal lo permita")),
                Scenario(
                    "Vas por el carril izquierdo de una avenida de tres carriles y te das cuenta de que la salida que necesitas está a 50 metros, a la derecha. ¿Qué haces?",
                    [
                        Choice("Cruzo los tres carriles de una vez para alcanzar la salida.", "Cruzar varios carriles de golpe sorprende a los demás y es una causa frecuente de choques laterales."),
                        Choice("Sigo derecho y tomo la siguiente salida o un retorno seguro.", "Correcto: perder una salida te cuesta unos minutos; una maniobra forzada puede costar una vida.", true),
                        Choice("Freno en mi carril hasta que alguien me deje pasar.", "Detenerte en un carril de circulación rápida provoca choques por detrás.")
                    ]),
                TrueFalse(
                    "Los espejos muestran todo lo que hay alrededor del vehículo, por eso no hace falta girar la cabeza antes de cambiar de carril.",
                    false,
                    "Falso: todos los vehículos tienen puntos ciegos que los espejos no cubren. Una mirada rápida por encima del hombro los revisa."),
                Quiz(
                    "Entras a una vía rápida por un carril de aceleración. ¿Qué es lo correcto?",
                    null,
                    ["Detenerme al final del carril y esperar un espacio", "Acelerar hasta una velocidad parecida a la del tráfico, señalizar y entrar en un espacio libre", "Entrar despacio, porque los demás deben frenar", "Usar la berma para seguir avanzando"],
                    1,
                    "El carril de aceleración existe para igualar tu velocidad a la del tráfico. Entrar despacio o detenerte crea conflictos con quien ya circula.")
            ]
        ),
        (
            "Cicloinfraestructura y convivencia con ciclistas",
            "Ciclorrutas, bicicarriles y cruces: cómo adelantar, girar y abrir la puerta sin poner en riesgo a quien pedalea.",
            13,
            [
                Text(
                    "La infraestructura para bicicletas",
                    "Ciclorruta: vía o franja exclusiva para bicicletas, separada físicamente de los carros; puede ir sobre el andén o en el separador.\n\n"
                    + "Bicicarril: carril para bicicletas sobre la calzada, separado del tráfico por bordillos, bolardos u otros elementos.\n\n"
                    + "Ciclobanda: franja demarcada con pintura en la calzada, sin separación física.\n\n"
                    + "En Barranquilla el Distrito ha conectado ciclorrutas y bicicarriles en sectores como las calles 88, 98 y 99. "
                    + "Donde no hay infraestructura, el ciclista circula por la derecha del carril y tiene derecho a usar la vía."),
                Classify(
                    "¿Qué conducta del conductor protege al ciclista y cuál lo pone en riesgo?",
                    ["Lo protege", "Lo pone en riesgo"],
                    [
                        ("Dejar 1,5 metros al adelantarlo", 0),
                        ("Esperar detrás si no hay espacio para adelantar", 0),
                        ("Mirar el espejo antes de abrir la puerta", 0),
                        ("Ceder el paso en un cruce de ciclorruta", 0),
                        ("Adelantarlo y girar a la derecha justo delante de él", 1),
                        ("Parquear sobre el bicicarril «solo un momento»", 1),
                        ("Pitarle para que se suba al andén", 1)
                    ],
                    "El ciclista no tiene carrocería: espacio, paciencia y una mirada al espejo son su protección."),
                Text(
                    "Los tres conflictos más peligrosos",
                    "El adelantamiento cercano: pasar rozando al ciclista lo desestabiliza con el aire y no le deja espacio para esquivar un hueco. La ley exige mínimo 1,5 metros.\n\n"
                    + "El giro a la derecha: el carro adelanta al ciclista y enseguida gira a la derecha, cerrándole el paso. Si vas a girar, quédate detrás del ciclista y gira cuando haya pasado.\n\n"
                    + "La puerta que se abre: un ciclista que viene pegado a los carros parqueados no alcanza a frenar si alguien abre la puerta. "
                    + "Antes de abrir, mira el espejo y gira la cabeza. Un truco útil: abre la puerta con la mano más lejana (la derecha si eres el conductor); así tu cuerpo gira y miras hacia atrás."),
                Flip(
                    "Lo que hace el ciclista que debes anticipar",
                    Card("Esquiva huecos", "Puede moverse hacia el centro del carril de repente. Por eso necesita 1,5 metros de espacio."),
                    Card("Señala con el brazo", "Brazo izquierdo extendido: va a girar a la izquierda. Brazo derecho extendido o izquierdo doblado hacia arriba: va a girar a la derecha."),
                    Card("Va más lento en subida", "En pendientes puede zigzaguear. Espera a tener visibilidad y espacio para adelantar."),
                    Card("Sale de la ciclorruta", "En los cruces y al final de la ciclorruta se incorpora a la vía: reduce y deja que termine su maniobra.")),
                Scenario(
                    "Vas por una calle de Riomar y quieres girar a la derecha en la próxima esquina. Un ciclista va por la derecha del carril, unos metros adelante. ¿Qué haces?",
                    [
                        Choice("Lo adelanto rápido y giro justo delante de él.", "Es el «gancho derecho»: le cierras el paso y el ciclista choca contra tu costado o cae."),
                        Choice("Reduzco, me quedo detrás del ciclista y giro cuando haya pasado la esquina.", "Correcto: esperar unos segundos evita cruzar su trayectoria.", true),
                        Choice("Le pito para que se detenga y me deje girar.", "El pito no le da tiempo de reaccionar y puede hacerlo caer. Quien gira debe ceder.")
                    ]),
                TrueFalse(
                    "Si no hay ciclorruta, el ciclista debe circular por el andén.",
                    false,
                    "Falso: el andén es para los peatones. Sin infraestructura, el ciclista circula por la derecha de la calzada y los demás deben respetarlo."),
                Quiz(
                    "Acabas de parquear junto a una ciclorruta. ¿Cuál es la forma más segura de abrir la puerta del conductor?",
                    null,
                    ["Abrirla rápido para no estorbar", "Mirar el espejo, girar la cabeza y abrir con la mano derecha", "Pitar antes de abrir", "Abrirla solo hasta la mitad"],
                    1,
                    "Abrir con la mano más lejana obliga a girar el cuerpo y mirar hacia atrás, así ves al ciclista que se acerca.")
            ]
        ),
        (
            "Espacio público y conflictos de movilidad",
            "Andenes, estacionamiento, cargue y descargue, ascenso de pasajeros y eventos masivos: usar la calle sin poner en riesgo a nadie.",
            12,
            [
                Text(
                    "La calle es de todos",
                    "El espacio público no es solo la calzada: incluye andenes, plazas, separadores, paraderos, ciclorrutas y zonas verdes. "
                    + "Cada parte tiene un uso, y los conflictos aparecen cuando alguien ocupa el espacio de otro: un carro en el andén, un vendedor en la ciclorruta, "
                    + "una moto en el paso peatonal o un camión descargando en doble fila."),
                Text(
                    "Los conflictos más comunes",
                    "Estacionar sobre el andén: obliga al peatón, a la persona en silla de ruedas o a la mamá con el coche a bajar a la calzada.\n\n"
                    + "Doble fila y cargue sin zona: bloquea un carril, reduce la visibilidad y provoca cambios de carril bruscos.\n\n"
                    + "Ascenso y descenso en mitad de la vía: el pasajero queda expuesto al tráfico. Detente junto al andén y en lugar permitido.\n\n"
                    + "Rampas y cruces bloqueados: una rampa ocupada deja sin camino a quien no puede subir un sardinel."),
                Pairs(
                    "Une cada conflicto con la solución que preserva la circulación y la seguridad.",
                    ("Necesitas descargar un electrodoméstico", "Usa la zona de cargue y descargue en su horario"),
                    ("Vas a recoger a alguien en un sector comercial", "Detente junto al andén en un lugar permitido, o da la vuelta"),
                    ("No encuentras parqueo cerca del colegio", "Parquea más lejos en un sitio permitido y camina"),
                    ("Un vendedor ocupa parte del carril", "Reduce, señaliza y rodéalo solo cuando sea seguro"),
                    ("La salida del concierto está llena de gente", "Espera y avanza a paso de persona")),
                Text(
                    "Eventos masivos y cierres",
                    "En el Gran Malecón, la Avenida del Río, el estadio o durante el Carnaval se hacen cierres, controles de acceso y desvíos para separar a miles de peatones de los vehículos. "
                    + "Respeta las vallas y los conos aunque parezca que «se puede pasar»: están ahí porque en minutos esa vía se llena de gente.\n\n"
                    + "A la salida de un evento espera peatones por todas partes, incluso fuera de los cruces, y conduce a paso de persona."),
                Classify(
                    "¿Es un uso correcto del espacio público o genera un conflicto?",
                    ["Correcto", "Genera conflicto"],
                    [
                        ("Recoger a un pasajero junto al andén en una zona permitida", 0),
                        ("Descargar mercancía en la zona y el horario señalados", 0),
                        ("Dejar libre la rampa de la esquina", 0),
                        ("Parquear sobre el andén frente a tu casa", 1),
                        ("Detenerte en doble fila para comprar algo «rápido»", 1),
                        ("Esperar a alguien encima de la cebra", 1)
                    ],
                    "El espacio público funciona cuando cada uno usa su parte: el andén para caminar, la zona de cargue para descargar y la calzada para circular."),
                Scenario(
                    "Llevas a tu hijo al colegio y la calle del frente está llena. El único espacio libre es la rampa de acceso de la esquina. ¿Qué haces?",
                    [
                        Choice("Me detengo en la rampa solo un minuto mientras se baja.", "Un minuto basta para que una persona en silla de ruedas o un niño con maleta de ruedas quede sin paso."),
                        Choice("Busco un lugar permitido un poco más lejos y lo acompaño caminando.", "Correcto: caminar unos metros es seguro y deja libre el espacio de los demás.", true),
                        Choice("Lo dejo bajar en mitad de la calle con las luces de parqueo.", "Bajar a un niño en medio de la vía lo expone a los vehículos que vienen detrás o a los lados.")
                    ]),
                Quiz(
                    "¿Dónde debe bajarse un pasajero de un vehículo?",
                    null,
                    ["En cualquier punto, si el vehículo tiene las luces de parqueo encendidas", "Junto al andén y en un lugar donde esté permitido detenerse", "En la mitad de la vía, si hay trancón", "Por la puerta del lado de la calzada"],
                    1,
                    "Al bajarse junto al andén, el pasajero llega directo a una zona segura, sin cruzar frente al tráfico.")
            ]
        )
    ];
}
