namespace Cale.Api.Services.Courses;

/// <summary>"Señalización vial e infraestructura": road markings, traffic lights and devices. Vertical signs live in "Señales de tránsito".</summary>
public sealed partial class CourseSeed
{
    private const string LinesImage = "/courses/lineas-demarcacion.svg";

    private static object ClassifySigns(string instructions, string[] groups, (string Code, string Name, int Group)[] items, string explanation) =>
        new
        {
            type = "classify",
            instructions,
            groups,
            items = items.Select(i => new { text = i.Name, imageUrl = Img(i.Code), group = i.Group }).ToArray(),
            explanation
        };

    private static List<(string Key, string Title, string Summary, int Minutes, object[] Blocks)> SignageLessons() =>
    [
        (
            "senalizacion-infraestructura/lineas",
            "Líneas en el centro y en los bordes",
            "Qué significan los colores blanco y amarillo, y las líneas continuas, discontinuas, dobles y mixtas.",
            12,
            [
                Text(
                    "Lo que está pintado también es una norma",
                    "Las señales verticales se estudian en el curso «Señales de tránsito». Este curso trata lo que está en el piso y alrededor del cruce: "
                    + "las líneas, las marcas transversales, los semáforos y los dispositivos.\n\n"
                    + "Recuerda la jerarquía: primero el agente, luego el semáforo, después las señales verticales y por último las marcas del pavimento. "
                    + "Que tengan la menor prioridad no las vuelve opcionales: si no hay nada por encima que diga otra cosa, la línea manda."),
                Text(
                    "El color dice quién va al lado",
                    "Blanco: separa carriles que van en el mismo sentido y marca el borde derecho de la calzada.\n\n"
                    + "Amarillo: separa flujos que van en sentidos opuestos en una calzada de doble sentido, y marca el borde izquierdo en las calzadas de un solo sentido.\n\n"
                    + "Si ves amarillo a tu izquierda en una vía sin separador, del otro lado vienen carros de frente."),
                Text(
                    "El trazo dice si puedes cruzar",
                    "Discontinua: puedes cruzarla para adelantar o cambiar de carril, si es seguro.\n\n"
                    + "Continua: no se cruza. Prohíbe adelantar y cambiar de carril.\n\n"
                    + "Doble continua: prohibición más fuerte; no se cruza en ninguno de los dos sentidos.\n\n"
                    + "Mixta (una continua y una discontinua): cada conductor obedece la línea que tiene de su lado. Si a tu lado está la discontinua, puedes adelantar con precaución; si está la continua, no."),
                new { type = "image", url = LinesImage, caption = "Cuatro vías vistas desde arriba. Las flechas muestran el sentido de cada carril." },
                Quiz(
                    "En la vía A los dos carriles van en el mismo sentido. ¿Por qué la línea del centro es blanca?",
                    LinesImage,
                    ["Porque es una vía rural", "Porque separa carriles del mismo sentido", "Porque prohíbe cambiar de carril", "Porque es una zona escolar"],
                    1,
                    "El blanco separa carriles que circulan en el mismo sentido. Al ser discontinua, puedes cambiar de carril con precaución."),
                Quiz(
                    "¿En cuáles vías está prohibido cruzar la línea del centro para adelantar, sin importar el carril en que vayas?",
                    LinesImage,
                    ["Solo en A", "En B y C", "Solo en D", "En todas"],
                    1,
                    "B tiene una línea amarilla continua y C una doble continua: ninguna se cruza. En D depende de tu lado, y en A la línea es discontinua."),
                Quiz(
                    "En la vía D, el carro rojo va detrás de un camión lento. ¿Puede adelantarlo?",
                    LinesImage,
                    ["No, nunca se cruza una línea amarilla", "Sí, si es seguro, porque de su lado la línea es discontinua", "Solo si pita antes", "Sí, pero por la derecha"],
                    1,
                    "En una línea mixta cada conductor obedece la línea de su lado. Al carro rojo le corresponde la discontinua, así que puede adelantar si hay visibilidad y el carril contrario está libre."),
                Classify(
                    "¿Qué indica cada línea?",
                    ["Se puede cruzar con precaución", "No se puede cruzar"],
                    [
                        ("Línea blanca discontinua", 0),
                        ("Línea amarilla discontinua", 0),
                        ("Línea mixta, con la discontinua de tu lado", 0),
                        ("Línea amarilla continua", 1),
                        ("Doble línea amarilla continua", 1),
                        ("Línea mixta, con la continua de tu lado", 1),
                        ("Línea blanca continua entre carriles", 1)
                    ],
                    "Discontinua: se cruza si es seguro. Continua o doble continua: no se cruza. En la mixta manda la línea de tu lado."),
                TrueFalse(
                    "Una línea amarilla en el centro de la vía indica que los carriles van en el mismo sentido.",
                    false,
                    "Falso: el amarillo separa sentidos opuestos. Los carriles del mismo sentido se separan con líneas blancas.")
            ]
        ),
        (
            "senalizacion-infraestructura/marcas",
            "Marcas transversales, símbolos y dispositivos",
            "Línea de pare, cebras, flechas, la cuadrícula de no bloquear, tachas y delineadores.",
            10,
            [
                Text(
                    "Líneas que cruzan la vía",
                    "Las líneas transversales se usan sobre todo en los cruces:\n\n"
                    + "Línea de pare: indica dónde debes detenerte, sin pisarla, ante un PARE o un semáforo en rojo.\n\n"
                    + "Cebra o paso peatonal: franjas blancas por donde cruzan los peatones. Nunca te detengas encima.\n\n"
                    + "Cruce de ciclistas: indica por dónde cruzan las bicicletas."),
                Text(
                    "Flechas, letras y símbolos",
                    "Las flechas en el carril te dicen hacia dónde puedes seguir desde ese carril: si solo hay una flecha a la derecha, desde ahí solo puedes girar a la derecha.\n\n"
                    + "Las palabras pintadas (PARE, SOLO BUS, ESCOLAR) refuerzan las señales verticales. "
                    + "La cuadrícula amarilla en una intersección significa que no puedes detenerte dentro de ella: solo entras si tienes espacio para salir."),
                new
                {
                    type = "signs",
                    title = "Señales que acompañan a las marcas del piso",
                    items = new[]
                    {
                        Sign("SR-47", "No bloquear intersección", "Se complementa con la cuadrícula amarilla pintada en el cruce."),
                        Sign("SP-46B", "Ubicación de cruce peatonal", "Marca el sitio exacto de la cebra."),
                        Sign("SP-25A", "Ubicación de resalto", "Indica dónde está el resalto; frena antes de llegar."),
                        Sign("SP-75", "Delineador de curva horizontal", "Muestra la dirección de la curva, sobre todo de noche.")
                    }
                },
                Text(
                    "Dispositivos que guían",
                    "Las tachas son pequeños reflectivos pegados al pavimento que refuerzan las líneas, sobre todo de noche y con lluvia. "
                    + "Las amarillas canalizan el tránsito o previenen, y las rojas indican una zona prohibida: si ves rojo de frente, vas hacia donde no debes.\n\n"
                    + "Los delineadores marcan el borde de la vía y la forma de las curvas. Los resaltos y reductores obligan a bajar la velocidad en zonas sensibles."),
                Scenario(
                    "El semáforo está en rojo. Delante hay una línea de pare y, después, la cebra. ¿Dónde te detienes?",
                    [
                        Choice("Antes de la línea de pare, sin pisarla.", "Correcto: así dejas la cebra libre para los peatones y no invades el cruce.", true),
                        Choice("Encima de la cebra, para arrancar más rápido.", "Detenerte en la cebra obliga a los peatones a rodear tu carro por la calzada, donde pueden ser atropellados."),
                        Choice("Donde quede, porque igual voy a esperar.", "La línea de pare existe para ordenar el cruce: detenerte más adelante tapa la cebra y la visibilidad de otros.")
                    ]),
                Scenario(
                    "Hay trancón y el cruce que tienes adelante tiene la cuadrícula amarilla pintada. El semáforo está en verde, pero del otro lado no hay espacio para tu carro. ¿Qué haces?",
                    [
                        Choice("Entro igual, porque tengo verde.", "Si te quedas dentro del cruce cuando cambie el semáforo, bloqueas a quienes cruzan en el otro sentido."),
                        Choice("Espero antes de la cuadrícula hasta que haya espacio para salir del otro lado.", "Correcto: la cuadrícula indica que no puedes detenerte dentro del cruce.", true),
                        Choice("Me meto hasta la mitad y espero ahí.", "Quedarte en la mitad del cruce es justo lo que prohíbe la cuadrícula amarilla.")
                    ]),
                TrueFalse(
                    "Si en mi carril solo hay pintada una flecha hacia la derecha, desde ese carril puedo seguir derecho.",
                    false,
                    "Falso: la flecha indica las maniobras permitidas desde ese carril. Con una sola flecha a la derecha, desde ahí solo se gira a la derecha.")
            ]
        ),
        (
            "senalizacion-infraestructura/semaforos",
            "Fases del semáforo vehicular y peatonal",
            "Qué obliga cada luz del semáforo para vehículos y para peatones, incluidas las intermitentes y las flechas.",
            12,
            [
                Text(
                    "Cada luz es una orden distinta",
                    "El semáforo no es solo «rojo y verde». Cada fase dice una cosa diferente, y quien ya está dentro del cruce no se queda atrapado: lo termina. "
                    + "Si un agente de tránsito indica otra cosa, manda el agente."),
                Flip(
                    "Semáforo vehicular",
                    Card("Rojo", "Detente antes de la línea de pare. No entres al cruce."),
                    Card("Amarillo fijo", "Prepárate para detenerte. Si ya no alcanzas a parar con seguridad, termina de cruzar. No aceleres para ganarle."),
                    Card("Verde", "Puedes avanzar si el cruce está despejado. Cede a quien todavía termina de cruzar, sobre todo al peatón."),
                    Card("Flecha verde", "Puedes hacer el movimiento de la flecha. Mira igual a peatones y a quien sigue derecho."),
                    Card("Amarillo intermitente", "Hay un riesgo: reduce y pasa solo cuando sea seguro."),
                    Card("Rojo intermitente", "Trátalo como un pare: detente y continúa cuando no venga nadie.")),
                Flip(
                    "Semáforo peatonal",
                    Card("Silueta en verde", "Puedes empezar a cruzar por la cebra."),
                    Card("Silueta intermitente", "No empieces a cruzar. Si ya vas en la cebra, termina con calma."),
                    Card("Mano roja", "No cruces. Espera la siguiente fase en el andén.")),
                Order(
                    "El semáforo está en verde para ti y cambia a amarillo cuando todavía no has entrado al cruce. Ordena lo correcto.",
                    [
                        "Dejo de acelerar",
                        "Miro si alcanzo a detenerme antes de la línea",
                        "Si alcanzo, me detengo",
                        "Si ya no alcanzo con seguridad, termino de cruzar sin acelerar",
                        "No me quedo detenido dentro del cruce"
                    ],
                    "El amarillo no es una invitación a acelerar. Es el aviso de que el rojo está a punto de llegar."),
                Scenario(
                    "Tienes verde, pero un peatón que empezó con su fase todavía va a la mitad de la cebra. Detrás tuyo pitan. ¿Qué haces?",
                    [
                        Choice("Avanzo porque mi luz ya está en verde y el de atrás tiene prisa.", "El verde te autoriza a seguir cuando el cruce está libre. El peatón que ya cruza tiene prioridad."),
                        Choice("Espero a que el peatón termine y después avanzo.", "Correcto: tu fase no borra a quien ya está en la cebra.", true),
                        Choice("Le pito para que se devuelva al andén.", "Devolver a un peatón a la mitad de la vía lo pone delante de los carros del otro sentido.")
                    ]),
                TrueFalse(
                    "Un semáforo en rojo intermitente significa que puedo pasar sin detenerme, solo reduciendo la velocidad.",
                    false,
                    "Falso: el rojo intermitente obliga a detenerse, como un pare, y a seguir solo cuando la vía esté libre. El que permite pasar con precaución es el amarillo intermitente."),
                Quiz(
                    "La silueta del semáforo peatonal empieza a parpadear y tú todavía estás en el andén. ¿Qué haces?",
                    null,
                    ["Cruzo corriendo", "No empiezo a cruzar y espero la siguiente fase", "Cruzo porque los carros tienen que esperar", "Me paro en la mitad de la cebra"],
                    1,
                    "El parpadeo es para quien ya va cruzando. Quien está en el andén espera.")
            ]
        ),
        (
            "senalizacion-infraestructura/repaso",
            "Repaso final",
            "Pon a prueba lo que aprendiste sobre líneas, marcas, semáforos y dispositivos.",
            8,
            [
                Text(
                    "Antes de empezar",
                    "En el examen teórico la señalización aparece en muchas preguntas. Recuerda tres ideas: el color de la línea te dice quién va al lado, "
                    + "el trazo te dice si puedes cruzarla y cada luz del semáforo es una orden distinta."),
                Classify(
                    "¿Qué te obliga a hacer cada indicación?",
                    ["Detenerme", "Pasar solo con precaución"],
                    [
                        ("Semáforo en rojo fijo", 0),
                        ("Semáforo en rojo intermitente", 0),
                        ("Mano roja del semáforo peatonal, si voy a pie", 0),
                        ("Línea de pare con el semáforo en rojo", 0),
                        ("Semáforo en amarillo intermitente", 1),
                        ("Flecha verde hacia mi giro", 1),
                        ("Línea blanca discontinua para cambiar de carril", 1)
                    ],
                    "El rojo, fijo o intermitente, obliga a detenerse. El amarillo intermitente, la flecha verde y la línea discontinua permiten seguir, pero solo si es seguro."),
                FillBlank(
                    "Las líneas [[blancas|blanca]] separan carriles del mismo sentido y las [[amarillas|amarilla]] separan sentidos opuestos. Una línea [[continua]] no se puede cruzar.",
                    ["rojas", "discontinua", "verdes"],
                    "Blanco: mismo sentido. Amarillo: sentidos opuestos. Continua: no se cruza."),
                Quiz(
                    "¿Qué indica una tacha roja que ves de frente en la vía?",
                    null,
                    ["Que hay un hospital cerca", "Que vas hacia una zona prohibida o en contravía", "Que la vía es de doble sentido", "Que puedes adelantar"],
                    1,
                    "Las tachas rojas marcan zonas prohibidas. Si las ves de frente, revisa de inmediato tu posición en la vía."),
                Quiz(
                    "¿Qué te indica esta señal?",
                    Img("SR-48"),
                    ["Prohibido adelantar", "Fin de la prohibición de adelantar", "Prioridad al sentido contrario", "Fin de la vía"],
                    1,
                    "Marca el final del tramo donde estaba prohibido adelantar. Aun así, solo adelantas si la línea lo permite y es seguro."),
                TrueFalse(
                    "Las palabras pintadas en el pavimento, como PARE, tienen más prioridad que un semáforo.",
                    false,
                    "Falso: las marcas en el pavimento tienen la menor prioridad; el semáforo está por encima de ellas.")
            ]
        )
    ];
}
