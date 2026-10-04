namespace Cale.Api.Services.Courses;

/// <summary>
/// "Normas de tránsito básicas": original texts based on Ley 769 de 2002 and its current amendments,
/// following the school's curriculum (núcleo 1-A-2, temas A a K).
/// </summary>
public sealed partial class CourseSeed
{
    private static object TrueFalse(string statement, bool answer, string explanation, string? imageUrl = null) =>
        new { type = "truefalse", statement, imageUrl, answer, explanation };

    private static object Order(string instructions, string[] steps, string explanation) =>
        new { type = "order", instructions, steps, explanation };

    private static object FillBlank(string text, string[] distractors, string explanation) =>
        new { type = "fillblank", text, distractors, explanation };

    private static object Choice(string text, string outcome, bool best = false) => new { text, outcome, best };

    private static object Scenario(string situation, object[] choices, string? imageUrl = null) =>
        new { type = "scenario", situation, imageUrl, choices };

    private static object Classify(string instructions, string[] groups, (string Text, int Group)[] items, string explanation) =>
        new
        {
            type = "classify",
            instructions,
            groups,
            items = items.Select(i => new { text = i.Text, imageUrl = (string?)null, group = i.Group }).ToArray(),
            explanation
        };

    private static object Spot(double x, double y, string label, string note) => new { x, y, label, note };

