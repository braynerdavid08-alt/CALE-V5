namespace Cale.Api.Services.Courses;

/// <summary>
/// "Dominio seguro del automóvil": the B1-specific competencies of the school's curriculum (B1-ES01).
/// </summary>
public sealed partial class CourseSeed
{
    private static List<(string Key, string Title, string Summary, int Minutes, object[] Blocks)> CarLessons() =>
    [
        (
            "automovil-b1/puesto-mandos",
            "Puesto de conducción y mandos",
            "Cómo ajustar el asiento, el volante y los espejos, y para qué sirve cada mando antes de encender el carro.",
            14,
            [
                Text(
                    "Todo empieza antes de encender",
                    "Un conductor mal sentado se cansa antes, ve menos y reacciona tarde. Por eso el primer paso de cualquier recorrido es ajustar el puesto de conducción, "
                    + "siempre con el carro detenido y en este orden: asiento, volante, espejos y cinturón. Si compartes el carro con otra persona, revísalo cada vez que te subas."),
                Flip(
                    "Ajustes del puesto de conducción",
                    Card("Distancia del asiento", "Con la espalda apoyada, debes poder pisar el pedal del freno (o del embrague) hasta el fondo sin estirar del todo la pierna: la rodilla queda un poco doblada."),
                    Card("Respaldo", "Casi vertical. Con los hombros apoyados, las muñecas deben llegar a la parte alta del volante sin despegar la espalda."),
                    Card("Altura", "Tus ojos deben quedar por lo menos a la mitad del parabrisas para ver el tablero y la vía sin agacharte."),
                    Card("Volante", "Manos en posición de las 9 y las 3 de un reloj, con los pulgares por fuera de los radios. Así no te golpeas si se activa el airbag."),
                    Card("Espejos", "El retrovisor central muestra todo el vidrio trasero. Los laterales se ajustan para ver apenas el costado de tu carro y el máximo de carril vecino."),
                    Card("Cinturón", "La banda baja va sobre la cadera, no sobre el abdomen; la banda diagonal cruza el hombro y el pecho, nunca el cuello.")),
                Order(
                    "Ordena los ajustes del puesto de conducción.",
                    [
                        "Ajusto la distancia y la altura del asiento",
                        "Ajusto el respaldo",
                        "Ajusto la altura y la profundidad del volante",
                        "Ajusto los tres espejos",
                        "Me abrocho el cinturón y verifico que todos lo lleven"
                    ],
                    "Primero el asiento, porque los demás ajustes dependen de dónde quedan tus ojos y tus manos."),
                Text(
                    "Los mandos del automóvil",
                    "Pedales (de derecha a izquierda): acelerador, freno y, en los carros mecánicos, embrague. El acelerador y el freno se manejan solo con el pie derecho; "
                    + "el izquierdo es para el embrague o, en los automáticos, descansa en el reposapiés.\n\n"
                    + "Palanca de cambios, freno de mano, palanca de luces y direccionales, limpiaparabrisas, pito y luces de parqueo (estacionarias). "
                    + "Antes de tu primera clase, aprende a ubicarlos sin mirar: en movimiento, tus ojos deben estar en la vía."),
                Pairs(
                    "Une cada mando con su función.",
                    ("Embrague", "Desconecta el motor de las ruedas para poder cambiar de marcha"),
                    ("Freno de mano", "Mantiene el carro inmóvil cuando está estacionado o detenido en pendiente"),
                    ("Luces de parqueo", "Avisan que el vehículo está detenido o que hay un peligro"),
                    ("Direccionales", "Anuncian con anticipación un giro o un cambio de carril"),
                    ("Desempañador", "Quita el vapor de los vidrios para no perder visibilidad")),
                Scenario(
                    "Te subes al carro de la escuela después de otro alumno mucho más alto que tú. ¿Qué haces primero?",
                    [
                        Choice("Enciendo y arranco; los ajustes los hago en el primer semáforo.", "Ajustar el asiento o los espejos en movimiento o en un semáforo te distrae y te deja sin visibilidad en el peor momento."),
                        Choice("Ajusto asiento, volante y espejos con el carro detenido, y luego me pongo el cinturón.", "Correcto: el puesto de conducción se ajusta siempre antes de arrancar.", true),
                        Choice("Solo muevo el retrovisor central, que es el que más uso.", "Con el asiento mal ubicado no alcanzas bien los pedales y los espejos laterales quedan mal ajustados.")
                    ]),
                TrueFalse(
                    "Lo más seguro es tomar el volante con las manos en la parte alta, en la posición de las 12.",
                    false,
                    "Falso: con las manos arriba tienes menos control y, si se activa el airbag, te puede golpear los brazos contra la cara. Usa la posición de las 9 y las 3."),
                Quiz(
                    "¿Con qué pie se manejan el acelerador y el freno?",
                    null,
                    ["El acelerador con el derecho y el freno con el izquierdo", "Ambos con el pie derecho", "Ambos con el pie izquierdo", "Depende de la comodidad del conductor"],
                    1,
                    "Usar el mismo pie evita que pises acelerador y freno al tiempo. En un frenado de emergencia, el pie pasa directo del acelerador al freno.")
            ]
        ),
        (
            "automovil-b1/embrague-cambios",
            "Embrague, cambios, arranque y detención",
            "Cómo arrancar sin que se apague el carro, cuándo cambiar de marcha, cómo detenerte con suavidad y qué cambia en un carro automático.",
            16,
            [
                Text(
                    "El punto de fricción",
                    "El embrague une y separa el motor de las ruedas. Cuando lo pisas a fondo, el motor queda libre; cuando lo sueltas, transmite su fuerza. "
                    + "Entre las dos posiciones hay un punto, llamado punto de fricción o de contacto, en el que el carro empieza a moverse.\n\n"
                    + "Arrancar bien es encontrar ese punto y soltar el embrague despacio mientras aceleras un poco. Si lo sueltas de golpe, el carro salta y se apaga; "
                    + "si aceleras mucho, las llantas patinan y gastas el embrague."),
                Order(
                    "Ordena los pasos para arrancar un carro mecánico en plano.",
                    [
                        "Piso el embrague a fondo y pongo la primera",
                        "Miro los espejos y el punto ciego",
                        "Pongo la direccional si voy a salir de la orilla",
                        "Suelto despacio el embrague hasta el punto de fricción",
                        "Quito el freno de mano y acelero suavemente mientras termino de soltar el embrague"
                    ],
                    "Antes de mover el carro miras: arrancar sin revisar el espejo y el punto ciego es una de las causas más comunes de choques al salir de un parqueo."),
                Text(
                    "Cuándo cambiar de marcha",
                    "Cada marcha sirve para un rango de velocidad. Como referencia, en un carro a gasolina se sube de marcha cerca de las 2.500 revoluciones y se baja cuando el motor empieza a forzarse. "
                    + "El tacómetro y el sonido del motor te guían.\n\n"
                    + "Para cambiar: pisa el embrague a fondo, mueve la palanca sin forzarla, suelta el embrague con suavidad. No apoyes el pie en el embrague mientras conduces: lo desgastas sin darte cuenta. "
                    + "Y nunca bajes una pendiente en neutro: pierdes el freno de motor y el control del carro."),
                Classify(
                    "¿Es un buen hábito con el embrague y los cambios, o un mal hábito?",
                    ["Buen hábito", "Mal hábito"],
                    [
                        ("Pisar el embrague a fondo para cambiar", 0),
                        ("Subir de marcha según las revoluciones y el sonido del motor", 0),
                        ("Usar una marcha baja para bajar una pendiente larga", 0),
                        ("Llevar el pie apoyado en el embrague", 1),
                        ("Bajar una pendiente en neutro para ahorrar gasolina", 1),
                        ("Sostener el carro en una subida con el embrague en lugar del freno", 1),
                        ("Mirar la palanca cada vez que cambias", 1)
                    ],
                    "Sostener el carro con el embrague en una subida lo quema en poco tiempo. Para eso está el freno."),
                Text(
                    "Detenerse con suavidad",
                    "Detenerse bien es anticipar: suelta el acelerador con tiempo, frena de forma progresiva y pisa el embrague justo antes de que el motor empiece a vibrar. "
                    + "Ya detenido, pon neutro y, si la parada es larga o en pendiente, el freno de mano.\n\n"
                    + "Si frenas tarde, frenas fuerte, y quien viene detrás puede no alcanzar a detenerse."),
                Flip(
                    "El carro automático",
                    Card("P (parqueo)", "Bloquea la transmisión. Solo se usa con el carro totalmente detenido, y siempre junto con el freno de mano."),
                    Card("R (reversa)", "Para retroceder. Se pone con el carro detenido y el pie en el freno."),
                    Card("N (neutro)", "Desconecta el motor de las ruedas. No se usa para bajar pendientes."),
                    Card("D (avance)", "La caja cambia sola. Al soltar el freno, el carro avanza despacio aunque no aceleres."),
                    Card("El pie izquierdo", "No se usa. Frenar con el izquierdo y acelerar con el derecho hace que pises los dos pedales a la vez en una emergencia.")),
                FillBlank(
                    "Para arrancar en un carro mecánico, se suelta el embrague despacio hasta el punto de [[fricción|contacto]]. En un carro automático, la posición [[P]] solo se usa con el carro totalmente detenido.",
                    ["neutro", "D", "aceleración"],
                    "El punto de fricción es donde el embrague empieza a transmitir la fuerza del motor."),
                Quiz(
                    "¿Por qué no se debe bajar una pendiente con el carro en neutro?",
                    null,
                    ["Porque se gasta más gasolina", "Porque se pierde el freno de motor y los frenos se recalientan", "Porque está prohibido solo de noche", "Porque se daña el radio"],
                    1,
                    "En neutro, todo el peso del carro lo tienen que detener los frenos. En una bajada larga se recalientan y pierden eficacia.")
            ]
        ),
        (
            "automovil-b1/frenado-pendientes",
            "Frenado y pendientes",
            "Frenado progresivo, frenado de emergencia con y sin ABS, arranque en subida y cómo dejar el carro estacionado en una pendiente.",
            15,
            [
                Text(
                    "El frenado progresivo",
                    "En la conducción normal se frena en tres tiempos: presión suave al inicio para que el peso pase hacia adelante, presión firme para reducir la velocidad "
                    + "y presión suave al final para que el carro no cabecee al detenerse. Así los pasajeros no se van hacia adelante y el de atrás tiene tiempo de reaccionar.\n\n"
                    + "Usa el freno de motor (bajar de marcha) para ayudar al freno de servicio en las bajadas largas."),
                Text(
                    "El frenado de emergencia",
                    "Con ABS: pisa el freno a fondo y mantenlo, aunque sientas que el pedal vibra o hace ruido. El sistema evita que las ruedas se bloqueen y te deja girar el volante para esquivar.\n\n"
                    + "Sin ABS: si pisas a fondo, las ruedas se bloquean y el carro sigue derecho aunque gires. Frena fuerte hasta justo antes de que se bloqueen; si se bloquean, afloja un poco y vuelve a frenar.\n\n"
                    + "Averigua si el carro en el que practicas tiene ABS: el testigo se enciende unos segundos al dar contacto."),
                Scenario(
                    "Vas a 50 km/h y un niño sale corriendo detrás de un carro estacionado. Tu carro tiene ABS. ¿Qué haces?",
                    [
                        Choice("Piso el freno a fondo y lo mantengo, y giro el volante para esquivar si hay espacio libre.", "Correcto: con ABS puedes frenar al máximo y dirigir el carro al mismo tiempo.", true),
                        Choice("Bombeo el freno varias veces.", "Con ABS no se bombea: el sistema ya lo hace mucho más rápido que tú. Al bombear, frenas menos."),
                        Choice("Pito y giro bruscamente sin frenar.", "Sin frenar, el impacto es a 50 km/h si no logras esquivar, y un giro brusco puede hacerte perder el control.")
                    ]),
                Text(
                    "Arrancar en subida",
                    "En una pendiente, el carro tiende a irse hacia atrás en el momento en que sueltas el freno. El freno de mano te da tiempo para coordinar los pedales: "
                    + "con el freno de mano puesto, encuentra el punto de fricción, acelera un poco más que en plano y suelta el freno de mano cuando sientas que el carro quiere avanzar. "
                    + "Muchos carros modernos tienen asistente de arranque en pendiente, que sostiene el freno unos segundos; aun así, aprende a hacerlo sin ayuda."),
                Order(
                    "Ordena los pasos para arrancar en una subida con un carro mecánico.",
                    [
                        "Con el freno de mano puesto, piso el embrague y pongo la primera",
                        "Suelto el embrague hasta el punto de fricción",
                        "Acelero un poco más que en plano",
                        "Suelto el freno de mano cuando siento que el carro quiere avanzar",
                        "Termino de soltar el embrague mientras acelero"
                    ],
                    "El freno de mano evita que el carro se devuelva mientras encuentras el punto de fricción."),
                Text(
                    "Estacionar en pendiente",
                    "Además del freno de mano, deja una marcha puesta: primera si el carro quedó mirando hacia arriba, reversa si quedó mirando hacia abajo (en automáticos, P).\n\n"
                    + "Gira las ruedas para que, si el carro se suelta, vaya contra el andén y no hacia la vía: en bajada, las ruedas hacia el andén; "
                    + "en subida con andén, las ruedas hacia la vía para que la parte de atrás de la llanta se apoye en el andén; sin andén, siempre hacia la orilla."),
                Classify(
                    "¿Hacia dónde deben quedar las ruedas delanteras?",
                    ["Hacia el andén o la orilla", "Hacia la vía"],
                    [
                        ("Estacionado en bajada, junto a un andén", 0),
                        ("Estacionado en bajada, sin andén", 0),
                        ("Estacionado en subida, sin andén", 0),
                        ("Estacionado en subida, junto a un andén", 1)
                    ],
                    "En subida con andén, las ruedas apuntan hacia la vía para que la llanta, si el carro se devuelve, quede trabada contra el andén."),
                TrueFalse(
                    "Si el pedal del freno vibra durante un frenado fuerte, significa que el ABS está fallando y debes soltarlo.",
                    false,
                    "Falso: la vibración es normal y muestra que el ABS está trabajando. Mantén el pedal pisado a fondo."),
                Quiz(
                    "¿Qué marcha dejas puesta al estacionar un carro mecánico mirando hacia abajo en una pendiente?",
                    null,
                    ["Neutro", "Reversa", "Tercera", "Ninguna, basta el freno de mano"],
                    1,
                    "La reversa se opone al movimiento hacia adelante. Mirando hacia arriba se deja la primera, y en los dos casos con el freno de mano puesto.")
            ]
        ),
        (
            "automovil-b1/reversa-estacionamiento",
            "Reversa y estacionamiento",
            "Cómo retroceder con control, y la técnica paso a paso del estacionamiento en paralelo, en batería y en reversa.",
            16,
            [
                Text(
                    "La reversa: despacio y mirando",
                    "Retroceder es una de las maniobras con menos visibilidad: detrás del carro hay una zona que no se ve desde el puesto de conducción, donde cabe un niño o un ciclista. "
                    + "Antes de poner la reversa, mira alrededor del carro. Mientras retrocedes, gira el cuerpo para mirar por el vidrio trasero, apóyate en los espejos y avanza a la velocidad de una persona caminando, "
                    + "controlando la velocidad con el embrague y el freno.\n\n"
                    + "Usa la reversa solo en distancias cortas y nunca en una intersección ni para recuperar una salida que te pasaste en una vía rápida. Las cámaras y sensores ayudan, pero no reemplazan tu mirada."),
                Scenario(
                    "Sales en reversa de un garaje y la acera tiene mucho movimiento de peatones. La cámara de reversa no muestra a nadie. ¿Qué haces?",
                    [
                        Choice("Salgo rápido para no bloquear la acera mucho tiempo.", "Salir rápido te deja sin tiempo de reacción si aparece alguien que la cámara no alcanzaba a mostrar."),
                        Choice("Pito y salgo confiando en la cámara.", "La cámara tiene un ángulo limitado y el pito no garantiza que te oigan o te vean."),
                        Choice("Salgo muy despacio, mirando por los espejos y el vidrio, y me detengo si alguien se acerca.", "Correcto: el peatón en la acera tiene prioridad. Avanza poco a poco hasta tener visibilidad completa.", true)
                    ]),
                Text(
                    "Estacionamiento en paralelo",
                    "Busca un espacio de al menos una vez y media el largo de tu carro. Usa siempre la direccional y revisa los espejos antes de detenerte, "
                    + "porque el carro que viene detrás necesita saber que vas a maniobrar."),
                Order(
                    "Ordena los pasos para estacionar en paralelo.",
                    [
                        "Pongo la direccional y me detengo al lado del carro de adelante, a unos 50 cm de él",
                        "Pongo la reversa y retrocedo despacio hasta que mi parachoques trasero quede a la altura del suyo",
                        "Giro todo el volante hacia el andén y retrocedo hasta que el carro quede a unos 45 grados",
                        "Enderezo el volante y retrocedo hasta que mi parachoques delantero pase el del otro carro",
                        "Giro todo el volante hacia la vía y retrocedo hasta quedar paralelo al andén",
                        "Enderezo el carro y lo centro en el espacio"
                    ],
                    "La clave está en el ángulo de 45 grados: si giras muy pronto, la llanta golpea el andén; si giras tarde, quedas lejos de él."),
                Text(
                    "En batería y en reversa",
                    "En batería (perpendicular al andén o en un parqueadero), entrar en reversa es más seguro: la maniobra difícil la haces cuando entras, con el espacio libre a la vista, "
                    + "y al salir tienes visibilidad completa de la vía y de los peatones.\n\n"
                    + "Para entrar en reversa: pasa un poco más allá del espacio, gira el volante hacia el lado del espacio mientras retrocedes despacio, "
                    + "vigila las líneas en los espejos y endereza cuando el carro quede paralelo a ellas."),
                Classify(
                    "¿Esta práctica de estacionamiento es segura o insegura?",
                    ["Segura", "Insegura"],
                    [
                        ("Entrar en reversa a un parqueadero en batería", 0),
                        ("Poner la direccional antes de detenerte para estacionar", 0),
                        ("Mirar el espejo lateral antes de abrir la puerta", 0),
                        ("Estacionar en paralelo en una curva", 1),
                        ("Retroceder rápido para aprovechar un espacio antes de que otro lo tome", 1),
                        ("Abrir la puerta del lado de la vía sin mirar", 1)
                    ],
                    "Antes de abrir la puerta, mira el espejo: una puerta abierta sin mirar es una de las caídas más graves para motociclistas y ciclistas."),
                TrueFalse(
                    "Si el carro tiene sensores de reversa, ya no es necesario mirar por los espejos ni por el vidrio trasero.",
                    false,
                    "Falso: los sensores no detectan todos los obstáculos (por ejemplo, objetos bajos o delgados) y no ven a quien se acerca rápido. Son una ayuda, no un reemplazo."),
                Quiz(
                    "¿Por qué es más seguro estacionar en reversa en un parqueadero en batería?",
                    null,
                    ["Porque el carro gasta menos gasolina", "Porque al salir tienes visibilidad completa de la vía y de los peatones", "Porque es obligatorio en todos los parqueaderos", "Porque se ocupa menos espacio"],
                    1,
                    "Salir de frente es mucho más seguro que salir en reversa hacia una vía con tráfico y peatones que no ves bien.")
            ]
        ),
        (
            "automovil-b1/giros",
            "Giros e intersecciones con el automóvil",
            "Cómo preparar y ejecutar un giro a la derecha, a la izquierda y en U, y cómo moverte dentro de una glorieta con el carro.",
            14,
            [
                Text(
                    "Un giro se prepara antes de la esquina",
                    "Todo giro tiene tres momentos: preparación (miras, pones la direccional, te ubicas en el carril correcto y reduces la velocidad), "
                    + "ejecución (giras a velocidad baja y constante, mirando hacia donde vas) y salida (enderezas y te ubicas en el carril que corresponde).\n\n"
                    + "La regla más común en el examen práctico: la direccional se pone antes de frenar, no cuando ya estás girando."),
                Flip(
                    "Los tres giros",
                    Card("A la derecha", "Desde el carril derecho, cerca del andén. Antes de girar, revisa el espejo derecho: por ahí pueden venir una moto o una bicicleta. Cede el paso a los peatones que cruzan la calle a la que entras."),
                    Card("A la izquierda", "Desde el carril izquierdo (o el más cercano al centro en una vía de doble sentido). Cede el paso a los vehículos que vienen de frente y a los peatones. Espera con las ruedas derechas, no giradas: si te chocan por detrás, no te lanzan al carril contrario."),
                    Card("En U", "Solo donde no esté prohibido por una señal y haya visibilidad suficiente en ambos sentidos. Nunca en curvas, puentes, túneles o cerca de la cima de una pendiente.")),
                Order(
                    "Ordena los pasos para girar a la izquierda en una intersección con semáforo.",
                    [
                        "Miro los espejos y pongo la direccional izquierda",
                        "Me ubico en el carril izquierdo y reduzco la velocidad",
                        "Avanzo hasta la línea de pare con las ruedas derechas",
                        "Espero a que no vengan vehículos de frente ni crucen peatones",
                        "Giro a velocidad baja y constante, mirando hacia la vía a la que entro",
                        "Me ubico en el carril correspondiente y apago la direccional"
                    ],
                    "Esperar con las ruedas derechas evita que, si te golpean por detrás, el carro salga empujado contra el tráfico que viene de frente."),
                Text(
                    "Moverte dentro de una glorieta",
                    "Las reglas de prelación de la glorieta ya las viste en el curso de Normas. Con el carro, la técnica es esta: "
                    + "elige el carril según la salida que vas a tomar (carril derecho para las primeras salidas, carril interno para las últimas), "
                    + "circula a velocidad constante y, antes de salir, pasa al carril externo con la direccional derecha puesta.\n\n"
                    + "Si te pasaste de la salida, da otra vuelta: nunca cruces de golpe ni frenes dentro de la glorieta."),
                Scenario(
                    "Vas a girar a la derecha en una calle de Barranquilla. Por el espejo derecho ves que viene una moto muy cerca del andén. ¿Qué haces?",
                    [
                        Choice("Giro rápido para entrar antes que ella.", "Es el choque más típico al girar a la derecha: la moto no alcanza a frenar y se estrella contra el costado de tu carro."),
                        Choice("Mantengo la direccional, espero a que la moto pase y luego giro.", "Correcto: la moto sigue derecho y tiene la vía. Tu giro puede esperar unos segundos.", true),
                        Choice("Pito para que la moto se detenga.", "El pito no te da prelación. La moto puede no oírte o no tener espacio para detenerse.")
                    ]),
                Pairs(
                    "Une cada situación con la acción correcta.",
                    ("Vas a girar a la derecha", "Revisa el espejo derecho por si vienen motos o bicicletas"),
                    ("Esperas para girar a la izquierda", "Mantén las ruedas derechas hasta que puedas girar"),
                    ("Te pasaste de la salida de una glorieta", "Da otra vuelta completa"),
                    ("Hay peatones cruzando la calle a la que entras", "Detente y cédeles el paso")),
                TrueFalse(
                    "Al esperar para girar a la izquierda, es mejor tener las ruedas ya giradas para salir más rápido.",
                    false,
                    "Falso: si te chocan por detrás con las ruedas giradas, el carro sale lanzado hacia el carril contrario."),
                Quiz(
                    "¿Cuándo se debe poner la direccional para girar?",
                    null,
                    ["Cuando ya estás girando", "Antes de frenar y de cambiar de carril", "Solo si hay otros carros cerca", "Solo en las avenidas"],
                    1,
                    "La direccional sirve para avisar con tiempo. Si la pones mientras giras, ya no le da tiempo a nadie de reaccionar.")
            ]
        )
    ];
}
