namespace Cale.Api.Services.Courses;

/// <summary>
/// "Movilidad segura y sostenible": núcleo 1-A-1 of the school's curriculum (temas A, B, C, E, G y H; el tema F está en Primeros auxilios).
/// Infographics are the school's own; videos are public material from the ANSV.
/// </summary>
public sealed partial class CourseSeed
{
    private const string MobilityImages = "/courses/movilidad";

    private static object Picture(string url, string caption) => new { type = "image", url, caption };

    private static object Flip(string title, params object[] cards) => new { type = "flipcards", title, cards };

    private static object Pairs(string instructions, params (string Left, string Right)[] pairs) =>
        new
        {
            type = "match",
            instructions,
            pairs = pairs.Select(p => new { left = p.Left, imageUrl = (string?)null, right = p.Right }).ToArray()
        };

    private static object Hotspot(string instructions, string imageUrl, params object[] spots) =>
        new { type = "hotspot", instructions, imageUrl, spots };

    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> MobilityLessons() =>
    [
        (
            "Seguridad vial y Sistema Seguro",
            "Qué es la seguridad vial, por qué un error no debe costar una vida y quién es responsable de prevenir los siniestros.",
            18,
            [
                Text(
                    "Moverse sin morir en el intento",
                    "La seguridad vial busca que nadie muera ni quede gravemente lesionado mientras se moviliza, sea a pie, en bicicleta, en moto, en carro o en bus.\n\n"
                    + "En cada situación de tránsito intervienen cuatro factores al mismo tiempo: la persona (sus decisiones y su comportamiento), "
                    + "el vehículo (su estado y su equipamiento de seguridad), la vía (su diseño, su señalización y su estado) y el entorno (el clima, la luz, el tráfico y los demás actores).\n\n"
                    + "Por eso, cuando ocurre un siniestro, la pregunta no es solo «¿quién tuvo la culpa?», sino «¿qué factores se juntaron y cuáles se pudieron controlar?»."),
                Picture($"{MobilityImages}/que-es-seguridad-vial.jpg", "Persona, vehículo, vía y entorno: el riesgo nunca depende de un solo factor."),
                Text(
                    "El enfoque de Sistema Seguro",
                    "El Sistema Seguro parte de una idea sencilla: las personas nos equivocamos. Calculamos mal una distancia, nos distraemos un segundo o no vemos a un motociclista.\n\n"
                    + "El objetivo no es exigir que nadie cometa errores, sino que un error no termine en una muerte o una lesión grave. Para lograrlo se trabaja a la vez sobre "
                    + "el comportamiento humano, vehículos más seguros, vías más seguras, velocidades seguras y una atención rápida a las víctimas.\n\n"
                    + "La Ley 2251 de 2022 incorporó este enfoque en la política de seguridad vial de Colombia."),
                Video("ansv-sistema-seguro.mp4", "El enfoque de Sistema Seguro en el contexto de la seguridad vial. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Picture($"{MobilityImages}/sistema-seguro.jpg", "Sistema Seguro: personas que se cuidan, vehículos y vías más seguros y una respuesta rápida al siniestro."),
                Text(
                    "De peligro a consecuencia",
                    "Peligro: una condición que puede producir daño. Por ejemplo, aceite derramado en la vía.\n\n"
                    + "Riesgo: la posibilidad de que ese peligro termine en daño. Una moto que pasa sobre el aceite puede perder adherencia.\n\n"
                    + "Siniestro: el hecho que ocurre durante la movilidad y produce daños, lesiones o muerte. La moto se desliza y cae.\n\n"
                    + "Consecuencia: lo que deja el siniestro: lesiones, fracturas, incapacidad, daños o muerte.\n\n"
                    + "Si aprendes a ver el peligro, puedes cortar la cadena antes de que llegue al siniestro."),
                Picture($"{MobilityImages}/peligro-consecuencia.jpg", "Una cadena que se puede prevenir: peligro, riesgo, siniestro y consecuencia."),
                Order(
                    "Ordena la cadena desde lo primero que aparece hasta el resultado final.",
                    ["Peligro: hay un hueco tapado por un charco", "Riesgo: un carro se acerca rápido sin verlo", "Siniestro: la llanta cae en el hueco y el carro se sale de su carril", "Consecuencia: daños en el vehículo y una persona lesionada"],
                    "Cuanto antes identifiques el eslabón, más fácil es cortarlo: si ves el charco y bajas la velocidad, la cadena se detiene en el riesgo."),
                Text(
                    "¿Siniestro o accidente?",
                    "Decir «fue un accidente» transmite la idea de que nada se podía hacer. Hablar de siniestro vial obliga a preguntarse qué factores estaban presentes, "
                    + "qué riesgos se podían identificar y qué decisión habría evitado o reducido el daño.\n\n"
                    + "El lenguaje importa: lo que se puede explicar, se puede prevenir."),
                Picture($"{MobilityImages}/siniestro-no-accidente.jpg", "«Analicemos qué pasó y cómo evitar que se repita»: por eso hablamos de siniestro."),
                TrueFalse(
                    "Llamar «accidente» a un choque ayuda a prevenir que se repita.",
                    false,
                    "Falso: «accidente» sugiere que fue inevitable. «Siniestro vial» invita a analizar causas y decisiones, que es lo que permite prevenir."),
                Text(
                    "Responsabilidad compartida",
                    "La seguridad vial no es solo tarea del conductor. Participan peatones, ciclistas, motociclistas, pasajeros, autoridades, quienes diseñan y mantienen las vías, "
                    + "los fabricantes de vehículos, las escuelas y los servicios de emergencia.\n\n"
                    + "Eso no significa que todos respondan igual en cada situación: quien conduce maneja el vehículo con más energía y, por eso, tiene deberes específicos. "
                    + "El artículo 55 del Código Nacional de Tránsito lo resume: todo usuario de la vía debe comportarse de forma que no obstaculice, perjudique ni ponga en riesgo a los demás."),
                Picture($"{MobilityImages}/responsabilidad-compartida.jpg", "Todos tenemos responsabilidad dentro del sistema, pero cada actor tiene deberes diferentes."),
                Flip(
                    "Las leyes que sostienen la seguridad vial",
                    Card("Ley 769 de 2002", "Código Nacional de Tránsito: regula el comportamiento de conductores, pasajeros y peatones."),
                    Card("Ley 1503 de 2011", "Promueve la formación de hábitos, comportamientos y conductas seguras en la vía."),
                    Card("Ley 1702 de 2013", "Crea la Agencia Nacional de Seguridad Vial (ANSV), la autoridad que lidera la política de seguridad vial."),
                    Card("Ley 2251 de 2022", "Adopta el enfoque de Sistema Seguro y la responsabilidad compartida en la política de seguridad vial.")),
                Scenario(
                    "Vas por una calle de Barranquilla al final de la tarde, después de un aguacero. El pavimento está húmedo, hay un carro estacionado a medio andén, "
                    + "una moto se acerca por tu lado y un peatón espera para cruzar en la cebra. ¿Qué haces?",
                    [
                        Choice("Mantengo la velocidad: tengo la vía y el peatón debe esperar.", "Tener la vía no elimina el riesgo. Con piso mojado, tu distancia de frenado es mayor y el peatón tiene prelación en la cebra."),
                        Choice("Reduzco la velocidad, miro el espejo por la moto y me preparo para ceder el paso al peatón.", "Correcto: observas, identificas los riesgos (piso, moto, peatón), decides con margen y verificas. Eso es conducir dentro del Sistema Seguro.", true),
                        Choice("Pito para que el peatón no cruce y acelero para pasar antes.", "Acelerar con piso mojado frente a una cebra pone en riesgo al actor más vulnerable de la escena.")
                    ],
                    $"{MobilityImages}/ejemplo-practico.jpg"),
                Quiz(
                    "¿Cuál es la idea central del enfoque de Sistema Seguro?",
                    null,
                    ["Que los conductores nunca cometan errores", "Que un error humano no termine en una muerte o una lesión grave", "Que la culpa siempre sea del conductor", "Que haya más agentes de tránsito"],
                    1,
                    "El Sistema Seguro acepta que las personas fallan y organiza vías, vehículos, velocidades y atención para que esos errores no cuesten vidas.")
            ]
        ),
        (
            "Víctimas y consecuencias de los siniestros",
            "Quiénes son las víctimas, por qué la velocidad decide la gravedad y cómo un siniestro cambia la vida de una familia.",
            18,
            [
                Text(
                    "Una cifra que tiene nombres",
                    "Según la Organización Mundial de la Salud, los siniestros viales matan a cerca de 1,2 millones de personas al año en el mundo y son la principal causa de muerte "
                    + "de niños y jóvenes entre 5 y 29 años.\n\n"
                    + "En Colombia mueren más de 8.000 personas al año en las vías, y más de la mitad de ellas iban en motocicleta. "
                    + "Detrás de cada número hay una familia, un trabajo que se pierde y una comunidad que cambia."),
                Video("ansv-victimas-siniestros.mp4", "La atención integral a las víctimas de siniestros viales y sus familias. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Text(
                    "Víctimas directas e indirectas",
                    "Víctima directa: la persona que muere o resulta lesionada en el siniestro.\n\n"
                    + "Víctimas indirectas: su familia, sus hijos, quienes dependían de su ingreso, los testigos y hasta quien causó el siniestro, que carga con consecuencias legales y emocionales.\n\n"
                    + "Las consecuencias no terminan el día del siniestro: hay discapacidades permanentes, tratamientos largos, gastos médicos, deudas y duelos que duran años."),
                Classify(
                    "Clasifica cada consecuencia según su tipo.",
                    ["Humana y familiar", "Social y económica"],
                    [
                        ("Una persona queda en silla de ruedas", 0),
                        ("Un niño pierde a su papá", 0),
                        ("Estrés postraumático del conductor", 0),
                        ("Gastos de hospital y rehabilitación", 1),
                        ("Pérdida del empleo o del ingreso de la familia", 1),
                        ("Congestión y cierre de la vía durante horas", 1)
                    ],
                    "Un siniestro golpea primero a las personas, pero también tiene un costo enorme para la economía de la familia y de la ciudad."),
                Text(
                    "La velocidad decide la gravedad",
                    "El cuerpo humano tolera muy poca energía en un choque. Por eso la velocidad no solo aumenta la probabilidad de un siniestro: decide qué tan grave será.\n\n"
                    + "La OMS explica que un peatón adulto atropellado a menos de 50 km/h tiene menos del 20 % de probabilidad de morir, "
                    + "pero a 80 km/h ese riesgo sube a casi el 60 %. A 30 km/h, la gran mayoría de los peatones sobrevive.\n\n"
                    + "La diferencia entre llegar dos minutos antes y causar una muerte puede ser de apenas 20 km/h."),
                Video("ansv-respeta-limites.mp4", "Respeta los límites de velocidad. Video: Ministerio de Transporte y Agencia Nacional de Seguridad Vial (ANSV)."),
                FillBlank(
                    "Un peatón atropellado a menos de [[50]] km/h tiene menos del 20 % de probabilidad de morir; a [[80]] km/h el riesgo sube a casi el 60 %. Por eso las zonas escolares se limitan a [[30]] km/h.",
                    ["10", "120", "45"],
                    "La energía de un choque crece con el cuadrado de la velocidad: pequeños aumentos de velocidad causan daños mucho mayores."),
                Text(
                    "Los más expuestos",
                    "Peatones, ciclistas y motociclistas no tienen una carrocería que los proteja: su cuerpo recibe todo el impacto. "
                    + "Por eso, en los corredores de alto flujo y en los entornos escolares de Barranquilla, las decisiones del conductor de un vehículo grande pesan más.\n\n"
                    + "Cuando leas cifras de siniestros, usa siempre las más recientes del Observatorio Nacional de Seguridad Vial o de la Secretaría Distrital de Tránsito: "
                    + "los números cambian cada año, la lección no."),
                Scenario(
                    "A las 6:30 a. m. vas a 50 km/h por una vía cerca de un colegio. Ves niños bajando de una buseta en la esquina. ¿Qué decisión reduce más la gravedad de un posible siniestro?",
                    [
                        Choice("Sigo a 50 km/h, porque es el límite de la vía.", "El límite general no es la velocidad segura frente a niños: si uno sale corriendo, a 50 km/h no alcanzas a detenerte."),
                        Choice("Bajo a 30 km/h o menos, me alejo de la buseta y me preparo para frenar.", "Correcto: a 30 km/h recorres menos metros mientras reaccionas y, si hay impacto, la probabilidad de sobrevivir es mucho mayor.", true),
                        Choice("Pito varias veces para que los niños se aparten.", "El pito puede asustar a un niño y hacer que corra hacia la vía. La protección real es bajar la velocidad.")
                    ]),
                TrueFalse(
                    "Las víctimas de un siniestro son solo las personas que resultan heridas o mueren en el lugar.",
                    false,
                    "Falso: también son víctimas sus familias, quienes dependían de ellas y los testigos. Las consecuencias se extienden por años."),
                Quiz(
                    "¿Qué actor resulta más vulnerable en un choque con un carro?",
                    null,
                    ["El conductor de otro carro", "El pasajero de un bus", "El peatón", "El conductor de un camión"],
                    2,
                    "El peatón no tiene ninguna protección: todo el impacto lo recibe su cuerpo. Por eso el conductor debe extremar la prudencia cerca de ellos.")
            ]
        ),
        (
            "Usuarios vulnerables, prioridad y convivencia",
            "Peatones, ciclistas, motociclistas, niños, personas mayores y personas con discapacidad: quién tiene prioridad y cómo protegerlos.",
            15,
            [
                Text(
                    "¿Quiénes son los usuarios vulnerables?",
                    "Son quienes reciben la peor parte en un choque porque su cuerpo queda expuesto: peatones, ciclistas, motociclistas y sus acompañantes.\n\n"
                    + "Dentro de ellos hay personas aún más vulnerables: niños, que son pequeños, impulsivos y calculan mal las distancias; "
                    + "personas mayores, que caminan más despacio y pueden ver u oír menos; y personas con discapacidad, que pueden necesitar más tiempo o no percibir el vehículo."),
                Text(
                    "La pirámide de la movilidad",
                    "La movilidad sostenible ordena las prioridades de abajo hacia arriba según la vulnerabilidad y el beneficio para la ciudad: "
                    + "primero el peatón, después la bicicleta, luego el transporte público, el transporte de carga y, al final, el vehículo particular.\n\n"
                    + "No significa que el peatón «siempre tenga la razón», sino que quien maneja más energía debe proteger a quien tiene menos."),
                Order(
                    "Ordena la pirámide de la movilidad, desde la mayor prioridad hasta la menor.",
                    ["Peatón", "Ciclista", "Transporte público", "Transporte de carga", "Vehículo particular"],
                    "Primero quien camina, luego la bicicleta y el transporte público, que mueven a muchas personas con poco espacio. El vehículo particular va al final."),
                Text(
                    "Prelación no es permiso para arriesgar",
                    "Tener la prelación (el derecho a pasar primero) no te autoriza a pasar a cualquier costo. Si un peatón se equivoca y cruza por donde no debe, "
                    + "tu deber sigue siendo evitar el choque.\n\n"
                    + "Algunas reglas clave: el peatón tiene prelación en las cebras y en las intersecciones sin semáforo; al girar, cedes el paso al peatón que cruza la vía a la que entras; "
                    + "y al adelantar a un ciclista debes dejarle al menos 1,5 metros de distancia lateral (Ley 1811 de 2016)."),
                Flip(
                    "Cómo proteger a cada uno",
                    Card("Niños", "Baja la velocidad en zonas escolares y residenciales. Si ves una pelota, espera que detrás venga un niño."),
                    Card("Personas mayores", "Dales tiempo para terminar de cruzar, aunque el semáforo cambie. No pites para apurarlas."),
                    Card("Personas con discapacidad", "Respeta rampas, cruces y parqueaderos reservados. Una persona ciega con bastón blanco tiene prioridad absoluta."),
                    Card("Ciclistas", "Déjales 1,5 metros al adelantar y no los encierres al girar a la derecha."),
                    Card("Motociclistas", "Míralos dos veces antes de cambiar de carril o girar: son pequeños y aparecen rápido en los espejos.")),
                Text(
                    "Entornos escolares en Barranquilla",
                    "El Distrito desarrolla el programa de Zonas Escolares con Movilidad Segura (ZEMS), que interviene los alrededores de los colegios con señalización, "
                    + "cruces seguros y educación vial.\n\n"
                    + "En la entrada y salida de clases los riesgos se multiplican: niños bajando de busetas y carros, padres estacionados en doble fila y cruces improvisados. "
                    + "Ahí la única velocidad segura es la que te permite detenerte de inmediato."),
                Signs(
                    "Señales que anuncian usuarios vulnerables",
                    Sign("SP-47", "Zona escolar", "Reduce a 30 km/h o menos y espera niños en cualquier momento."),
                    Sign("SP-46A", "Proximidad de cruce peatonal", "Prepárate para ceder el paso."),
                    Sign("SP-59", "Ciclistas en la vía", "Comparte el carril con espacio y paciencia."),
                    Sign("SP-46C", "Zona con prioridad peatonal", "Aquí el peatón manda: circula a paso de persona.")),
                Scenario(
                    "Vas a girar a la derecha en un cruce con semáforo en verde. Una señora mayor está cruzando despacio la calle a la que vas a entrar. ¿Qué haces?",
                    [
                        Choice("Giro detrás de ella apenas pase la mitad del carril.", "Pasar «raspando» a una persona mayor es peligroso: puede detenerse, devolverse o tropezar."),
                        Choice("Me detengo antes del cruce y espero a que termine de cruzar.", "Correcto: al girar cedes el paso al peatón que cruza, y con una persona mayor le das todo el tiempo que necesite.", true),
                        Choice("Pito para que se apure, porque el semáforo me da paso.", "El verde te permite avanzar, pero no te da prioridad sobre el peatón que ya está cruzando.")
                    ]),
                TrueFalse(
                    "Si un peatón cruza por un lugar indebido, el conductor ya no tiene obligación de evitar el choque.",
                    false,
                    "Falso: el error de otro no autoriza a atropellarlo. Quien conduce debe hacer todo lo posible por evitar el siniestro."),
                Quiz(
                    "¿Qué distancia lateral mínima debes dejar al adelantar a un ciclista?",
                    null,
                    ["50 centímetros", "1 metro", "1,5 metros", "No hay una distancia mínima"],
                    2,
                    "La Ley 1811 de 2016 exige mínimo 1,5 metros. Si no hay espacio para dejarlos, espera detrás del ciclista hasta que puedas adelantar con seguridad.")
            ]
        ),
        (
            "Movilidad sostenible y conducción responsable",
            "Moverse contaminando menos y sin aumentar el riesgo: elegir el modo de transporte, planear el recorrido y cuidar el vehículo.",
            12,
            [
                Text(
                    "¿Qué es la movilidad sostenible?",
                    "Es moverse de forma que se satisfagan las necesidades de hoy sin dañar la salud, el ambiente ni la seguridad de los demás. Incluye tres preguntas:\n\n"
                    + "¿Necesito hacer este viaje? ¿Cuál es el modo más eficiente para hacerlo: caminar, bicicleta, transporte público o vehículo particular? "
                    + "Y si uso el vehículo, ¿lo uso de forma responsable?\n\n"
                    + "Un carro con una sola persona ocupa el espacio de varias decenas de peatones y produce mucho más ruido y emisiones por persona transportada."),
                Classify(
                    "Para un trayecto corto dentro del barrio, ¿qué opción es más sostenible?",
                    ["Más sostenible", "Menos sostenible"],
                    [
                        ("Caminar hasta la tienda a tres cuadras", 0),
                        ("Ir en bicicleta al trabajo por una ciclorruta", 0),
                        ("Usar Transmetro o un bus para ir al centro", 0),
                        ("Compartir el carro con compañeros que van al mismo lugar", 0),
                        ("Sacar el carro para recorrer dos cuadras", 1),
                        ("Dejar el motor encendido mientras esperas a alguien", 1),
                        ("Dar vueltas buscando parqueo sin planear", 1)
                    ],
                    "Caminar, pedalear, usar transporte público o compartir el vehículo reduce congestión, ruido y emisiones."),
                Text(
                    "Planear el recorrido",
                    "En Barranquilla hay cierres temporales por obras, eventos en el Gran Malecón o la Avenida del Río, el Carnaval, partidos y carreras deportivas. "
                    + "Antes de salir, revisa las publicaciones de la Secretaría Distrital de Tránsito y Seguridad Vial: un desvío planeado ahorra tiempo, combustible y estrés.\n\n"
                    + "Si vas a un evento masivo, considera el transporte público o llegar caminando desde un punto cercano: el parqueo y la salida suelen ser el mayor problema."),
                Text(
                    "El mantenimiento también es sostenible",
                    "Un vehículo bien mantenido contamina menos, consume menos y es más seguro. Llantas con la presión correcta, filtro de aire limpio, aceite al día y motor sincronizado "
                    + "hacen la diferencia. La revisión técnico-mecánica y de emisiones existe precisamente para verificarlo."),
                Tip("Ser eficiente nunca significa conducir de forma insegura: apagar el motor en una bajada o ir pegado al vehículo de adelante para «aprovechar el rebufo» es peligroso."),
                Scenario(
                    "El domingo hay un concierto en el Gran Malecón. Quieres ir con tres amigos que viven cerca de ti. ¿Cuál es la decisión más responsable?",
                    [
                        Choice("Cada uno va en su carro para tener libertad de salir.", "Cuatro carros para cuatro personas multiplican la congestión, la búsqueda de parqueo y las emisiones."),
                        Choice("Revisan los cierres anunciados y van juntos en un solo vehículo o en transporte público.", "Correcto: planear y compartir reduce el tráfico, el estrés y el riesgo a la salida del evento.", true),
                        Choice("Van en un carro y parquean en el andén más cercano a la entrada.", "Parquear en el andén es una infracción y obliga a los peatones a caminar por la vía, justo donde hay más gente.")
                    ]),
                TrueFalse(
                    "Conducir de forma eficiente significa ir siempre a la menor velocidad posible, aunque estorbes el flujo.",
                    false,
                    "Falso: la eficiencia busca un ritmo suave y constante, adaptado a la vía. Ir excesivamente lento también genera conflictos y riesgos."),
                Quiz(
                    "¿Por qué el mantenimiento preventivo influye en la movilidad sostenible?",
                    null,
                    ["Porque el vehículo se ve más bonito", "Porque un vehículo en buen estado consume menos, contamina menos y es más seguro", "Porque permite ir más rápido", "No tiene ninguna relación"],
                    1,
                    "Llantas, filtros, aceite y motor en buen estado reducen el consumo y las emisiones, y además evitan fallas que causan siniestros.")
            ]
        ),
        (
            "Conducción preventiva y gestión del riesgo",
            "Observar, identificar, decidir, actuar y verificar: anticiparte a los errores propios y ajenos con distancia y tiempo de reacción.",
            18,
            [
                Text(
                    "El buen conductor se anticipa",
                    "La conducción preventiva (o defensiva) consiste en conducir de forma que puedas evitar un siniestro a pesar de tus errores, de los errores de los demás "
                    + "y de las condiciones adversas. No se trata de reaccionar rápido, sino de no tener que reaccionar a última hora."),
                Picture($"{MobilityImages}/conductor-sistema-seguro.jpg", "Observar, identificar, decidir, actuar y verificar: la secuencia del conductor dentro del Sistema Seguro."),
                Order(
                    "Ordena la secuencia que sigue un conductor preventivo.",
                    ["Observar: ¿qué está pasando alrededor?", "Identificar: ¿qué puede convertirse en un riesgo?", "Decidir: ¿qué conducta reduce ese riesgo?", "Actuar: ejecuto la maniobra comunicando mi intención", "Verificar: ¿la decisión mantuvo la seguridad?"],
                    "Primero miras y reconoces los peligros, luego eliges y ejecutas, y al final compruebas que la situación siga bajo control."),
                Text(
                    "El tiempo de reacción",
                    "Desde que aparece un peligro hasta que empiezas a frenar pasa cerca de un segundo, y más si estás cansado, distraído o bajo efectos del alcohol. "
                    + "Durante ese segundo el vehículo sigue avanzando a la misma velocidad.\n\n"
                    + "A 60 km/h recorres casi 17 metros antes de tocar el freno, y después necesitas otros metros para detenerte. "
                    + "Con piso mojado, llantas gastadas o frenos en mal estado, la distancia total crece mucho más."),
                Text(
                    "La distancia de seguridad",
                    "El Código Nacional de Tránsito (artículo 108) fija distancias mínimas entre vehículos que circulan uno detrás de otro: "
                    + "10 metros hasta 30 km/h, 20 metros entre 30 y 60 km/h, 25 metros entre 60 y 80 km/h y 30 metros entre 80 y 100 km/h.\n\n"
                    + "Una forma práctica de medirla es la regla de los segundos: cuando el vehículo de adelante pase un punto fijo (un poste), cuenta «mil uno, mil dos, mil tres». "
                    + "Si llegas al poste antes de terminar, vas demasiado cerca. Con lluvia o de noche, duplica el tiempo."),
                FillBlank(
                    "Según el Código, a una velocidad entre 30 y 60 km/h debes dejar mínimo [[20]] metros con el vehículo de adelante. Con lluvia, la distancia en segundos se debe [[duplicar]].",
                    ["5", "reducir", "mantener igual"],
                    "La distancia te da el tiempo de reacción que necesitas. Cuando el piso está mojado, frenar toma más metros."),
                Hotspot(
                    "Analiza la escena como si estuvieras conduciendo. Toca los cuatro elementos que representan un riesgo.",
                    $"{MobilityImages}/ejemplo-practico.jpg",
                    Spot(27, 52, "Carro estacionado", "Reduce el espacio y puede abrir una puerta o salir sin avisar."),
                    Spot(37, 55, "Motocicleta cercana", "Puede estar en tu punto ciego; mírala antes de cualquier movimiento lateral."),
                    Spot(59, 53, "Peatón a punto de cruzar", "Tiene prelación en la cebra: prepárate para detenerte."),
                    Spot(30, 65, "Pavimento mojado", "Aumenta la distancia de frenado: baja la velocidad y frena con suavidad.")),
                Text(
                    "Escenarios adversos en la ciudad",
                    "Lluvia: en Barranquilla un aguacero fuerte forma arroyos en minutos. Nunca intentes cruzar un arroyo: la corriente puede arrastrar un carro con pocos centímetros de agua. "
                    + "Espera en un lugar alto y seguro.\n\n"
                    + "Obras y cambios viales: los desvíos cambian los sentidos y los carriles. Lee la señalización temporal en vez de confiar en tu memoria.\n\n"
                    + "Alta interacción con motos: espera que aparezcan por ambos lados y en los cruces. Una mirada adicional al espejo vale más que una frenada de emergencia."),
                Picture($"{MobilityImages}/factores-riesgo.jpg", "Peatón cruzando, vehículo que frena, moto al costado y pavimento húmedo: varios factores al mismo tiempo."),
                Scenario(
                    "Llueve fuerte en Barranquilla y ves que adelante la calle se convirtió en un arroyo. Los carros de adelante se detienen, pero una camioneta intenta pasar. ¿Qué haces?",
                    [
                        Choice("Sigo a la camioneta: si ella pasa, yo también.", "La fuerza del agua no depende del vehículo de adelante. Un arroyo puede arrastrar un carro con pocos centímetros de agua."),
                        Choice("Me detengo en un lugar alto y seguro hasta que el arroyo baje.", "Correcto: ningún trayecto vale la vida. Los arroyos bajan en poco tiempo; esperar es la decisión segura.", true),
                        Choice("Acelero fuerte para cruzar rápido.", "Acelerar dentro del agua puede apagar el motor, hacerte perder el control o meter agua al motor. Y la corriente sigue siendo la misma.")
                    ]),
                TrueFalse(
                    "La conducción preventiva consiste en tener reflejos muy rápidos para frenar a última hora.",
                    false,
                    "Falso: se trata de anticiparse con distancia, velocidad adecuada y observación, para no depender de los reflejos."),
                Quiz(
                    "Vas a 60 km/h y aparece un obstáculo. ¿Aproximadamente cuántos metros recorres durante el segundo que tardas en reaccionar?",
                    null,
                    ["2 metros", "Casi 17 metros", "60 metros", "100 metros"],
                    1,
                    "60 km/h equivalen a unos 16,7 metros por segundo. Y eso es antes de empezar a frenar.")
            ]
        ),
        (
            "Conducción eficiente y eco-conducción",
            "Acelerar con suavidad, anticiparte, usar bien las marchas y cuidar las llantas para gastar menos sin perder seguridad.",
            12,
            [
                Text(
                    "Gastar menos sin arriesgar más",
                    "La conducción eficiente reduce el consumo de combustible, las emisiones y el desgaste del vehículo. Lo mejor es que casi todas sus técnicas también hacen la conducción más segura: "
                    + "un conductor que se anticipa frena menos, acelera menos y tiene más margen."),
                Flip(
                    "Las claves de la eco-conducción",
                    Card("Arranca suave", "Acelera de forma progresiva. Los arranques bruscos son los que más combustible gastan."),
                    Card("Anticípate", "Mira lejos: si el semáforo está en rojo, suelta el acelerador y deja que el carro ruede en vez de frenar en el último momento."),
                    Card("Usa la marcha adecuada", "Cambia a una marcha más alta a bajas revoluciones (en motores a gasolina, cerca de 2.000 a 2.500 rpm)."),
                    Card("Velocidad constante", "Mantén un ritmo uniforme. Los cambios bruscos de velocidad elevan el consumo."),
                    Card("Llantas bien infladas", "Una llanta con poca presión aumenta el consumo, se desgasta más rápido y frena peor."),
                    Card("Menos peso, menos consumo", "Saca del baúl lo que no necesitas y retira la parrilla del techo si no la usas.")),
                Classify(
                    "¿Este hábito ahorra combustible o lo desperdicia?",
                    ["Ahorra", "Desperdicia"],
                    [
                        ("Soltar el acelerador con anticipación al ver un semáforo en rojo", 0),
                        ("Revisar la presión de las llantas cada dos semanas", 0),
                        ("Planear la ruta para evitar trancones conocidos", 0),
                        ("Acelerar a fondo en cada arranque", 1),
                        ("Llevar el baúl lleno de cosas que no usas", 1),
                        ("Dejar el motor encendido diez minutos esperando a alguien", 1),
                        ("Ir en segunda marcha a 50 km/h", 1)
                    ],
                    "Lo que ahorra combustible casi siempre coincide con lo que reduce el riesgo: suavidad, anticipación y buen mantenimiento."),
                Text(
                    "Eficiencia en una ciudad con trancones",
                    "En recorridos urbanos con muchas paradas, la mayor parte del combustible se va en arrancar. Mantener distancia con el vehículo de adelante permite rodar más y frenar menos.\n\n"
                    + "Si conduces un vehículo de servicio público, la eficiencia nunca debe convertirse en presión por tiempo: la seguridad de los pasajeros va primero."),
                Tip("Nunca bajes una pendiente con el motor apagado o en neutro para ahorrar: pierdes el freno de motor y, en muchos vehículos, la asistencia de la dirección y de los frenos."),
                Scenario(
                    "Vas por la Vía 40 y ves que el semáforo de la próxima intersección, a 200 metros, acaba de cambiar a rojo. ¿Cuál es la conducción más eficiente y segura?",
                    [
                        Choice("Sigo acelerando y freno fuerte al llegar.", "Gastas combustible acelerando para luego desperdiciarlo frenando, y una frenada brusca puede causar un choque por detrás."),
                        Choice("Suelto el acelerador, dejo que el carro ruede y freno suave al final.", "Correcto: aprovechas la inercia, gastas menos frenos y combustible, y los de atrás tienen tiempo de reaccionar.", true),
                        Choice("Pongo neutro y apago el motor para ahorrar.", "Apagar el motor en marcha es peligroso: puedes perder la asistencia de la dirección y de los frenos.")
                    ]),
                TrueFalse(
                    "Una llanta con baja presión aumenta el consumo de combustible.",
                    true,
                    "Verdadero: la llanta se deforma más, ofrece más resistencia al rodar, se desgasta más rápido y frena peor."),
                Quiz(
                    "¿Qué opción reduce el consumo manteniendo el margen de seguridad?",
                    null,
                    ["Ir pegado al vehículo de adelante para aprovechar el aire", "Mantener distancia y una velocidad constante", "Bajar las pendientes en neutro", "Acelerar fuerte para llegar rápido a la velocidad de crucero"],
                    1,
                    "Con distancia y ritmo constante frenas y aceleras menos. Las otras opciones aumentan el riesgo.")
            ]
        )
    ];

    private static object Signs(string title, params object[] items) => new { type = "signs", title, items };
}
