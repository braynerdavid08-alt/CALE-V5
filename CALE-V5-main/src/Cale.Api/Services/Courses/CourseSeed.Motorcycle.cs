namespace Cale.Api.Services.Courses;

/// <summary>
/// "Conducción segura en motocicleta": the A2-specific competencies of the school's curriculum (A2-ES01),
/// based on the ANSV's public course for motorcyclists.
/// </summary>
public sealed partial class CourseSeed
{
    private static List<(string Key, string Title, string Summary, int Minutes, object[] Blocks)> MotorcycleLessons() =>
    [
        (
            "motocicleta-a2/proteccion-alistamiento",
            "Antes de salir: elementos de protección y alistamiento",
            "El casco, la ropa de protección, los documentos, el kit de herramientas y la preparación del cuerpo antes de subirte a la moto.",
            20,
            [
                Text(
                    "Tu carrocería es tu equipo",
                    "En un carro, la carrocería y el cinturón te protegen. En una moto, lo único entre tu cuerpo y el pavimento es lo que llevas puesto. "
                    + "Por eso los elementos de protección personal (EPP) no son un accesorio: son la diferencia entre levantarse de una caída o no.\n\n"
                    + "La ANSV recomienda cumplir la tríada salvavidas para todos los EPP: buena calidad, talla correcta y uso apropiado (ajustado y abrochado)."),
                Video("ansv-moto-alistamiento.mp4", "Cómo prepararse y alistar la moto antes de un recorrido. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Flip(
                    "Los elementos de protección",
                    Card("Casco", "Obligatorio para conductor y acompañante. Debe estar certificado según el reglamento técnico vigente (Resolución 20233040005155 de 2023, que acepta la norma ONU R22.06; también se ven sellos DOT o NTC 4533) y ser de tu talla. La correa va abrochada, sin pasar por la mandíbula, dejando apenas un centímetro con el cuello: si te lo puedes quitar sin soltarla, está floja. El integral es el que más protege."),
                    Card("Prenda reflectiva", "Conductor y acompañante deben llevarla entre las 18:00 y las 6:00 y siempre que haya poca visibilidad. La ANSV recomienda usarla siempre: también te hace más visible en los puntos ciegos de carros y buses."),
                    Card("Guantes", "En una caída, las manos son lo primero que toca el piso. Deben tener protección en palmas y nudillos; busca la certificación EN 13594. Según estudios citados por la ANSV, previenen cerca de 1 de cada 5 lesiones en las manos."),
                    Card("Chaqueta y pantalón con protecciones", "Protegen hombros, codos, espalda, caderas y rodillas. Las protecciones con certificación EN 1621 reducen casi a la mitad las lesiones en el torso."),
                    Card("Botas", "Que cubran el tobillo y tengan suela antideslizante. Las chanclas y los tenis no protegen."),
                    Card("Impermeable de dos piezas", "Los impermeables tipo poncho o ruana se pueden enredar en la cadena o en las llantas.")),
                Text(
                    "El casco, bien usado",
                    "Un casco certificado reduce cerca de un 69 % el riesgo de una lesión grave en la cabeza y cerca de un 42 % el riesgo de morir en un choque. "
                    + "Aun así, alrededor de un tercio de los motociclistas que mueren en Colombia fallecen por trauma en la cabeza (ANSV, 2024).\n\n"
                    + "La norma (Resolución 20203040023385 de 2020) fija tres condiciones de uso:\n\n"
                    + "1. La cabeza va totalmente dentro del casco y la correa abrochada debajo de la mandíbula, sin correas rotas ni broches partidos.\n"
                    + "2. Nada entre la cabeza y el casco: el celular solo con manos libres.\n"
                    + "3. Si el casco es abatible, la mentonera va cerrada y asegurada mientras circulas.\n\n"
                    + "Desde la Ley 2251 de 2022 no te pueden exigir que el casco tenga pintada la placa de la moto."),
                Text(
                    "Lo que muestran los datos en Barranquilla",
                    "En un estudio de la ANSV con motociclistas de Barranquilla y su área metropolitana, el 39 % dijo haber tenido algún siniestro, y para el 78 % la moto es su herramienta de trabajo. "
                    + "Los propios motociclistas señalaron como tramos de riesgo la Circunvalar, La Cordialidad, la Murillo y la Vía 40: contravía, cruces sobre los separadores, huecos y arroyos cuando llueve fuerte. "
                    + "En la ciudad es común ver a acompañantes con el casco en la mano o mal puesto.\n\n"
                    + "A nivel nacional, solo 1 de cada 5 motociclistas usa prenda reflectiva y casi la mitad de los acompañantes no usa casco (ANSV, 2024)."),
                Text(
                    "Documentos y kit básico",
                    "Además de tu licencia de conducción de categoría A2, la moto necesita licencia de tránsito, SOAT vigente y, desde los dos años de matriculada, revisión técnico-mecánica. "
                    + "La licencia de conducción digital tiene los mismos efectos que la física.\n\n"
                    + "Para recorridos largos, lleva un kit básico: llaves de la moto, destornilladores, alicate, kit para reparar pinchazos, linterna y un botiquín pequeño."),
                Video("ansv-moto-epp-lluvia.mp4", "El casco y el equipo también se cuidan: Cecilia y Roberto esperan a que pase la lluvia. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Text(
                    "La postura correcta",
                    "Espalda recta pero relajada, brazos ligeramente flexionados (no rígidos), rodillas apretando el tanque, pies sobre los reposapiés y la mirada lejos, hacia donde quieres ir. "
                    + "Una postura tensa cansa más y hace que reacciones peor. Antes de un trayecto largo, estira cuello, hombros, muñecas y piernas."),
                Classify(
                    "¿Este elemento protege al motociclista o no sirve como protección?",
                    ["Protege", "No protege"],
                    [
                        ("Casco certificado y abrochado", 0),
                        ("Guantes con protección en nudillos", 0),
                        ("Botas que cubren el tobillo", 0),
                        ("Chaqueta con protecciones en codos y hombros", 0),
                        ("Gorra debajo del casco sin abrochar", 1),
                        ("Chanclas", 1),
                        ("Impermeable tipo poncho", 1)
                    ],
                    "Un casco sin abrochar sale volando en el primer golpe. Las chanclas y el poncho, además de no proteger, pueden causar la caída."),
                Scenario(
                    "Vas a hacer un mandado de cinco cuadras en la moto y hace mucho calor en Barranquilla. ¿Qué haces con el casco?",
                    [
                        Choice("Lo llevo en el brazo, porque es un trayecto corto.", "La mayoría de las caídas ocurren cerca de casa y a baja velocidad. Un golpe en la cabeza a 30 km/h puede ser mortal."),
                        Choice("Me lo pongo y lo abrocho, aunque sean cinco cuadras.", "Correcto: el casco funciona solo si está puesto y abrochado, en cualquier distancia.", true),
                        Choice("Me lo pongo sin abrochar para que no me dé calor.", "Un casco sin abrochar se sale con el primer golpe y no protege nada.")
                    ]),
                TrueFalse(
                    "Con un casco abatible puedes circular con la mentonera levantada si hace calor, siempre que vaya abrochado.",
                    false,
                    "Falso: la norma exige que la mentonera de un casco abatible vaya cerrada y asegurada mientras circulas. Levantada no protege la cara."),
                TrueFalse(
                    "El acompañante corre menos riesgo que el conductor, por eso puede ir sin casco.",
                    false,
                    "Falso: el acompañante tiene el mismo riesgo y la ley le exige el casco igual que al conductor."),
                Quiz(
                    "¿Cuál es la tríada salvavidas para los elementos de protección?",
                    null,
                    ["Barato, bonito y cómodo", "Buena calidad, talla correcta y uso apropiado", "Color negro, liviano y grande", "Marca reconocida, nuevo y caro"],
                    1,
                    "Un buen casco de talla equivocada o sin abrochar no protege. Las tres condiciones se tienen que cumplir al tiempo.")
            ]
        ),
        (
            "motocicleta-a2/preoperacional",
            "Revisión preoperacional de la motocicleta",
            "Llantas, rines, frenos, controles, luces, fluidos, cadena y chasis antes de cada recorrido, y el mantenimiento preventivo que evita fallas.",
            20,
            [
                Text(
                    "Una moto revisada no te deja botado",
                    "En una moto, una falla pequeña tiene consecuencias grandes: una llanta con poca presión desestabiliza la dirección, una cadena floja puede saltarse "
                    + "y una luz de stop fundida hace que el de atrás no sepa que estás frenando. La revisión preoperacional toma pocos minutos y se hace antes de cada salida."),
                Text(
                    "Revisión preoperacional y mantenimiento preventivo",
                    "Son dos cosas distintas que se complementan:\n\n"
                    + "• Revisión preoperacional: la haces tú, antes de cada salida, en pocos minutos. Miras, pruebas y detectas fallas antes de rodar.\n"
                    + "• Mantenimiento preventivo: se hace cada cierto tiempo o kilometraje, según el manual de la moto: cambio de aceite, ajuste o cambio del kit de arrastre, pastillas de freno, limpieza y lubricación.\n\n"
                    + "La ANSV resume sus beneficios: disminuye los gastos en reparaciones, contribuye al cuidado del medio ambiente y te ayuda a conocer tu moto a fondo. "
                    + "Sobre todo, cuida tu vida y ayuda a evitar siniestros de tránsito."),
                Video("ansv-moto-revision.mp4", "Cecilia y Roberto revisan sus motos antes de continuar el viaje. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Flip(
                    "Qué revisar en cada parte",
                    Card("Llantas y rines", "Presión en frío según la carga, labrado, objetos incrustados, cortes o abultamientos. El rin no debe estar torcido ni los radios flojos o rotos."),
                    Card("Frenos", "Al accionar cada freno, la moto no debe rodar. Revisa el nivel del líquido y que las pastillas no estén gastadas."),
                    Card("Controles", "Acelerador que regrese solo, embrague con juego correcto, palancas sin daños y dirección que gire libre, sin ruidos."),
                    Card("Luces y sistema eléctrico", "Luz delantera alta y baja, stop con ambos frenos, direccionales, luz de placa y pito."),
                    Card("Aceite y fluidos", "Aceite del motor con la moto vertical y sin fugas debajo del motor. Líquido de frenos entre el mínimo y el máximo del depósito. Si tu moto es refrigerada por líquido, el refrigerante también va entre el mínimo y el máximo de su recipiente transparente; fíjate en su color. Revísalos con el motor frío."),
                    Card("Kit de arrastre", "Cadena lubricada y con la tensión que indica el manual; piñón y corona sin dientes gastados en punta."),
                    Card("Chasis", "Sin fisuras, soldaduras sueltas ni tornillos flojos. Espejos firmes y bien ajustados.")),
                Classify(
                    "¿Esta acción hace parte de la revisión de llantas, rines y frenos?",
                    ["Sí, es de llantas, rines y frenos", "No, es de otra parte de la moto"],
                    [
                        ("Verificar que el rin no esté torcido", 0),
                        ("Revisar que no haya objetos incrustados en la llanta", 0),
                        ("Verificar que los radios no estén doblados ni flojos", 0),
                        ("Comprobar que la moto no ruede al accionar cada freno", 0),
                        ("Revisar el nivel de aceite del motor", 1),
                        ("Probar las direccionales", 1),
                        ("Ajustar los espejos", 1)
                    ],
                    "La revisión se organiza por partes para no olvidar nada: primero llantas, rines y frenos; después controles, luces, fluidos y chasis."),
                Text(
                    "La cadena: el punto que más se olvida",
                    "Una cadena demasiado floja puede salirse y bloquear la rueda trasera; una demasiado tensa se desgasta rápido y daña los rodamientos. "
                    + "Revisa la tensión con la moto en el soporte, según el juego que indique el manual, y lubrícala cada pocos cientos de kilómetros o después de rodar con lluvia."),
                Text(
                    "Cada fluido en su lugar",
                    "En el recurso de la ANSV \"¿Un día perfecto?\", Juan empieza su primer día como domiciliario sin revisar la moto y le echa aceite de cocina al depósito del líquido de frenos. "
                    + "En plena entrega fallan el pito y los frenos, se estrella y casi atropella a un peatón con discapacidad visual en un paso peatonal.\n\n"
                    + "El sistema de frenos funciona con un líquido específico: usa solo el tipo que indican el manual o la tapa del depósito (por ejemplo, DOT 3 o DOT 4). "
                    + "Otro líquido, como el aceite, daña los empaques del sistema y puede dejarte sin frenos. Lo mismo vale para el aceite del motor y el refrigerante: nada de \"lo que haya en la casa\".\n\n"
                    + "Si el nivel del líquido de frenos baja, no basta con rellenar: puede ser que las pastillas estén gastadas o que haya una fuga. Revísalo antes de salir."),
                Text(
                    "Limpieza y herramientas en casa",
                    "Lavar la moto no es solo cuestión de estética: el polvo y el barro aceleran el desgaste de sus piezas, y al limpiarla ves a tiempo fugas, fisuras o tornillos flojos. "
                    + "Evita el chorro a presión directo sobre rodamientos, conectores eléctricos y el tablero.\n\n"
                    + "Para el mantenimiento básico, la ANSV recomienda tener en casa: destornilladores de pala y de estrella, alicates, pinzas, llaves combinadas, copas, llaves Allen, martillo, cinta aislante, "
                    + "lubricante y limpiador de cadena, cepillo o trapo, linterna, medidor de presión, profundímetro para el labrado y un kit de reparación de neumáticos.\n\n"
                    + "Fuente: recurso interactivo de la ANSV \"Mantenimiento preventivo y revisión preoperacional de la motocicleta\"."),
                Classify(
                    "¿Esta tarea es de la revisión preoperacional (antes de cada salida) o del mantenimiento preventivo (periódico)?",
                    ["Revisión preoperacional", "Mantenimiento preventivo"],
                    [
                        ("Probar las luces y el pito", 0),
                        ("Verificar la presión de las llantas", 0),
                        ("Mirar el nivel del líquido de frenos", 0),
                        ("Cambiar el aceite según el kilometraje del manual", 1),
                        ("Cambiar el kit de arrastre desgastado", 1),
                        ("Cambiar las pastillas de freno gastadas", 1)
                    ],
                    "La revisión preoperacional detecta problemas en minutos; el mantenimiento preventivo los corrige o los evita con cambios y ajustes periódicos."),
                Scenario(
                    "Al revisar la moto antes de salir notas que la luz de stop no enciende cuando frenas con la palanca delantera, aunque sí con el pedal. ¿Qué haces?",
                    [
                        Choice("Salgo igual, porque con el pedal sí funciona.", "Si frenas fuerte solo con el freno delantero, quien viene detrás no lo sabrá. Es una falla que debes corregir antes de salir."),
                        Choice("Reviso el interruptor de la palanca o la llevo a revisar antes de salir.", "Correcto: el stop debe encender con ambos frenos para que los demás sepan que te estás deteniendo.", true),
                        Choice("Uso solo el freno trasero durante el viaje.", "Con solo el freno trasero pierdes alrededor del 70 % de la capacidad de frenado de la moto.")
                    ]),
                Scenario(
                    "Vas a salir a trabajar y notas que el depósito del líquido de frenos está bajo el mínimo. Un vecino te dice que le eches un poco de aceite de cocina para salir del paso. ¿Qué haces?",
                    [
                        Choice("Le echo el aceite: es un líquido y sirve igual.", "El aceite daña los empaques del sistema de frenos y puede dejarte sin frenos en plena vía, como le pasó a Juan en el recurso de la ANSV."),
                        Choice("No salgo en la moto hasta conseguir el líquido que indica el manual y revisar por qué bajó el nivel.", "Correcto: solo el líquido indicado, y además hay que descartar pastillas gastadas o una fuga.", true),
                        Choice("Salgo igual y uso solo el freno de pie.", "Con un sistema de frenos con fallas no se rueda. Además, solo con el freno trasero pierdes gran parte de la capacidad de frenado.")
                    ]),
                TrueFalse(
                    "La presión de las llantas de la moto se debe ajustar según la carga que vas a llevar.",
                    true,
                    "Verdadero: con acompañante o con carga, el manual suele indicar una presión mayor. Revísala siempre en frío."),
                TrueFalse(
                    "Lavar la moto hace parte de su mantenimiento, no solo de su apariencia.",
                    true,
                    "Verdadero: el polvo y el barro desgastan las piezas, y al limpiarla puedes detectar fugas o daños a tiempo."),
                Quiz(
                    "¿Qué riesgo genera una cadena demasiado floja?",
                    null,
                    ["Ninguno, solo hace ruido", "Que se salga y bloquee la rueda trasera", "Que la moto consuma menos", "Que las luces no funcionen"],
                    1,
                    "Una cadena que se sale puede bloquear la rueda trasera en marcha y causar una caída.")
            ]
        ),
        (
            "motocicleta-a2/frenado-curvas",
            "Técnicas de manejo: frenado y curvas",
            "Cómo frenar con los dos frenos, cómo trazar una curva (frenar, inclinar, pasar y salir) y los errores más comunes.",
            16,
            [
                Text(
                    "Las tres formas de frenar",
                    "Solo freno trasero: muchos motociclistas lo usan por miedo a que se bloquee la llanta delantera, pero así se pierde cerca del 70 % de la capacidad de frenado "
                    + "y aumenta el riesgo de derrapar.\n\n"
                    + "Solo freno delantero: frena más, pero todavía se pierde cerca del 30 % de la capacidad y el peso se va hacia adelante, lo que compromete el equilibrio.\n\n"
                    + "Los dos frenos: es la técnica correcta. El delantero es el principal y el trasero estabiliza. En piso seco y plano se usa una proporción cercana a 70-30: "
                    + "más fuerza adelante que atrás, accionando el trasero un instante antes. En bajadas pronunciadas y con lluvia se aplica un poco más de fuerza atrás."),
                Quiz(
                    "En piso seco y plano, ¿cuál es la técnica de frenado más estable y efectiva?",
                    null,
                    ["Solo el freno trasero", "Solo el freno delantero", "Los dos frenos, con más fuerza en el delantero", "Frenar solo con el motor"],
                    2,
                    "Con los dos frenos usas el 100 % de la capacidad: el delantero detiene y el trasero estabiliza."),
                Text(
                    "Trazar una curva en cuatro pasos",
                    "1. Frenar: en línea recta, antes de la curva, ubica la moto en el lado exterior del carril y reduce la velocidad de forma progresiva.\n\n"
                    + "2. Inclinar: con la velocidad ya ajustada, inclina la moto en el punto adecuado, ni muy pronto ni muy tarde. Si inclinas con los frenos accionados, la moto se resiste.\n\n"
                    + "3. Pasar: cuando veas la salida despejada, ve cerrando gradualmente hacia el interior de la curva, sin salirte de tu carril.\n\n"
                    + "4. Salir: acelera de forma gradual, endereza el cuerpo y proyecta la mirada hacia adelante, buscando la siguiente curva."),
                Order(
                    "Ordena las fases para tomar una curva en moto.",
                    ["Frenar en línea recta, por el lado exterior del carril", "Inclinar la moto con la velocidad ya ajustada", "Pasar cerrando gradualmente hacia el interior", "Salir acelerando suave y mirando hacia adelante"],
                    "Todo el frenado se hace antes de inclinar. Dentro de la curva no se frena, a menos que sea necesario, y con suavidad."),
                Flip(
                    "Los errores más comunes en las curvas",
                    Card("Frenar dentro de la curva", "La moto se endereza y se sale de la trayectoria. El frenado se hace antes."),
                    Card("No bajar la velocidad antes", "Entras demasiado rápido y tienes que corregir en el peor momento."),
                    Card("No inclinar por miedo", "La moto abre la trayectoria y te saca del carril."),
                    Card("Usar el freno delantero en plena curva", "Puede hacer que la llanta delantera pierda agarre."),
                    Card("Invadir el carril contrario", "En una curva ciega, el que viene de frente no te ve."),
                    Card("No mirar hacia la salida", "La moto va hacia donde miras: mira la salida, no el borde.")),
                Video("clase-curva-moto.mp4", "Motos bajando una curva cerrada en una vía rural. Fíjate en la posición de cada una dentro del carril y en que, desde la entrada, no se alcanza a ver lo que viene. Video: Luz Verde."),
                Tip("Con lluvia o neblina, frena antes y con más suavidad, inclina menos y evita la franja central del carril, donde se acumula el aceite de los vehículos."),
                Scenario(
                    "Entras a una curva cerrada y te das cuenta de que vienes un poco más rápido de lo que debías. ¿Qué es lo más seguro?",
                    [
                        Choice("Frenar fuerte con el freno delantero en plena curva.", "Frenar fuerte adelante con la moto inclinada puede hacer que la llanta pierda agarre y caigas."),
                        Choice("Mantener la mirada en la salida, inclinar un poco más y, si debo frenar, hacerlo suave con el trasero.", "Correcto: la moto va hacia donde miras, y casi siempre tiene más capacidad de inclinación de la que el conductor cree.", true),
                        Choice("Enderezar la moto y frenar en línea recta, aunque me salga del carril.", "Salirte del carril en una curva te pone frente a quien viene en sentido contrario o fuera de la vía.")
                    ]),
                TrueFalse(
                    "La forma más segura de frenar una moto es usar solo el freno trasero.",
                    false,
                    "Falso: con solo el trasero se pierde cerca del 70 % de la capacidad de frenado y aumenta el riesgo de derrape."),
                Quiz(
                    "¿En qué momento debe hacerse el frenado para tomar una curva?",
                    null,
                    ["Dentro de la curva, con la moto inclinada", "Antes de la curva, en línea recta", "A la salida de la curva", "No se debe frenar nunca"],
                    1,
                    "Frenar en línea recta, antes de inclinar, permite usar los frenos con toda su capacidad sin comprometer el agarre.")
            ]
        ),
        (
            "motocicleta-a2/posicion-trafico",
            "Posición en la vía, puntos ciegos y tráfico urbano",
            "Dónde ubicar la moto dentro del carril, por qué zigzaguear entre carros es tan peligroso, el choque más común en las intersecciones y los puntos ciegos de los vehículos grandes.",
            16,
            [
                Text(
                    "Un carril es tuyo: ocúpalo",
                    "La moto es un vehículo y, como cualquier otro, circula ocupando un carril: así lo exige el Código Nacional de Tránsito. No es un vehículo de segunda que se acomoda en los espacios que dejan los demás.\n\n"
                    + "Dentro del carril, ubícate donde veas y te vean: normalmente hacia el lado izquierdo, donde el conductor de adelante te encuentra en su espejo. "
                    + "Aléjate del borde derecho, donde salen carros de los parqueaderos, se abren puertas y se acumulan huecos, arena y agua.\n\n"
                    + "Lleva la luz encendida también de día: para un conductor que mira rápido, una moto sin luz se confunde con el fondo."),
                Text(
                    "Zigzaguear y avanzar entre carros",
                    "Pasar entre dos filas de carros o cambiar de carril a cada momento parece ahorrar tiempo, pero te pone en el punto ciego de todos. "
                    + "Basta que un conductor cambie de carril sin verte, abra una puerta o mueva el espejo para que no tengas a dónde ir.\n\n"
                    + "Si el tráfico está detenido, espera tu turno en tu carril. Si necesitas adelantar, hazlo como cualquier vehículo: con espacio, direccional y por la izquierda."),
                Text(
                    "Intersecciones: el choque más común",
                    "Uno de los choques más frecuentes entre carro y moto ocurre cuando un carro gira a la izquierda frente a la moto que viene de frente, o sale de una vía secundaria o de un garaje: "
                    + "el conductor no la ve o calcula mal su velocidad.\n\n"
                    + "Al acercarte a un cruce baja la velocidad, ten los dedos listos sobre el freno, busca los ojos del otro conductor y piensa hacia dónde te moverías si no te ve. "
                    + "Tener la prelación no te protege si el otro no sabe que estás ahí."),
                Scenario(
                    "Vas derecho por una avenida y en el cruce un carro que viene de frente espera para girar a su izquierda, atravesándose en tu camino. ¿Qué haces?",
                    [
                        Choice("Mantengo la velocidad: tengo la prelación y él debe esperarme.", "Si el conductor no te ve o calcula mal tu velocidad, girará igual. Tener la razón no evita el golpe."),
                        Choice("Reduzco la velocidad, preparo el freno y busco sus ojos hasta estar seguro de que me vio.", "Correcto: te das tiempo y espacio por si el carro gira de todas formas.", true),
                        Choice("Acelero para pasar antes de que se decida a girar.", "Acelerar reduce tu margen de frenado y hace aún más difícil que el conductor calcule tu velocidad.")
                    ]),
                Text(
                    "Los puntos ciegos de los vehículos grandes",
                    "Si vas al lado y un poco atrás de un vehículo y no ves la cara del conductor en su espejo lateral, él tampoco te ve: estás en su punto ciego. "
                    + "En un camión o un bus, los puntos ciegos son enormes; según la ANSV pueden abarcar hasta 60 metros.\n\n"
                    + "No te quedes al lado de un vehículo grande. Ubícate detrás, donde el conductor te vea por el espejo central, o adelántalo con decisión y sin quedarte en su costado. "
                    + "Usa las direccionales, el cambio de luces y el pito de forma moderada para avisar."),
                Video("ansv-espejos-adelantar.mp4", "Usa los espejos y deja espacio cuando otro vehículo te adelanta. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Scenario(
                    "Vas en la moto por la Vía 40 junto a una tractomula, a la altura de su tanque de combustible. Ella pone la direccional hacia tu lado. ¿Qué haces?",
                    [
                        Choice("Acelero para pasarla antes de que se mueva.", "Si la mula ya empezó a moverse, acelerar a su costado te deja atrapado entre ella y el borde de la vía."),
                        Choice("Reduzco la velocidad, me ubico detrás de ella y le dejo espacio para la maniobra.", "Correcto: desde detrás el conductor te ve por sus espejos y tú tienes espacio para reaccionar.", true),
                        Choice("Pito fuerte y me mantengo en mi posición.", "Lo más probable es que el conductor no te vea ni te escuche. Quedarte en su punto ciego es lo más peligroso.")
                    ]),
                Classify(
                    "¿Esta posición o conducta te hace más visible y seguro, o te pone en riesgo?",
                    ["Más seguro", "En riesgo"],
                    [
                        ("Ocupar mi carril donde el carro de adelante me ve en su espejo", 0),
                        ("Quedarme detrás de un bus y no a su costado", 0),
                        ("Llevar la luz encendida también de día", 0),
                        ("Ir pegado al borde derecho junto a carros parqueados", 1),
                        ("Avanzar entre dos filas de carros detenidos", 1),
                        ("Subirme al andén para salir del trancón", 1)
                    ],
                    "Ser visto y tener espacio para reaccionar es lo que te protege. Los atajos entre carros o por el andén te dejan sin salida y sin que nadie te vea."),
                TrueFalse(
                    "Si no ves la cara del conductor en su espejo lateral, probablemente él tampoco te ve.",
                    true,
                    "Verdadero: es la regla práctica para saber si estás en un punto ciego. Si no ves sus ojos, apártate.")
            ]
        ),
        (
            "motocicleta-a2/clima-fatiga",
            "Lluvia, calor y fatiga",
            "Cómo adaptar la conducción a la lluvia, el viento y el calor, y cómo gestionar el cansancio en recorridos largos.",
            14,
            [
                Text(
                    "Los primeros minutos de lluvia son los peores",
                    "Cuando empieza a llover, el agua se mezcla con el polvo, el aceite y el combustible del pavimento y forma una capa muy resbalosa. "
                    + "Después de un rato la lluvia lava esa capa, pero el agarre sigue siendo menor que en seco y las distancias de frenado aumentan.\n\n"
                    + "Evita pisar líneas pintadas, tapas de alcantarilla, rejillas y manchas de grasa. Cuidado con los charcos: pueden esconder huecos. "
                    + "Y si la lluvia es muy fuerte o se forman arroyos, el consejo más importante es detenerte en un refugio seguro y esperar."),
                Video("ansv-moto-viaje-lluvia.mp4", "Planear la ruta y adaptarse cuando llega la lluvia. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Classify(
                    "¿Esta acción reduce el riesgo al conducir con lluvia?",
                    ["Reduce el riesgo", "Aumenta el riesgo"],
                    [
                        ("Frenar con suavidad para que no se bloqueen las ruedas", 0),
                        ("Encender las luces para ser más visible", 0),
                        ("Aumentar la distancia con el vehículo de adelante", 0),
                        ("Detenerse si la visibilidad baja demasiado", 0),
                        ("Pasar rápido por los charcos", 1),
                        ("Frenar sobre una línea pintada", 1),
                        ("Usar un impermeable tipo poncho", 1)
                    ],
                    "Con lluvia todo debe ser más suave y con más espacio. Y si el agua es demasiada, la mejor maniobra es detenerse."),
                Text(
                    "Aquaplaning, viento y calor",
                    "Aquaplaning: con mucha agua, la llanta flota sobre una película de agua y pierdes el control. Se evita con llantas en buen estado y velocidad moderada. "
                    + "Si la moto patina, no frenes: mantén la trayectoria con correcciones suaves.\n\n"
                    + "Viento: llega en ráfagas. Reduce la velocidad, agáchate un poco sobre el tanque, sujeta el manubrio con firmeza y circula por el lado del carril de donde viene el viento.\n\n"
                    + "Calor: en Barranquilla el calor y el sol cansan rápido. Hidrátate antes de tener sed, usa ropa clara y ventilada con protecciones, y ten cuidado después de comer: el calor aumenta el sueño."),
                Text(
                    "La fatiga",
                    "En moto el cuerpo trabaja más: equilibrio, viento, vibración y temperatura. La ANSV recomienda no viajar más de seis horas al día, parar al menos cada dos horas "
                    + "para estirar, hidratarse y revisar la moto, y no conducir si estás cansado. Los estimulantes y las bebidas energizantes no quitan el cansancio: lo esconden y después cae de golpe."),
                TrueFalse(
                    "Una bebida energizante quita el cansancio y permite seguir conduciendo varias horas más.",
                    false,
                    "Falso: los estimulantes esconden el cansancio por un rato y después cae de golpe. Lo único que lo quita es descansar."),
                Quiz(
                    "Según la ANSV, ¿cada cuánto se recomienda parar a descansar en un viaje largo en moto?",
                    null,
                    ["Cada ocho horas", "Al menos cada dos horas", "Solo al llegar al destino", "Cuando se acabe la gasolina"],
                    1,
                    "Parar cada dos horas permite estirar, hidratarse y revisar la moto. Además, no se recomienda viajar más de seis horas al día.")
            ]
        ),
        (
            "motocicleta-a2/acompanante-carga",
            "Acompañante, carga y fin del recorrido",
            "Cómo llevar un acompañante y transportar carga de forma segura, y qué hacer al terminar el recorrido.",
            20,
            [
                Text(
                    "Conducir con acompañante",
                    "Llevar a otra persona cambia el comportamiento de la moto: frena en más metros, acelera más lento y es más sensible en las curvas. Algunas reglas:\n\n"
                    + "Experiencia: si apenas estás aprendiendo, acumula práctica antes de llevar a alguien.\n\n"
                    + "Orden: el conductor sube primero y estabiliza la moto; el acompañante avisa, sube por el lado contrario al exhosto apoyándose en los hombros del conductor y se sienta erguido.\n\n"
                    + "Sincronía: el acompañante se inclina con el conductor en las curvas, nunca hacia el lado contrario.\n\n"
                    + "Apoyo: los pies van siempre en los reposapiés, aunque alcance el piso, y no hace señas a otros usuarios.\n\n"
                    + "Responsabilidad: no deben ir más de dos personas en la moto, y los niños pequeños no alcanzan los reposapiés ni pueden sujetarse bien."),
                Video("ansv-moto-acompanante.mp4", "Recomendaciones para viajar en moto con acompañante y con carga. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Pairs(
                    "Une cada momento con lo que debe hacer el acompañante.",
                    ("Al subirse", "Avisar al conductor y subir por el lado contrario al exhosto"),
                    ("En una curva", "Inclinarse con el conductor, nunca hacia el lado contrario"),
                    ("En un semáforo", "Mantener los pies en los reposapiés"),
                    ("Durante el recorrido", "Ir erguido y sujeto, sin hacer señas"),
                    ("Si el conductor hace maniobras peligrosas", "Pedirle que se detenga y no seguir el viaje")),
                Text(
                    "Transportar carga",
                    "La moto es un vehículo de dos ruedas con capacidad para el conductor y un acompañante; la carga la hace menos estable. Si vas a llevar algo:\n\n"
                    + "Respeta la capacidad de la parrilla que indica el manual.\n"
                    + "La carga no debe superar el ancho del manubrio ni sobresalir hacia los lados.\n"
                    + "No tapes las luces, las direccionales ni la placa.\n"
                    + "Ubícala baja, centrada y bien asegurada con cinchos, red elástica o caja.\n"
                    + "No pongas nada entre tu cuerpo y el manubrio.\n"
                    + "Si la carga supera esas dimensiones, usa otro vehículo."),
                Video("ansv-moto-carga.mp4", "Cómo transportar con seguridad artículos pequeños y livianos en moto. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Classify(
                    "¿Esta forma de llevar carga en la moto es segura?",
                    ["Segura", "Insegura"],
                    [
                        ("Una caja asegurada en la parrilla, sin superar su capacidad", 0),
                        ("Un morral bien ajustado a la espalda", 0),
                        ("Carga asegurada con cinchos y red elástica", 0),
                        ("Una caja que tapa la luz de stop", 1),
                        ("Un tubo largo que sobresale a los lados", 1),
                        ("Un bulto apoyado entre el pecho y el manubrio", 1),
                        ("Bolsas colgando del manubrio", 1)
                    ],
                    "La carga segura va baja, centrada, asegurada y sin tapar luces ni interferir con el manubrio."),
                Text(
                    "Al terminar el recorrido",
                    "Estaciona en un lugar seguro y, si puedes, a la sombra o bajo techo: el sol y la lluvia deterioran piezas de la moto. "
                    + "Haz estiramientos de cuello, espalda, muñecas y piernas, e hidrátate. Revisa si notaste ruidos o fallas durante el viaje para atenderlas antes del próximo recorrido."),
                Video("ansv-moto-fin-recorrido.mp4", "Al llegar: estacionar en un lugar seguro y recuperar el cuerpo. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Scenario(
                    "Un amigo te pide que lo lleves en la moto junto con su hijo de cinco años, «los tres, que es cerquita». ¿Qué haces?",
                    [
                        Choice("Acepto: es cerca y voy despacio.", "Tres personas en una moto superan su capacidad y el niño no alcanza los reposapiés ni puede sujetarse bien."),
                        Choice("No acepto llevar a tres personas y les sugiero otro medio de transporte.", "Correcto: en la moto no deben ir más de dos personas, y un niño pequeño corre un riesgo muy alto.", true),
                        Choice("Llevo al niño adelante, entre el manubrio y yo.", "Un niño entre tu cuerpo y el manubrio te impide maniobrar y queda sin ninguna protección en una frenada.")
                    ]),
                TrueFalse(
                    "En una curva, el acompañante debe inclinarse hacia el lado contrario para equilibrar la moto.",
                    false,
                    "Falso: es la reacción instintiva, pero es la incorrecta. El acompañante debe acompañar el movimiento del conductor."),
                Quiz(
                    "¿Hasta dónde puede llegar el ancho de la carga en una moto?",
                    null,
                    ["Hasta el ancho del manubrio", "Hasta un metro a cada lado", "No hay límite si va bien amarrada", "Hasta el ancho de un carro"],
                    0,
                    "La carga no debe superar el ancho del manubrio: si lo supera, cambia el equilibrio de la moto y puede golpear a otros vehículos.")
            ]
        )
    ];
}