    private static List<(string Key, string Title, string Summary, int Minutes, object[] Blocks)> RulesLessons() =>
    [
        (
            "normas-transito/autorregulacion",
            "Autorregulación y responsabilidad",
            "Controlar tus decisiones aunque nadie te vigile: exceso de confianza, presión social e impulsividad.",
            10,
            [
                Text(
                    "La norma que te pones tú",
                    "No siempre habrá un agente mirando. La autorregulación es la capacidad de controlar tus decisiones y tu conducta aunque no exista vigilancia: "
                    + "respetar el límite con la vía vacía, no usar el celular aunque nadie lo vea y decidir no conducir si estás cansado o tomaste alcohol.\n\n"
                    + "Es lo que diferencia aprobar el examen de ser un buen conductor."),
                Text(
                    "Lo que te saca del control",
                    "Exceso de confianza: «yo manejo bien, a mí no me pasa».\n\n"
                    + "Presión social: amigos que te apuran, un pasajero que te pide ir más rápido, el carro de atrás que pita.\n\n"
                    + "Impulsividad: reaccionar de inmediato ante un cierre o una demora, sin pensar en el riesgo.\n\n"
                    + "Y algo que poca gente considera: una decisión puede ser legal y aun así imprudente. Ir a 50 km/h en una calle permitida, "
                    + "pero llena de niños saliendo del colegio, no viola el límite, pero no es seguro."),
                Tip("La verdadera autorregulación aparece cuando nadie te obliga a elegir la opción segura, y aun así la eliges."),
                Scenario(
                    "Hay un trancón por un cierre en un corredor de Barranquilla y vas tarde. Ves tres opciones. ¿Cuál demuestra autorregulación?",
                    [
                        Choice("Me meto por el carril contrario o por la berma para avanzar.", "Es la reacción impulsiva más común, y también la más peligrosa: invades espacios que otros no esperan que ocupes."),
                        Choice("Espero mi turno en la fila o busco una ruta alterna permitida.", "Correcto: aceptas la demora o la resuelves sin crear riesgo. Llegar tarde se puede arreglar; un choque no.", true),
                        Choice("Pito y me pego al carro de adelante para presionar.", "Presionar no hace avanzar la fila; solo sube el estrés de todos y reduce tu distancia para frenar.")
                    ]),
                TrueFalse(
                    "Una conducta puede cumplir la norma y aun así ser imprudente.",
                    true,
                    "Verdadero: la norma marca un mínimo. Un conductor responsable también adapta su conducta al riesgo que ve."),
                Quiz(
                    "Son las 11 de la noche y la avenida está vacía. ¿Qué haría un conductor autorregulado?",
                    null,
                    ["Ir más rápido, porque no hay agentes", "Pasarse el semáforo en rojo si no viene nadie", "Mantener el límite, porque el riesgo sigue ahí", "Usar el celular, porque hay poco tráfico"],
                    2,
                    "De noche hay menos visibilidad y un peatón o un ciclista puede aparecer en cualquier momento. Cumplir aunque nadie vigile es autorregulación.")
            ]
        ),
        (
            "normas-transito/autoridades",
            "Autoridades de tránsito y el Código",
            "Quiénes son las autoridades, tus derechos y deberes en un control, qué hacer en un choque sin heridos y cómo aplicar el Código a un caso real.",
            20,
            [
                Text(
                    "El Código Nacional de Tránsito",
                    "Las reglas para circular están en el Código Nacional de Tránsito Terrestre (Ley 769 de 2002), actualizado por varias leyes posteriores. "
                    + "Aplica a conductores, pasajeros, peatones, ciclistas y a cualquier persona que use la vía.\n\n"
                    + "Se organiza en partes: principios y autoridades, licencias y requisitos de los vehículos, normas de comportamiento en la vía, "
                    + "y al final infracciones, sanciones y procedimiento. No hace falta memorizarlo: hace falta saber aplicarlo."),
                Tip(
                    "Para resolver un caso pregúntate: ¿qué conducta estoy realizando?, ¿qué norma la regula?, ¿hay una medida local vigente? y ¿qué señalización hay en el sitio?"),
                Text(
                    "Las autoridades de tránsito",
                    "La ley las enumera en este orden: el Ministro de Transporte; los gobernadores y alcaldes; los organismos de tránsito departamentales, municipales o distritales; "
                    + "la Policía Nacional a través de su Dirección de Tránsito y Transporte; los inspectores de policía y de tránsito, corregidores o quien haga sus veces; "
                    + "la Superintendencia de Transporte; las Fuerzas Militares, solo donde no haya otra autoridad de tránsito; y los agentes de tránsito y transporte."),
                Text(
                    "Señal fija, señal temporal y orden del agente",
                    "En la vía vas a encontrar tres tipos de indicaciones: las señales fijas (las de siempre), las señales temporales (por obras, eventos o desvíos) "
                    + "y las órdenes directas de una autoridad.\n\n"
                    + "Cuando no coinciden, manda la condición actual: primero el agente, luego el semáforo, después la señal temporal y por último la fija. "
                    + "Un control vial no es una confrontación: es la forma de ordenar la circulación y proteger a todos."),
                Text(
                    "Tus derechos y deberes en un control",
                    "El respeto va en las dos direcciones. Tú debes atender las indicaciones de la autoridad, detenerte cuando te lo ordenen, hablar con respeto y presentar tus documentos. "
                    + "La autoridad debe tratarte con dignidad y seguir el procedimiento que fija la ley (Código de Policía, Ley 1801 de 2016, artículo 171).\n\n"
                    + "Algunas reglas que conviene conocer:\n\n"
                    + "• Puedes grabar el procedimiento con tu celular, con calma y sin estorbar. Ninguna autoridad puede impedirlo sin una justificación legal (Ley 1801 de 2016, artículo 21).\n"
                    + "• Los documentos se pueden presentar en físico o mediante la consulta en el RUNT: la información del RUNT tiene el mismo valor que el papel.\n"
                    + "• La revisión técnico-mecánica la verifica la autoridad en el RUNT; no te pueden exigir el certificado físico (Código de Tránsito, artículo 53).\n"
                    + "• Si te imponen un comparendo, el agente debe entregarte la orden y explicarte cómo y en qué plazo puedes responder.\n"
                    + "• Si el vehículo es inmovilizado, se hace un inventario de lo que hay dentro y de su estado al entrar y al salir del parqueadero.\n\n"
                    + "Si crees que hubo un abuso, no discutas en la vía: guarda la evidencia y preséntala en la audiencia o ante la oficina de control interno o la Procuraduría."),
                Scenario(
                    "Un agente te detiene en un control y te pide los documentos. No llevas el certificado de la revisión técnico-mecánica en papel, pero está vigente. ¿Qué haces?",
                    [
                        Choice("Discuto en voz alta y me niego a mostrar cualquier documento.", "Negarte y alterarte empeora la situación y puede ser una falta de respeto a la autoridad. Tus documentos sí debes presentarlos."),
                        Choice("Presento mi licencia, la licencia de tránsito y el SOAT, y le recuerdo con respeto que la revisión técnico-mecánica se verifica en el RUNT.", "Correcto: cumples tu deber y haces valer tu derecho con respeto. Si quieres, puedes grabar el procedimiento.", true),
                        Choice("Le ofrezco dinero para que me deje ir.", "Ofrecer dinero a un servidor público es un delito (cohecho), aunque los documentos estén en regla.")
                    ]),
                TrueFalse(
                    "Si un agente de tránsito te detiene, puedes grabar el procedimiento con tu celular siempre que no interfieras con su labor.",
                    true,
                    "Verdadero: todo procedimiento de policía puede ser grabado (Ley 1801 de 2016, artículo 21). Grabar con calma también protege al agente que actúa bien."),
                Scenario(
                    "Llegas a un cruce con el semáforo apagado y un agente de tránsito regulando el paso con la mano. ¿Qué haces?",
                    [
                        Choice("Aplico la regla de prelación de la derecha, porque no hay semáforo.", "Habría sido correcto sin agente, pero cuando hay un agente regulando, sus instrucciones están por encima de cualquier otra regla."),
                        Choice("Observo al agente y avanzo o me detengo según lo que indique.", "Correcto: la orden del agente es la que manda en ese momento.", true),
                        Choice("Paso rápido aprovechando que el agente está mirando hacia otro lado.", "Si el agente no te ha dado paso, no lo tienes. Además, los demás conductores están siguiendo sus indicaciones.")
                    ]),
                Text(
                    "Si tienes un choque sin heridos",
                    "Lo primero es confirmar que nadie está herido. Si hay una persona lesionada, no muevas los vehículos: protege la escena y llama al 123, como se explica en el curso «Primeros auxilios en la vía».\n\n"
                    + "Si solo hay daños materiales:\n\n"
                    + "1. Enciende las luces de parqueo y, si hace falta, coloca las señales del equipo de carretera.\n"
                    + "2. Toma fotos de la posición de los vehículos, de los daños, de las placas y del lugar antes de moverlos.\n"
                    + "3. Si los vehículos pueden andar, retíralos de la calzada: dejarlos atravesados bloquea el tránsito y crea nuevos riesgos, y todo usuario debe evitar obstaculizar la vía.\n"
                    + "4. Intercambia datos: nombre, documento, teléfono, placa, licencia y aseguradora.\n"
                    + "5. Si llegan a un acuerdo, déjenlo por escrito. Si no, llamen a la autoridad de tránsito, que elabora el informe del accidente (IPAT); con él el caso puede ir a conciliación.\n\n"
                    + "El SOAT cubre a las personas lesionadas, no los daños de los vehículos. Y nunca te vayas sin dejar tus datos: abandonar el lugar empeora tu situación aunque el daño sea pequeño."),
                Scenario(
                    "En un semáforo, el carro de atrás te golpea suavemente. Nadie está herido, pero los dos carros bloquean un carril de la avenida. El otro conductor quiere esperar al agente sin mover nada. ¿Qué es lo más adecuado?",
                    [
                        Choice("Dejamos los carros donde están hasta que llegue el agente, aunque tarde una hora.", "Si nadie está herido, mantener la vía bloqueada solo crea nuevos riesgos para quienes vienen detrás."),
                        Choice("Tomamos fotos de la posición y los daños, movemos los carros a un lado, intercambiamos datos y, si no hay acuerdo, llamamos a la autoridad.", "Correcto: guardas la evidencia, liberas la vía y conservas la opción del informe si no se ponen de acuerdo.", true),
                        Choice("Como el golpe fue leve, me voy sin decir nada.", "Irte sin dejar tus datos te puede traer consecuencias mucho más graves que el golpe.")
                    ]),
                Order(
                    "Ordena estas autoridades como aparecen en la ley, de la primera a la última.",
                    ["Ministro de Transporte", "Gobernadores y alcaldes", "Organismos de tránsito departamentales y municipales", "Agentes de tránsito y transporte"],
                    "La ley empieza por el Ministro de Transporte y termina con los agentes, que son quienes ves a diario en la calle."),
                TrueFalse(
                    "Las Fuerzas Militares pueden regular el tránsito en zonas donde no hay ninguna otra autoridad de tránsito.",
                    true,
                    "Verdadero: la ley se lo permite solo donde no haya presencia de otra autoridad de tránsito.")
            ]
        ),
        (
            "normas-transito/documentos",
            "Documentos, habilitación y restricciones para circular",
            "Qué verificar antes de mover el vehículo: licencia, SOAT, revisión técnico-mecánica, equipo de carretera y las restricciones locales vigentes.",
            15,
            [
                Text(
                    "La persona y el vehículo",
                    "Antes de arrancar se verifican dos cosas: que el conductor esté habilitado y que el vehículo pueda circular.\n\n"
                    + "El conductor necesita su licencia de conducción vigente y de la categoría correcta para ese vehículo.\n\n"
                    + "El vehículo necesita su licencia de tránsito, el SOAT vigente y, cuando le corresponda, la revisión técnico-mecánica y de emisiones contaminantes. "
                    + "Mantenerlos al día es obligación del propietario, pero quien conduce también responde en un control. "
                    + "Los datos se consultan en el RUNT. Los vehículos de servicio público tienen documentos adicionales, como la tarjeta de operación."),
                Text(
                    "Requisitos para obtener la licencia",
                    "Para conducir un vehículo particular debes saber leer y escribir y tener al menos 16 años. Para servicio público, al menos 18 años.\n\n"
                    + "Además necesitas el certificado de capacitación de un centro de enseñanza automovilística registrado en el RUNT, "
                    + "el certificado de aptitud física, mental y de coordinación motriz expedido por un Centro de Reconocimiento de Conductores (CRC), "
                    + "y aprobar los exámenes teórico y práctico ante una entidad habilitada."),
                Text(
                    "Las placas",
                    "Los carros llevan dos placas iguales, adelante y atrás; las motos, los remolques y los semirremolques llevan una sola. "
                    + "El color indica el servicio: amarillo para particular, blanco para público y azul para diplomático.\n\n"
                    + "Las placas deben leerse completas: nada de marcos, suciedad, bicicletas u otros objetos que tapen letras o números, ni distintivos que las imiten."),
                Text(
                    "¿Cuándo toca la revisión técnico-mecánica?",
                    "Los carros particulares nuevos hacen la primera revisión cuando cumplen cinco años de matriculados. "
                    + "Los vehículos de servicio público y las motocicletas la hacen a los dos años. Después de la primera, se renueva cada año."),
                Scenario(
                    "Un amigo te presta su carro para un mandado. Tienes tu licencia vigente, pero al revisar ves que el SOAT del carro venció la semana pasada. ¿Qué haces?",
                    [
                        Choice("Lo uso igual, porque es un trayecto corto.", "El SOAT no depende de la distancia. Sin él, el vehículo no puede circular y, si ocurre un siniestro, las víctimas quedan sin esa cobertura."),
                        Choice("No lo uso hasta que el SOAT esté renovado.", "Correcto: tu licencia te habilita a ti, pero el vehículo también tiene que estar habilitado.", true),
                        Choice("Lo uso, porque la responsabilidad del SOAT es solo del dueño.", "El propietario debe mantenerlo al día, pero quien conduce un vehículo sin SOAT también se expone a la sanción y a la inmovilización.")
                    ]),
                Text(
                    "¿Puedo circular hoy por aquí?",
                    "Tener los documentos al día no basta: además del Código, cada ciudad puede fijar restricciones por horarios, zonas, tipos de vehículo, obras o eventos. "
                    + "En Barranquilla, por ejemplo, ha habido medidas para motociclistas por horarios y por sectores como el Centro, y cierres por eventos como el Carnaval.\n\n"
                    + "Estas medidas son dinámicas: no las aprendas de memoria. Antes de salir consulta las fuentes oficiales del Distrito y, en la vía, "
                    + "obedece la señalización que encuentres, aunque no coincida con lo que recordabas."),
                Order(
                    "Encuentras un aviso de cierre en tu ruta de siempre. Ordena lo que debes hacer.",
                    [
                        "Reduzco la velocidad y leo el aviso o la señal temporal",
                        "Sigo las indicaciones del desvío o del personal de control",
                        "Busco una ruta alterna permitida",
                        "La próxima vez consulto las medidas vigentes antes de salir"
                    ],
                    "Primero bajas la velocidad para leer bien, luego obedeces el desvío y solo entonces replanteas la ruta. Consultar antes de salir te evita la sorpresa."),
                TrueFalse(
                    "Si una restricción local ya no aparece en las noticias, puedo asumir que terminó.",
                    false,
                    "Falso: las medidas locales se verifican en las fuentes oficiales y en la señalización, no por lo que recuerdes o hayas oído."),
                Text(
                    "El equipo de carretera",
                    "Todo vehículo debe llevar como mínimo: gato, cruceta, dos señales reflectivas en forma de triángulo (o lámparas amarillas intermitentes), "
                    + "botiquín de primeros auxilios, extintor, dos tacos para bloquear las llantas, caja de herramientas básica (alicate, destornilladores, llave de expansión y llaves fijas), "
                    + "llanta de repuesto y linterna."),
                Classify(
                    "¿Lo exige el código como equipo de carretera?",
                    ["Es obligatorio", "No es obligatorio"],
                    [
                        ("Extintor", 0),
                        ("Botiquín de primeros auxilios", 0),
                        ("Dos tacos para bloquear las llantas", 0),
                        ("Linterna", 0),
                        ("Llanta de repuesto", 0),
                        ("Cables para pasar corriente", 1),
                        ("Ambientador", 1),
                        ("Parasol para el panorámico", 1)
                    ],
                    "Los cables de arranque o el parasol pueden ser útiles, pero el código no los exige. El extintor, el botiquín, los tacos, la linterna y el repuesto sí."),
                FillBlank(
                    "El seguro obligatorio que debe tener todo vehículo se llama [[SOAT]]. Un carro particular nuevo hace su primera revisión técnico-mecánica a los [[5|cinco]] años, y una moto nueva a los [[2|dos]] años.",
                    ["3", "10", "RUNT"],
                    "Carro particular nuevo: primera revisión a los 5 años. Motos y servicio público: a los 2 años. Después, cada año.")
            ]
        ),
        (
            "normas-transito/velocidad",
            "Velocidad segura y adaptación al entorno",
            "La diferencia entre el límite de velocidad y la velocidad segura, y los límites que fija la ley.",
            17,
            [
                Text(
                    "Los límites de la ley",
                    "En vías urbanas la autoridad local fija el límite, pero nunca puede pasar de 50 km/h. En zonas escolares y residenciales el máximo es 30 km/h.\n\n"
                    + "En carreteras nacionales y departamentales el límite no puede superar los 90 km/h, salvo en dobles calzadas sin pasos peatonales, "
                    + "donde puede llegar a 120 km/h. Los vehículos de servicio público de carga nunca pueden pasar de 80 km/h.\n\n"
                    + "Además, debes bajar a 30 km/h donde se concentran personas, en zonas residenciales y escolares, con poca visibilidad, "
                    + "cuando una señal lo ordene y al acercarte a una intersección."),
                Text(
                    "El límite no es la velocidad segura",
                    "El límite es el máximo permitido, no una obligación de ir a esa velocidad. La velocidad segura depende de lo que ves: visibilidad, lluvia, estado del pavimento, "
                    + "cantidad de tráfico, obras y, sobre todo, quién está cerca. Un peatón o un motociclista es mucho más vulnerable que otro carro.\n\n"
                    + "Piensa en la Circunvalar en un día despejado y en una calle residencial a la salida del colegio: el límite puede parecer similar, pero el riesgo no. "
                    + "Por eso hay intervenciones como los radares pedagógicos de la carrera 65A con calle 99: buscan proteger a peatones, ciclistas y conductores."),
                Tip("Más riesgo significa menos margen para reaccionar. Y menos margen significa que debes ir más despacio."),
                Video("experimento-30-kmh.mp4", "Experimento real: 30 kilómetros por hora hacen la diferencia."),
                Video("exceso-velocidad.mp4", "Exceso de velocidad: un factor de riesgo que sí podemos controlar."),
                new
                {
                    type = "signs",
                    title = "Señales relacionadas",
                    items = new[]
                    {
                        Sign("SR-30", "Velocidad máxima permitida", "El número es el límite en km/h para ese tramo."),
                        Sign("SP-47", "Zona escolar", "Prepárate para bajar a 30 km/h o menos.")
                    }
                },
                TrueFalse(
                    "Si la señal dice 50 km/h, ir a 50 km/h siempre es seguro.",
                    false,
                    "Falso: 50 es el máximo con buenas condiciones. Con lluvia, peatones o poca visibilidad, la velocidad segura es menor."),
                Classify(
                    "Pon cada situación en el límite máximo que le corresponde.",
                    ["30 km/h", "50 km/h", "90 km/h"],
                    [
                        ("Frente a un colegio", 0),
                        ("En un barrio residencial", 0),
                        ("Con niebla espesa en la ciudad", 0),
                        ("Avenida urbana sin otra señal", 1),
                        ("Vía arteria urbana con señal de 50", 1),
                        ("Carretera nacional de una calzada", 2),
                        ("Vía departamental en buen estado", 2)
                    ],
                    "Zonas escolares, residenciales y con poca visibilidad: 30. Ciudad: máximo 50. Carretera: máximo 90, salvo dobles calzadas sin pasos peatonales."),
                FillBlank(
                    "En la ciudad el límite nunca puede superar los [[50]] km/h. En zona escolar el máximo es [[30]] km/h y en una doble calzada sin pasos peatonales puede llegar a [[120]] km/h.",
                    ["60", "80", "100"],
                    "Ciudad: 50. Zona escolar o residencial: 30. Doble calzada sin pasos peatonales: hasta 120."),
                Scenario(
                    "Vas a 50 km/h por una avenida y empieza a llover muy fuerte; casi no ves los carros de adelante. ¿Qué haces?",
                    [
                        Choice("Mantengo los 50 km/h, porque es el límite de la vía.", "El límite es para buenas condiciones. Con poca visibilidad la norma te obliga a bajar a 30 km/h; a 50 no alcanzarías a frenar a tiempo."),
                        Choice("Bajo a 30 km/h o menos, enciendo las luces y aumento la distancia con el carro de adelante.", "Correcto: con poca visibilidad debes reducir a 30 km/h, y más distancia te da tiempo para frenar en piso mojado.", true),
                        Choice("Enciendo las luces de parqueo y sigo a la misma velocidad.", "Las estacionarias no reemplazan reducir la velocidad, y en movimiento confunden a los demás conductores.")
                    ]),
                Quiz(
                    "¿Por qué la velocidad influye en la gravedad de un siniestro y no solo en la posibilidad de que ocurra?",
                    null,
                    ["Porque a más velocidad el carro pesa más", "Porque a más velocidad hay más energía en el impacto y más distancia para detenerse", "No influye: la gravedad depende solo del tipo de vehículo", "Porque los frenos funcionan peor de día"],
                    1,
                    "A mayor velocidad recorres más metros mientras reaccionas y frenas, y el golpe libera mucha más energía. Para un peatón, la diferencia entre 30 y 50 km/h puede ser la diferencia entre vivir o no.")
            ]
        ),
        (
            "normas-transito/prelacion",
            "Prelación: quién pasa primero",
            "Peatones, vehículos de emergencia, intersecciones sin señal, glorietas y pendientes. Y qué hacer aunque tengas la prioridad.",
            12,
            [
                Text(
                    "Primero las personas",
                    "Los conductores de vehículos motorizados deben respetar los derechos y la integridad de los peatones, los ciclistas y los usuarios de vehículos eléctricos livianos, "
                    + "y darles prelación en la vía. Quien va en el vehículo más grande tiene más responsabilidad, porque puede hacer más daño."),
                Text(
                    "Las reglas de prelación",
                    "En una intersección sin señales (que no sea glorieta) pasa primero el vehículo que viene por tu derecha.\n\n"
                    + "Si dos vehículos llegan de frente y uno va a girar a la izquierda, pasa primero el que sigue derecho.\n\n"
                    + "Dentro de una glorieta tiene prelación quien ya circula en ella, siempre que esté en movimiento.\n\n"
                    + "En una pendiente donde no caben los dos, tiene prelación el vehículo que sube.\n\n"
                    + "Ante una ambulancia, los bomberos o la policía con luces o sirena, te orillas a la derecha y te detienes hasta que pasen."),
                Tip("Tener la prelación no te autoriza a ignorar el riesgo. Si el otro no te cede el paso, frenar a tiempo vale más que tener la razón."),
                Scenario(
                    "Llegas a un cruce sin semáforo ni señales. Al mismo tiempo, un carro llega por la vía de tu derecha. ¿Qué haces?",
                    [
                        Choice("Acelero para cruzar primero.", "En un cruce sin señales tiene prelación quien viene por la derecha. Acelerar puede terminar en un choque lateral."),
                        Choice("Le cedo el paso y cruzo cuando haya pasado.", "Correcto: en intersecciones sin señalizar pasa primero el vehículo de la derecha.", true),
                        Choice("Pito para que el otro me deje pasar.", "Pitar no te da prelación. La regla dice que pasa el de la derecha.")
                    ]),
                Scenario(
                    "El semáforo cambia a verde para ti, pero un peatón mayor todavía va a mitad de la cebra. ¿Qué haces?",
                    [
                        Choice("Arranco despacio para que se apure.", "Presionar a un peatón que ya está cruzando lo pone en riesgo; puede tropezar o detenerse de golpe."),
                        Choice("Pito para avisarle que ya tengo verde.", "El pito asusta y no resuelve nada. Tener verde no te da derecho a pasar por encima de alguien."),
                        Choice("Espero a que termine de cruzar y luego arranco.", "Correcto: tienes la prelación, pero la seguridad del peatón está primero.", true)
                    ]),
                Scenario(
                    "Vas a entrar a una glorieta y un carro ya va circulando dentro, acercándose a tu entrada. ¿Qué haces?",
                    [
                        Choice("Espero a que pase y entro cuando haya espacio.", "Correcto: quien ya está en la glorieta y en movimiento tiene prelación sobre quien va a entrar.", true),
                        Choice("Entro rápido, porque yo llegué primero a la entrada.", "Llegar primero a la entrada no te da prelación. Quien circula dentro de la glorieta pasa primero."),
                        Choice("Me detengo dentro de la glorieta para decidir por dónde salir.", "Detenerte dentro de la glorieta bloquea el tráfico; decide tu salida antes de entrar.")
                    ]),
                Order(
                    "Escuchas una sirena detrás de ti. Ordena lo que debes hacer.",
                    [
                        "Identifico de dónde viene la sirena y miro los espejos",
                        "Pongo la direccional derecha",
                        "Me orillo al costado derecho de la vía",
                        "Me detengo hasta que pase el vehículo de emergencia",
                        "Reviso los espejos y me reincorporo con cuidado"
                    ],
                    "Primero ubica la emergencia, avisa con la direccional, oríllate a la derecha, detente y reincorpórate solo cuando sea seguro."),
                TrueFalse(
                    "En una pendiente angosta donde no caben dos carros, tiene prelación el que baja.",
                    false,
                    "Falso: tiene prelación el que sube, porque arrancar de nuevo en subida es más difícil y peligroso."),
                Quiz(
                    "Dos carros llegan de frente a una intersección: uno va a seguir derecho y el otro va a girar a la izquierda. ¿Quién pasa primero?",
                    null,
                    ["El que gira a la izquierda", "El que va más rápido", "El que llegó primero", "El que sigue derecho"],
                    3,
                    "Cuando uno de los dos va a girar a la izquierda, tiene prelación el que sigue derecho.")
            ]
        ),
        (
            "normas-transito/adelantar",
            "Adelantar, carriles y estacionamiento",
            "Dónde no se puede adelantar, cómo usar los carriles exclusivos y preferenciales, y dónde no se estaciona.",
            14,
            [
                Text(
                    "Adelantar es la maniobra más peligrosa",
                    "Para adelantar invades por unos segundos el carril contrario. No se debe adelantar en intersecciones, donde hay línea central continua o una señal que lo prohíbe, "
                    + "en curvas o pendientes, con mala visibilidad, cerca de pasos peatonales, en cruces de vías férreas, por la berma o por la derecha de otro vehículo, "
                    + "y en general siempre que la maniobra sea peligrosa."),
                Classify(
                    "¿Se puede adelantar en cada caso?",
                    ["Prohibido", "Permitido si es seguro"],
                    [
                        ("En una curva sin visibilidad", 0),
                        ("Con línea central amarilla continua", 0),
                        ("Llegando a un paso peatonal", 0),
                        ("Por la berma", 0),
                        ("Al subir una pendiente", 0),
                        ("En una recta con línea discontinua y buena visibilidad", 1),
                        ("Por la izquierda, con el carril contrario despejado y sin señal que lo prohíba", 1)
                    ],
                    "Solo adelanta en tramos rectos, con línea discontinua, buena visibilidad y el carril contrario libre. Nunca por la derecha ni por la berma."),
                Scenario(
                    "Vas detrás de un camión muy lento en una carretera de montaña. Viene una curva y la línea central es continua. ¿Qué haces?",
                    [
                        Choice("Lo adelanto rápido antes de la curva.", "Con línea continua y una curva adelante está prohibido adelantar; si viene alguien de frente no tendrás dónde meterte."),
                        Choice("Lo adelanto por la berma derecha.", "Adelantar por la berma está prohibido y pone en riesgo a peatones, ciclistas y al propio camión."),
                        Choice("Mantengo distancia y espero un tramo recto con línea discontinua.", "Correcto: unos minutos de paciencia valen más que una maniobra prohibida en curva.", true)
                    ]),
                Text(
                    "Carriles generales, preferenciales y exclusivos",
                    "Cada carril tiene una función. Los carriles exclusivos (por ejemplo, los de transporte masivo o las ciclorrutas) solo los pueden usar los vehículos autorizados. "
                    + "Los preferenciales dan prioridad a cierto tipo de vehículo, pero pueden tener reglas de uso compartido según la señalización.\n\n"
                    + "Antes de girar, ubícate con anticipación en el carril más cercano a tu giro, sin invadir el exclusivo. "
                    + "Y lee siempre la señalización del momento: una obra o un evento puede cambiar la organización de los carriles (pasa, por ejemplo, en la Vía 40 o en el Gran Malecón)."),
                Scenario(
                    "Vas por una avenida de tres carriles; el de la derecha es exclusivo para buses y necesitas girar a la derecha en dos cuadras. ¿Qué haces?",
                    [
                        Choice("Me paso al carril exclusivo desde ya para no quedar atrapado.", "Circular por el carril exclusivo está prohibido y pone en riesgo a los buses y a sus pasajeros."),
                        Choice("Sigo por el carril general más cercano y solo cruzo el exclusivo donde la señalización lo permita para girar.", "Correcto: te preparas con tiempo y usas el carril exclusivo únicamente donde está permitido cruzarlo.", true),
                        Choice("Giro desde el carril del centro cuando llegue a la esquina.", "Girar desde un carril que no es el más cercano al giro corta el paso a los demás y es una causa frecuente de choques.")
                    ]),
                Text(
                    "Dónde y cómo estacionar",
                    "Donde se permite estacionar, el vehículo debe quedar del lado autorizado, a no más de 30 centímetros del andén y a por lo menos 5 metros de la intersección.\n\n"
                    + "Está prohibido estacionar, entre otros lugares: sobre andenes y zonas verdes; dentro de un cruce; en puentes y túneles; en paraderos de servicio público "
                    + "o zonas para personas con discapacidad; en carriles de transporte masivo; en ciclorrutas; en doble fila; frente a hidrantes y entradas de garajes; y en curvas."),
                new
                {
                    type = "hotspot",
                    instructions = "Toca en la imagen los seis lugares donde estaría prohibido dejar el carro estacionado.",
                    imageUrl = "/courses/cruce-estacionar.svg",
                    spots = new[]
                    {
                        Spot(52.5, 53, "Dentro del cruce", "Un carro detenido en la intersección bloquea a todos los que cruzan."),
                        Spot(20, 68, "Frente al hidrante", "Los bomberos necesitan acceso inmediato al hidrante."),
                        Spot(20, 37.5, "Entrada de garaje", "Impide que otros vehículos entren o salgan."),
                        Spot(82.5, 63.5, "Paradero de buses", "Es una zona reservada para que el transporte público recoja pasajeros."),
                        Spot(82.5, 37.5, "Sobre el andén", "El andén es de los peatones; nunca se estaciona encima."),
                        Spot(45, 16, "Ciclorruta", "Obliga a los ciclistas a salir al carril de los carros.")
                    }
                },
                FillBlank(
                    "Al estacionar, el carro debe quedar a no más de [[30]] centímetros del andén y a mínimo [[5|cinco]] metros de la intersección.",
                    ["50", "10", "2"],
                    "30 centímetros del andén y al menos 5 metros de la esquina, para no tapar la visibilidad de quienes cruzan."),
                TrueFalse(
                    "Si solo te vas a demorar dos minutos, puedes estacionar en doble fila con las luces de parqueo encendidas.",
                    false,
                    "Falso: estacionar en doble fila está prohibido sin importar el tiempo, y las luces de parqueo no lo hacen permitido.")
            ]
        ),
        (
            "normas-transito/factores-humanos",
            "Factores humanos, alcohol y sustancias",
            "Fatiga, sueño, estrés, distracciones y alimentación; y por qué con alcohol o sustancias la única decisión segura es no conducir.",
            80,
            [
                Text(
                    "El primer sistema de seguridad eres tú",
                    "La fatiga y el sueño hacen que reacciones tarde, igual que el alcohol. Si sientes los párpados pesados, bostezas seguido o no recuerdas los últimos kilómetros, "
                    + "detente en un sitio seguro y descansa.\n\n"
                    + "El estrés y la presión por llegar te llevan a acelerar, pitar o cerrar a otros. El calor, la lluvia y los trancones largos lo empeoran.\n\n"
                    + "Una comida muy pesada antes de manejar da sueño; un estómago vacío baja la concentración. Lo mejor es comer liviano e hidratarse."),
                Text(
                    "El semáforo de aptitud",
                    "Antes de arrancar, evalúate:\n\n"
                    + "Verde: descansado, tranquilo y concentrado. Puedes conducir.\n"
                    + "Amarillo: algo cansado, estresado o con malestar. Haz una pausa o ajusta el plan antes de salir.\n"
                    + "Rojo: con sueño intenso, bajo efectos de alcohol, sustancias o un medicamento que afecte tu capacidad. No conduces."),
                Classify(
                    "Clasifica cada estado según el semáforo de aptitud.",
                    ["Verde: puedo conducir", "Amarillo: pausa o ajuste", "Rojo: no conduzco"],
                    [
                        ("Dormí bien y voy con tiempo", 0),
                        ("Me siento tranquilo y concentrado", 0),
                        ("Llevo dos horas en trancón y me siento irritado", 1),
                        ("Almorcé muy pesado y me da algo de sueño", 1),
                        ("Llevo casi 24 horas sin dormir", 2),
                        ("Me tomé unas cervezas en el almuerzo", 2),
                        ("Tomé un medicamento que advierte no conducir", 2)
                    ],
                    "En amarillo todavía puedes corregir con una pausa. En rojo no hay ajuste posible: se busca otra forma de desplazarse."),
                Text(
                    "Distracciones: los ojos, las manos y la cabeza",
                    "Una distracción es cualquier cosa que aparta tus ojos de la vía, tus manos del volante o tu mente de la conducción. El celular hace las tres cosas a la vez: "
                    + "leer un mensaje durante cinco segundos a 60 km/h es recorrer más de 80 metros sin mirar.\n\n"
                    + "El Código Nacional de Tránsito sanciona usar el celular mientras se conduce, salvo con un sistema de manos libres. Aun así, una conversación exigente con manos libres también reduce tu atención.\n\n"
                    + "Otras distracciones frecuentes: programar el GPS en marcha, comer, arreglarse en el espejo, buscar algo que se cayó, discutir con un pasajero o mirar un choque al lado de la vía.\n\n"
                    + "Qué hacer: configura la ruta antes de arrancar, deja el celular en silencio o fuera de tu alcance y, si necesitas usarlo, detente en un lugar seguro. "
                    + "Un semáforo en rojo no es un lugar seguro: sigues conduciendo."),
                Scenario(
                    "Vas por una avenida y suena el celular: es un mensaje de tu jefe que dice «urgente». ¿Qué haces?",
                    [
                        Choice("Lo leo rápido en el próximo semáforo en rojo.", "En el semáforo sigues conduciendo: el cambio de luz, un peatón o una moto te pueden sorprender con la vista en la pantalla."),
                        Choice("Sigo conduciendo y lo respondo cuando pueda detenerme en un lugar seguro.", "Correcto: ningún mensaje vale más que tu atención en la vía. Unos minutos de espera no cambian la urgencia.", true),
                        Choice("Lo leo con el celular en la pierna para que no se note.", "Bajar la vista a la pierna es igual de peligroso, y además te obliga a quitar una mano del volante.")
                    ]),
                Text(
                    "Alcohol y sustancias: no hay cantidad segura",
                    "El alcohol, las sustancias psicoactivas y algunos medicamentos alteran la percepción, el juicio y la reacción. Desde 20 miligramos de etanol por cada 100 mililitros de sangre ya hay sanción:\n\n"
                    + "Grado cero: de 20 a 39 mg/100 ml.\nPrimer grado: de 40 a 99 mg/100 ml.\nSegundo grado: de 100 a 149 mg/100 ml.\nTercer grado: 150 mg/100 ml o más.\n\n"
                    + "En todos los casos se retiene la licencia y el vehículo se inmoviliza. Si te niegas a hacer la prueba o huyes del control, te cancelan la licencia, "
                    + "la multa es de 1.440 salarios mínimos diarios y el vehículo queda inmovilizado 20 días hábiles (Ley 1696 de 2013). Las multas por alcohol no tienen el descuento por hacer el curso.\n\n"
                    + "Y si causas la muerte de alguien conduciendo con alcohol o drogas, la pena por homicidio culposo aumenta de dos terceras partes al doble."),
                Tip("No intentes calcular cuándo «ya puedes» manejar, y no confíes en sentirte bien. Si vas a tomar, planea desde antes cómo vas a volver."),
                Video("clase-alcoholemia-0.mp4", "Clase de alcoholemia y pruebas, parte 1 de 5."),
                Video("clase-alcoholemia-1.mp4", "Clase de alcoholemia y pruebas, parte 2 de 5."),
                Video("clase-alcoholemia-2.mp4", "Clase de alcoholemia y pruebas, parte 3 de 5."),
                Video("clase-alcoholemia-3.mp4", "Clase de alcoholemia y pruebas, parte 4 de 5."),
                Video("clase-alcoholemia-4.mp4", "Clase de alcoholemia y pruebas, parte 5 de 5."),
                FillBlank(
                    "El grado cero de alcoholemia empieza en [[20]] mg de etanol por 100 ml de sangre. El tercer grado empieza en [[150]] mg.",
                    ["0", "50", "100"],
                    "Grado cero: 20 a 39. Primer grado: 40 a 99. Segundo: 100 a 149. Tercero: 150 o más."),
                TrueFalse(
                    "Tomarse un café cargado después de beber permite manejar sin riesgo.",
                    false,
                    "Falso: el café puede hacerte sentir más despierto, pero el alcohol sigue en tu sangre y tus reflejos siguen afectados."),
                Scenario(
                    "Llevaste tu carro a una celebración de Carnaval y te tomaste unos tragos. Un amigo te dice: «Tranquilo, estás bien, vámonos». ¿Qué haces?",
                    [
                        Choice("Manejo despacio y con cuidado.", "Manejar «con cuidado» bajo efectos no existe: tus reflejos siguen afectados. Arriesgas vidas, la inmovilización del carro y la licencia."),
                        Choice("Pido un taxi o un conductor elegido y recojo el carro al día siguiente.", "Es la decisión correcta: nadie sale lastimado y no arriesgas tu licencia.", true),
                        Choice("Me tomo un café, espero 15 minutos y manejo.", "Quince minutos y un café no bajan el alcohol en la sangre. Seguirías conduciendo con los reflejos afectados.")
                    ])
            ]
        ),
        (
            "normas-transito/carga",
            "Dimensiones, pesos y elementos de seguridad de la carga",
            "Cómo llevar carga sin tapar las luces, sin exceder lo que el vehículo soporta y sin que se caiga en la vía.",
            12,
            [
                Text(
                    "La carga también es parte del vehículo",
                    "El Código exige que la carga no supere la capacidad del vehículo, que vaya bien sujeta y que no arrastre, no se caiga ni oculte las luces, las placas o los espejos. "
                    + "Un objeto suelto en un frenazo se convierte en un proyectil para quien va adentro y en un obstáculo para quien va detrás.\n\n"
                    + "El peso y el volumen los define el fabricante, no las ganas de «que quepa». Más peso alarga la distancia de frenado y puede reventar una llanta o doblar la suspensión."),
                Flip(
                    "Lo que debe cumplirse",
                    Card("Peso", "No pases la capacidad de carga del vehículo. Reparte el peso y pon lo más pesado abajo y centrado."),
                    Card("Sujeción", "Amarra la carga a los puntos del vehículo. Una lona suelta no reemplaza las correas."),
                    Card("Visibilidad", "No tapes luces, direccionales, placa ni espejos. El de atrás tiene que verte frenar."),
                    Card("Si sobresale", "De día se señala con un banderín rojo; de noche, con una luz roja. No puede arrastrar ni barrer la vía."),
                    Card("Lo que no se lleva", "Combustible en recipientes abiertos, carga que gotee y objetos sueltos en la cabina.")),
                Classify(
                    "¿Esta forma de llevar carga es correcta?",
                    ["Correcta", "Incorrecta"],
                    [
                        ("Correas en los puntos de anclaje y peso repartido", 0),
                        ("Lo pesado abajo y centrado", 0),
                        ("Banderín rojo si la carga sobresale de día", 0),
                        ("Un colchón amarrado con una sola cuerda floja", 1),
                        ("Canecas de gasolina destapadas en el baúl", 1),
                        ("La carga tapando la luz de freno", 1),
                        ("Más peso del que indica el fabricante", 1)
                    ],
                    "Si se mueve, gotea o tapa una luz, ya no es una carga: es un riesgo para ti y para los demás."),
                Scenario(
                    "Vas a llevar una nevera en un carro particular. Con el baúl abierto sobresale casi un metro y no tienes cómo señalarla. ¿Qué haces?",
                    [
                        Choice("La saco despacio por calles internas, sin señalarla.", "Una carga que sobresale sin señal es invisible para quien viene detrás, sobre todo de noche."),
                        Choice("Busco un vehículo o un servicio que pueda llevarla cerrada y sujeta.", "Correcto: si no cabe ni se puede señalar, no se transporta en ese carro.", true),
                        Choice("La dejo en el asiento de atrás, sin amarrar, y cierro el baúl.", "En un frenazo la nevera sigue hacia adelante y puede golpear a quien va adelante.")
                    ]),
                TrueFalse(
                    "Si la carga cabe en el baúl, no importa que pese más de lo que el fabricante autoriza.",
                    false,
                    "Falso: el límite es de peso y de volumen. El exceso alarga el frenado y puede dañar llantas y suspensión."),
                Quiz(
                    "¿Qué debe verse siempre, aunque el vehículo lleve carga?",
                    null,
                    ["Solo la placa delantera", "Las luces, las direccionales, las placas y los espejos", "Únicamente el espejo izquierdo", "Ninguna: la carga tiene prioridad"],
                    1,
                    "Quien viene detrás necesita ver tus luces de freno, y tú necesitas los espejos para cambiar de carril.")
            ]
        ),
        (
            "normas-transito/infracciones",
            "Infracciones y comparendos",
            "Por qué existen las normas, qué es un comparendo, los tipos de multa y cómo obtener descuentos.",
            10,
            [
                Text(
                    "Norma, riesgo y consecuencia",
                    "Cada norma existe para evitar un riesgo concreto. Estacionar en una esquina tapa la visibilidad de quienes cruzan; conducir sin licencia significa que nadie verificó que sabes hacerlo.\n\n"
                    + "Ante una conducta, pregúntate tres cosas: ¿qué norma afecta?, ¿qué riesgo genera? y ¿qué consecuencia puede tener? "
                    + "Así entiendes la norma en lugar de memorizar multas."),
                Text(
                    "Comparendo no es lo mismo que sanción",
                    "El comparendo es la orden que te entrega la autoridad para que respondas por una presunta infracción. La sanción es la decisión final: llega si aceptas la infracción "
                    + "o si, después del proceso, la autoridad concluye que la cometiste. Puede ser una multa, la suspensión o cancelación de la licencia, o la inmovilización del vehículo. "
                    + "Si reincides, las sanciones aumentan.\n\n"
                    + "Las multas se miden en salarios mínimos legales diarios vigentes (SMLDV): tipo A, 4; tipo B, 8; tipo C, 15; tipo D, 30; tipo E, 45. "
                    + "El tipo A aplica a vehículos no automotores o de tracción animal. El alcohol tiene sus propias sanciones, más duras."),
                Text(
                    "Si aceptas el comparendo",
                    "Tienes dos opciones con descuento, siempre que hagas un curso sobre normas de tránsito en un organismo de tránsito, "
                    + "un Centro de Enseñanza Automovilística o un Centro Integral de Atención registrado en el RUNT:\n\n"
                    + "Pagar el 50% de la multa dentro de los 5 días siguientes al comparendo.\n"
                    + "Pagar el 75% dentro de los 20 días siguientes.\n\n"
                    + "Si no pagas en esos plazos, pagas el 100% más intereses. Si no estás de acuerdo, puedes presentarte ante la autoridad a defenderte."),
                Text(
                    "Si no estás de acuerdo con el comparendo",
                    "El comparendo es una citación, no una prueba de que cometiste la infracción. Si no la aceptas, te presentas ante el organismo de tránsito dentro del plazo indicado, "
                    + "solicitas la audiencia, das tus descargos y aportas pruebas (fotos, videos, testigos). Puedes ir con abogado, pero no es obligatorio.\n\n"
                    + "Motivos frecuentes para controvertir un comparendo:\n\n"
                    + "• El agente no siguió el procedimiento.\n"
                    + "• La fotomulta viene de una cámara no autorizada o sin la señal que avisa su presencia.\n"
                    + "• Te atribuyen la infracción como propietario, pero no ibas conduciendo: debe identificarse a quien conducía.\n"
                    + "• Te sancionan por alcohol sin haberte practicado la prueba.\n"
                    + "• La conducta no está descrita como infracción en el Código, por ejemplo, hablar por un dispositivo de manos libres.\n\n"
                    + "Ojo: mientras decides, corren los plazos de descuento. Si vas a defenderte, hazlo a tiempo."),
                Order(
                    "Ordena los tipos de multa de la más baja a la más alta.",
                    ["Tipo A: 4 SMLDV", "Tipo B: 8 SMLDV", "Tipo C: 15 SMLDV", "Tipo D: 30 SMLDV", "Tipo E: 45 SMLDV"],
                    "Las letras van de menor a mayor gravedad: 4, 8, 15, 30 y 45 salarios mínimos diarios."),
                FillBlank(
                    "Si aceptas el comparendo y haces el curso, pagas el [[50%|50|cincuenta por ciento]] de la multa dentro de los primeros [[5|cinco]] días, o el [[75%|75|setenta y cinco por ciento]] dentro de los primeros 20 días.",
                    ["25%", "10", "100%"],
                    "50% hasta 5 días después, 75% hasta 20 días después, siempre con el curso. Después de eso, el 100% más intereses."),
                TrueFalse(
                    "Una multa por conducir con alcohol también tiene un 50% de descuento si haces el curso en los primeros 5 días.",
                    false,
                    "Falso: las multas por alcoholemia no tienen la reducción por curso."),
                Quiz(
                    "¿Qué diferencia hay entre un comparendo y una sanción?",
                    null,
                    ["Son lo mismo", "El comparendo es la orden para responder por una presunta infracción; la sanción es la decisión final", "La sanción la pone el agente en la calle y el comparendo el juez", "El comparendo solo aplica a motos"],
                    1,
                    "El comparendo inicia el proceso; la sanción se impone cuando aceptas la infracción o cuando la autoridad la declara al final del proceso."),
                Scenario(
                    "Un agente te hace un comparendo por estacionar en un sitio prohibido. Sabes que cometiste la infracción. ¿Qué te conviene más?",
                    [
                        Choice("Ignorarlo y esperar a que se olvide.", "El comparendo no desaparece: después de los plazos pagarás el 100% más intereses y quedará reportado."),
                        Choice("Hacer el curso y pagar dentro de los 5 días siguientes.", "Es lo más conveniente: pagas solo el 50% de la multa y además repasas las normas.", true),
                        Choice("Pagar el 100% cuando me acuerde.", "Pagar es lo correcto, pero pierdes el descuento y pueden sumarse intereses si te demoras.")
                    ])
            ]
        )
    ];
}
