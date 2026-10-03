namespace Cale.Api.Services.Courses;

/// <summary>
/// "Primeros auxilios en la vía": lay first-responder guidance only (no clinical procedures),
/// following the topics of the school's own presentation.
/// </summary>
public sealed partial class CourseSeed
{
    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> FirstAidLessons() =>
    [
        (
            "El primer respondiente",
            "Quién es el primer respondiente, la regla de oro de no convertirte en otra víctima y el deber de ayudar.",
            8,
            [
                Text(
                    "Tú puedes ser el primero en llegar",
                    "El primer respondiente es la primera persona que decide ayudar a alguien que sufrió un accidente o una emergencia de salud, mientras llega la ayuda profesional.\n\n"
                    + "No necesitas ser médico. Tu papel es conservar la vida de la persona, evitar que empeore y conseguir que la atiendan lo antes posible. "
                    + "En un siniestro vial los primeros minutos son los más importantes: muchas muertes evitables ocurren en la primera hora."),
                Text(
                    "Proteger, Avisar, Socorrer",
                    "Toda atención sigue este orden:\n\n"
                    + "Proteger: asegura el lugar para que no haya más víctimas, empezando por ti.\n"
                    + "Avisar: llama a la línea de emergencias 123 y da información clara.\n"
                    + "Socorrer: solo entonces atiendes a la persona, con lo que sabes hacer."),
                Tip("Regla de oro: evita ser tú la siguiente víctima. Y si no te sientes capaz de hacer un procedimiento, no lo hagas: avisar y acompañar también salva vidas."),
                Text(
                    "Ayudar es un deber",
                    "Negarse a ayudar, sin justa causa, a una persona cuya vida o salud está en grave peligro es un delito en Colombia: la omisión de socorro (artículo 131 del Código Penal), "
                    + "castigada con prisión. Ayudar no siempre significa tocar a la víctima: llamar al 123 y señalizar el lugar ya es socorrer."),
                Order(
                    "Ordena las tres acciones como debes hacerlas al llegar a un accidente.",
                    ["Proteger el lugar", "Avisar al 123", "Socorrer a la víctima"],
                    "Primero proteges (para que no haya más víctimas), luego avisas y por último socorres."),
                TrueFalse(
                    "Si no sé primeros auxilios, lo mejor es seguir de largo sin hacer nada.",
                    false,
                    "Falso: siempre puedes proteger el lugar, llamar al 123 y acompañar a la víctima. Negarse a ayudar a alguien en grave peligro puede ser un delito."),
                Tip("Este curso es una orientación básica para conductores. No reemplaza un curso certificado de primeros auxilios, que te recomendamos tomar.")
            ]
        ),
        (
            "Proteger la escena y avisar",
            "Cómo señalizar un accidente, qué peligros revisar y qué decir cuando llamas al 123.",
            10,
            [
                Text(
                    "Proteger",
                    "Estaciona tu vehículo en un lugar seguro, fuera de la calzada si es posible y sin bloquear el paso de las ambulancias. Enciende las luces de parqueo.\n\n"
                    + "Ponte un chaleco reflectivo si tienes y coloca las señales triangulares del equipo de carretera detrás del accidente, a una distancia suficiente "
                    + "para que los demás alcancen a frenar (más lejos en carretera, en curvas o de noche).\n\n"
                    + "Revisa peligros: fuego, olor a combustible, cables eléctricos, otros vehículos que pasan. Apaga el motor de los vehículos accidentados y no permitas que nadie fume cerca."),
                Text(
                    "Avisar",
                    "Llama al 123. Habla con calma y di:\n\n"
                    + "Dónde estás: dirección, vía, kilómetro o un punto de referencia.\n"
                    + "Qué pasó: choque, atropello, caída de moto, incendio.\n"
                    + "Cuántas personas están heridas y cómo están: si responden y si respiran.\n"
                    + "Si hay peligros: fuego, combustible, personas atrapadas.\n\n"
                    + "No cuelgues hasta que el operador te lo indique: te puede dar instrucciones."),
                Classify(
                    "¿A qué paso corresponde cada acción?",
                    ["Proteger", "Avisar", "Socorrer"],
                    [
                        ("Encender las luces de parqueo", 0),
                        ("Colocar los triángulos detrás del accidente", 0),
                        ("Apagar el motor del carro accidentado", 0),
                        ("Llamar al 123", 1),
                        ("Dar la dirección y un punto de referencia", 1),
                        ("Revisar si la víctima respira", 2),
                        ("Presionar una herida que sangra", 2)
                    ],
                    "Proteger evita más víctimas, avisar trae la ayuda profesional y socorrer es atender a la persona."),
                Scenario(
                    "Llegas a un choque en una avenida de noche. Hay una persona herida dentro de un carro y huele a gasolina. ¿Qué haces primero?",
                    [
                        Choice("Corro hacia el carro para sacar a la persona.", "Sin proteger la escena puedes ser atropellado, y mover a un herido puede agravar una lesión de columna. Con olor a combustible, además, el riesgo de incendio es alto."),
                        Choice("Estaciono seguro, enciendo las luces de parqueo, señalizo, pido que nadie fume y llamo al 123 avisando del olor a gasolina.", "Correcto: proteges la escena y avisas del peligro. Solo moverías a la persona si hubiera fuego o un riesgo inmediato para su vida.", true),
                        Choice("Tomo fotos y videos para las redes y luego llamo.", "Grabar no ayuda a nadie y retrasa la llamada. Los primeros minutos son los más importantes.")
                    ]),
                Order(
                    "Ordena la información que das al 123, de la más urgente a la menos urgente.",
                    ["Dónde estoy", "Qué pasó", "Cuántos heridos hay y cómo están", "Si hay peligros como fuego o combustible"],
                    "Sin la ubicación la ayuda no puede llegar, por eso va primero. Después qué pasó, el estado de las víctimas y los peligros.")
            ]
        ),
        (
            "Valorar a la víctima",
            "Cómo saber si la persona responde y respira, por qué no se mueve a un herido y qué hacer si no respira.",
            12,
            [
                Text(
                    "Responde y respira",
                    "Acércate de frente para que la persona te vea sin girar la cabeza. Háblale fuerte: «¿Me escucha?». Si no responde, tócale los hombros.\n\n"
                    + "Luego revisa si respira: mira si el pecho se mueve, escucha y siente el aire durante unos 10 segundos.\n\n"
                    + "Los profesionales siguen un orden llamado ABC: A, vía aérea; B, respiración; C, circulación. Para ti, lo esencial es saber si responde y si respira, y contárselo al 123."),
                Text(
                    "No muevas al herido",
                    "Después de un choque puede haber lesiones en la columna, sobre todo en el cuello. Moverlo puede causar daños permanentes.\n\n"
                    + "Muévelo solo si hay un peligro inmediato para su vida, como fuego. Mantén su cabeza alineada con el cuerpo, sin girarla.\n\n"
                    + "Si es un motociclista, no le quites el casco, salvo que no respire y sepas cómo hacerlo entre dos personas.\n\n"
                    + "No le des comida, agua ni medicamentos. Abrígalo, háblale con calma y no lo dejes solo."),
                Text(
                    "Si no respira",
                    "Llama al 123 o pide a alguien que llame, y di que la persona no respira. Si sabes hacerlo, empieza compresiones en el centro del pecho: "
                    + "fuertes y rápidas, entre 100 y 120 por minuto, sin parar hasta que llegue la ayuda o la persona reaccione. El operador del 123 te puede guiar."),
                Order(
                    "Llegas junto a una persona tendida en la vía. Ordena lo que haces.",
                    ["Compruebo que el lugar es seguro", "Le hablo fuerte y le toco los hombros", "Reviso si respira durante unos 10 segundos", "Llamo al 123 y cuento lo que encontré"],
                    "Seguridad primero, luego si responde, si respira, y avisas con esa información."),
                TrueFalse(
                    "Después de una caída de moto, hay que quitarle el casco al motociclista para que respire mejor.",
                    false,
                    "Falso: quitar el casco puede agravar una lesión de cuello. Solo se retira si la persona no respira y quien lo hace sabe cómo, idealmente entre dos personas."),
                Scenario(
                    "Un peatón fue atropellado. Está consciente, se queja de dolor en el cuello y quiere levantarse. ¿Qué haces?",
                    [
                        Choice("Le ayudo a sentarse en el andén para que esté más cómodo.", "Con dolor de cuello puede haber una lesión de columna. Moverlo puede empeorarla."),
                        Choice("Le pido que no se mueva, le sostengo la cabeza alineada, lo abrigo y espero a la ambulancia hablándole con calma.", "Correcto: evitas que la lesión empeore y lo acompañas hasta que llegue la ayuda.", true),
                        Choice("Le doy agua y una pastilla para el dolor.", "No se dan alimentos, bebidas ni medicamentos a un herido: puede necesitar cirugía o vomitar.")
                    ]),
                Quiz(
                    "¿Cuándo está justificado mover a un herido antes de que llegue la ambulancia?",
                    null,
                    ["Siempre, para que no esté en el piso", "Cuando hay un peligro inmediato para su vida, como fuego", "Cuando se queja mucho", "Cuando está bloqueando el tráfico"],
                    1,
                    "Solo se mueve a un herido si quedarse ahí pone su vida en riesgo inmediato. El tráfico se maneja señalizando, no moviendo a la víctima.")
            ]
        ),
        (
            "Heridas y hemorragias",
            "Cómo reconocer un sangrado grave y controlarlo con presión directa.",
            10,
            [
                Text(
                    "Arterias y venas",
                    "Las arterias llevan la sangre con oxígeno desde el corazón hacia el cuerpo; las venas la devuelven al corazón.\n\n"
                    + "Un sangrado arterial suele ser rojo brillante y salir a chorros, al ritmo del pulso: es el más peligroso. "
                    + "Un sangrado venoso suele ser rojo oscuro y salir de forma continua. Cualquier sangrado abundante es una emergencia."),
                Text(
                    "Presión directa",
                    "Protégete: usa guantes si tienes, o una bolsa plástica limpia.\n\n"
                    + "Pon un trapo, gasa o prenda limpia sobre la herida y presiona fuerte con la mano, sin soltar. Si la tela se empapa, no la quites: pon más encima y sigue presionando.\n\n"
                    + "Mantén la presión hasta que llegue la ayuda. Si hay un objeto clavado, no lo saques: presiona alrededor.\n\n"
                    + "El torniquete se reserva para sangrados graves en brazos o piernas que no se controlan con presión, y solo si te entrenaron para ponerlo."),
                Order(
                    "Ordena los pasos para controlar una herida que sangra mucho.",
                    ["Me protejo las manos", "Cubro la herida con una tela limpia", "Presiono fuerte y sin soltar", "Si se empapa, pongo más tela encima y sigo presionando"],
                    "Protección, cobertura, presión firme y continua. Nunca retires la primera tela: arrancarías el coágulo que se está formando."),
                TrueFalse(
                    "Si un objeto quedó clavado en la pierna, lo mejor es sacarlo de inmediato.",
                    false,
                    "Falso: el objeto puede estar tapando el sangrado. Se deja en su lugar y se presiona alrededor hasta que llegue la ayuda."),
                Classify(
                    "¿Qué tipo de sangrado describe cada frase?",
                    ["Arterial", "Venoso"],
                    [
                        ("Rojo brillante", 0),
                        ("Sale a chorros, al ritmo del pulso", 0),
                        ("Rojo oscuro", 1),
                        ("Sale de forma continua, sin chorros", 1)
                    ],
                    "El arterial es rojo brillante y pulsátil; el venoso, más oscuro y continuo. Ambos se controlan con presión directa."),
                Scenario(
                    "Un motociclista tiene una herida en el brazo que sangra mucho. Pusiste tu camiseta encima y ya se empapó. ¿Qué haces?",
                    [
                        Choice("Quito la camiseta para ver cómo va la herida.", "Retirarla arranca el coágulo que se está formando y el sangrado vuelve con más fuerza."),
                        Choice("Pongo otra prenda encima y sigo presionando fuerte.", "Correcto: se añade más tela sin retirar la primera y se mantiene la presión.", true),
                        Choice("Le echo alcohol para desinfectar.", "El alcohol no detiene el sangrado y retrasa lo importante: presionar.")
                    ])
            ]
        ),
        (
            "Quemaduras",
            "Cómo reconocer el grado de una quemadura y qué hacer (y qué no) mientras llega la ayuda.",
            10,
            [
                Text(
                    "Los grados de una quemadura",
                    "Primer grado: la piel se pone roja, seca y duele al tocarla. Es como una quemadura leve de sol; el dolor suele pasar en dos o tres días.\n\n"
                    + "Segundo grado: aparecen ampollas, la piel se ve roja oscura, inflamada, húmeda y brillante, y duele mucho. Las causan el agua muy caliente, las llamas, "
                    + "un objeto caliente, sustancias químicas o la electricidad.\n\n"
                    + "Tercer grado: la piel se ve blanca, café o negra. Puede no doler, porque los nervios están dañados. Siempre requiere atención médica urgente.\n\n"
                    + "En la vía, una quemadura frecuente es la del tubo de escape de la moto en la pierna."),
                Text(
                    "Qué hacer",
                    "Aleja a la persona de la fuente de calor. Enfría la quemadura con agua corriente a temperatura ambiente durante unos 20 minutos.\n\n"
                    + "Quita anillos, relojes o pulseras antes de que la zona se inflame, pero no despegues la ropa que esté pegada a la piel.\n\n"
                    + "Cubre con una tela limpia que no suelte pelusa. Busca atención médica si la quemadura es grande, profunda, o está en la cara, las manos, los genitales o las articulaciones."),
                Tip("No uses hielo, mantequilla, crema dental, aceite ni remedios caseros, y no revientes las ampollas: empeoran la lesión y aumentan el riesgo de infección."),
                Classify(
                    "¿Qué grado de quemadura describe cada caso?",
                    ["Primer grado", "Segundo grado", "Tercer grado"],
                    [
                        ("Piel roja y seca, duele al tocar", 0),
                        ("Quemadura leve de sol", 0),
                        ("Ampollas y piel húmeda y brillante", 1),
                        ("Roja oscura e inflamada, muy dolorosa", 1),
                        ("Piel blanca o negra que casi no duele", 2)
                    ],
                    "Que una quemadura no duela no significa que sea leve: puede ser de tercer grado, con los nervios dañados."),
                FillBlank(
                    "Una quemadura se enfría con agua corriente durante unos [[20|veinte]] minutos. Nunca se aplica [[hielo]] ni se revientan las [[ampollas]].",
                    ["5", "alcohol", "vendas"],
                    "Agua corriente unos 20 minutos; nada de hielo, cremas caseras ni reventar ampollas."),
                Scenario(
                    "Tu acompañante se quemó la pantorrilla con el tubo de escape de la moto. Se le está formando una ampolla. ¿Qué haces?",
                    [
                        Choice("Le pongo crema dental para que refresque.", "La crema dental no enfría bien, irrita y aumenta el riesgo de infección."),
                        Choice("Reviento la ampolla para que salga el líquido.", "La ampolla protege la piel de abajo. Reventarla abre la puerta a una infección."),
                        Choice("Enfrío con agua corriente unos 20 minutos, cubro con una tela limpia y busco atención si es grande.", "Correcto: el agua corriente detiene el daño y la tela limpia protege la herida.", true)
                    ])
            ]
        ),
        (
            "Atragantamiento: maniobra de Heimlich",
            "Cómo ayudar a un adulto, a un niño, a un bebé o a ti mismo cuando algo bloquea la vía aérea.",
            10,
            [
                Text(
                    "Reconocer el atragantamiento",
                    "Una persona atragantada se lleva las manos al cuello, no puede hablar, toser ni respirar bien, y puede ponerse morada.\n\n"
                    + "Si todavía puede toser con fuerza, anímala a seguir tosiendo: es lo más efectivo. Si no puede toser, hablar ni respirar, actúa de inmediato y pide que alguien llame al 123."),
                Text(
                    "Adultos y niños mayores de un año",
                    "Párate detrás de la persona y rodéala con los brazos. Pon un puño, con el pulgar hacia adentro, entre el ombligo y el final del esternón, y cúbrelo con la otra mano.\n\n"
                    + "Haz compresiones rápidas hacia adentro y hacia arriba hasta que expulse el objeto o pierda el conocimiento.\n\n"
                    + "En mujeres embarazadas o personas con mucho abdomen, las compresiones se hacen en el centro del pecho.\n\n"
                    + "Si pierde el conocimiento, acuéstala en el piso, llama al 123 y empieza compresiones en el pecho si sabes hacerlo."),
                Text(
                    "Bebés y uno mismo",
                    "Bebés menores de un año: acuéstalo boca abajo sobre tu antebrazo, con la cabeza más baja que el pecho y sosteniéndole la mandíbula. "
                    + "Da 5 golpes firmes entre los omóplatos con el talón de la mano. Luego voltéalo boca arriba y da 5 compresiones en el centro del pecho con dos dedos. Repite.\n\n"
                    + "Si estás solo y te atragantas: pon tu puño sobre el ombligo y empuja hacia adentro y arriba, o apóyate con fuerza contra el espaldar de una silla."),
                Order(
                    "Ordena los pasos de la maniobra de Heimlich en un adulto que no puede toser.",
                    ["Me paro detrás y lo rodeo con los brazos", "Pongo el puño entre el ombligo y el esternón", "Cubro el puño con la otra mano", "Hago compresiones hacia adentro y arriba hasta que expulse el objeto"],
                    "Posición, ubicación del puño, agarre y compresiones firmes hacia adentro y arriba."),
                TrueFalse(
                    "Si la persona atragantada tose con fuerza, hay que darle golpes en la espalda de inmediato.",
                    false,
                    "Falso: si tose con fuerza, la tos es lo más efectivo. Se le anima a seguir tosiendo y se actúa solo si deja de poder toser, hablar o respirar."),
                Classify(
                    "¿Qué técnica corresponde a cada persona?",
                    ["Compresiones en el abdomen", "Compresiones en el pecho", "Golpes en la espalda y compresiones con dos dedos"],
                    [
                        ("Adulto que no puede toser", 0),
                        ("Niño de 8 años atragantado", 0),
                        ("Mujer embarazada", 1),
                        ("Bebé de 6 meses", 2)
                    ],
                    "Adultos y niños: abdomen. Embarazadas y personas con mucho abdomen: pecho. Bebés: 5 golpes en la espalda y 5 compresiones con dos dedos."),
                Scenario(
                    "Estás almorzando en un restaurante de carretera y un señor de la mesa de al lado se lleva las manos al cuello. No puede hablar ni toser. ¿Qué haces?",
                    [
                        Choice("Le doy agua para que pase el bocado.", "Si la vía aérea está bloqueada, el agua no pasa y puede empeorar la situación."),
                        Choice("Pido que llamen al 123 y le hago la maniobra de Heimlich.", "Correcto: actúas de inmediato y la ayuda profesional ya va en camino.", true),
                        Choice("Espero un momento a ver si se le pasa solo.", "Sin aire, cada segundo cuenta. Si no puede toser ni hablar, hay que actuar ya.")
                    ])
            ]
        )
    ];
}
