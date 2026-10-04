namespace Cale.Api.Services.Courses;

/// <summary>
/// "El vehículo: conócelo, revísalo y atiéndelo": núcleo 2-A-1 of the school's curriculum (temas A, B, C, E y F).
/// </summary>
public sealed partial class CourseSeed
{
    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> VehicleLessons() =>
    [
        (
            "Reconocimiento y funcionamiento del vehículo",
            "Motor, transmisión, frenos, dirección, suspensión, llantas, luces y fluidos: qué hace cada sistema y qué señales indican una falla.",
            16,
            [
                Text(
                    "El vehículo es un sistema de seguridad",
                    "Un vehículo no es solo un medio de transporte: cada uno de sus sistemas influye en que puedas frenar, girar, ver y ser visto a tiempo. "
                    + "Conocerlos te permite detectar una falla antes de que se convierta en un siniestro, ahorrar dinero en reparaciones y explicar con claridad qué le pasa al vehículo."),
                Flip(
                    "Los sistemas principales",
                    Card("Motor", "Transforma el combustible en movimiento. Necesita aceite para lubricarse y refrigerante para no recalentarse."),
                    Card("Transmisión", "Lleva la fuerza del motor a las ruedas: embrague, caja de cambios y, en la moto, el kit de arrastre (cadena, piñón y corona)."),
                    Card("Frenos", "Detienen el vehículo. Pueden ser de disco o de tambor y funcionan con líquido de frenos."),
                    Card("Dirección", "Permite orientar las ruedas. Un volante con juego o que «jala» hacia un lado indica una falla."),
                    Card("Suspensión", "Mantiene las llantas pegadas al piso y absorbe los golpes. Amortiguadores gastados alargan la frenada."),
                    Card("Llantas", "Son el único contacto con la vía: todo lo que haces al conducir pasa por cuatro (o dos) superficies del tamaño de una mano."),
                    Card("Sistema eléctrico", "Batería, alternador, luces, direccionales y pito. Sin él no ves ni te ven."),
                    Card("Fluidos", "Aceite de motor, refrigerante, líquido de frenos, líquido de dirección y agua del limpiabrisas.")),
                Text(
                    "Cómo funciona un motor de cuatro tiempos",
                    "La mayoría de carros y motos usan un motor de cuatro tiempos. En cada cilindro, el pistón sube y baja repitiendo cuatro pasos:\n\n"
                    + "1. Admisión: el pistón baja y entra la mezcla de aire y combustible.\n"
                    + "2. Compresión: el pistón sube y comprime la mezcla.\n"
                    + "3. Explosión: la bujía produce una chispa, la mezcla se quema y empuja el pistón hacia abajo. Es el único tiempo que produce fuerza.\n"
                    + "4. Escape: el pistón sube y expulsa los gases quemados.\n\n"
                    + "En los motores diésel no hay bujía: el combustible se enciende por la alta temperatura del aire comprimido."),
                Order(
                    "Ordena los cuatro tiempos del motor.",
                    ["Admisión", "Compresión", "Explosión", "Escape"],
                    "Entra la mezcla, se comprime, se quema empujando el pistón y salen los gases. Y vuelve a empezar."),
                Text(
                    "Los testigos del tablero",
                    "Las luces del tablero te hablan por colores:\n\n"
                    + "Rojo: detente en un lugar seguro lo antes posible. Por ejemplo, presión de aceite, temperatura del motor, falla de frenos o falla de carga de la batería.\n\n"
                    + "Amarillo o ámbar: revisa pronto, el vehículo puede seguir con precaución. Por ejemplo, falla del motor (check engine), ABS, presión de llantas o combustible en reserva.\n\n"
                    + "Verde o azul: solo informan que algo está encendido, como las luces bajas, las altas o las direccionales."),
                Pairs(
                    "Une cada testigo con lo que debes hacer.",
                    ("Testigo rojo de temperatura del motor", "Detenerte en un lugar seguro y apagar el motor"),
                    ("Testigo rojo de presión de aceite", "Detenerte de inmediato: el motor se puede fundir"),
                    ("Testigo amarillo de ABS", "Seguir con precaución: los frenos funcionan, pero sin ABS"),
                    ("Testigo azul de luces altas", "Cambiar a luces bajas si viene alguien de frente"),
                    ("Testigo amarillo de combustible", "Tanquear pronto")),
                Text(
                    "Leer una llanta",
                    "En el costado de cada llanta hay un código como 175/70 R13 82T:\n\n"
                    + "175: ancho de la llanta en milímetros.\n"
                    + "70: altura del costado, como porcentaje del ancho.\n"
                    + "R: construcción radial.\n"
                    + "13: diámetro del rin en pulgadas.\n"
                    + "82: índice de carga, es decir, el peso máximo que soporta.\n"
                    + "T: índice de velocidad máxima.\n\n"
                    + "La palabra «Tubeless» indica que la llanta no lleva neumático interno. La presión correcta está en el manual o en una etiqueta en el marco de la puerta, y se mide en frío."),
                FillBlank(
                    "En la llanta 175/70 R13, el número [[175]] es el ancho en milímetros y el [[13]] es el diámetro del rin en pulgadas. La presión se mide con la llanta [[fría]].",
                    ["70", "caliente", "82"],
                    "El ancho va primero, el perfil después y el rin al final. La presión en caliente da lecturas más altas que la real."),
                Scenario(
                    "Vas por la Circunvalar y la aguja de temperatura sube hasta la zona roja. ¿Qué haces?",
                    [
                        Choice("Sigo hasta mi casa, que queda a diez minutos.", "Con el motor recalentado puedes dañar la culata o fundir el motor en pocos minutos, y quedar detenido en un lugar peligroso."),
                        Choice("Me orillo en un lugar seguro, apago el motor y espero a que se enfríe antes de revisar.", "Correcto: detenerte a tiempo evita un daño mayor. Nunca abras la tapa del radiador con el motor caliente.", true),
                        Choice("Abro la tapa del radiador de inmediato para agregar agua.", "El refrigerante caliente sale a presión y causa quemaduras graves. Hay que esperar a que el motor se enfríe.")
                    ]),
                Quiz(
                    "¿Cómo se debe comprobar el nivel de aceite del motor?",
                    null,
                    ["Con el motor encendido", "Con el motor apagado y el vehículo en terreno plano", "Con el vehículo inclinado", "Mientras se conduce"],
                    1,
                    "Con el motor apagado (y unos minutos de espera) el aceite baja al cárter y la varilla marca el nivel real. En terreno inclinado la lectura es falsa.")
            ]
        ),
        (
            "Inspección preoperacional",
            "La revisión sistemática antes de arrancar: qué mirar, en qué orden y cuándo no se debe iniciar la marcha.",
            15,
            [
                Text(
                    "Cinco minutos que salvan vidas",
                    "La inspección preoperacional es una revisión rápida y ordenada que haces antes de mover el vehículo. No reemplaza al mecánico ni a la revisión técnico-mecánica: "
                    + "su objetivo es detectar fallas visibles que harían inseguro el recorrido de hoy.\n\n"
                    + "En las empresas con Plan Estratégico de Seguridad Vial (PESV) se registra en una lista de chequeo diaria. Para cualquier conductor, es un hábito."),
                Video("ansv-revision-preoperacional.mp4", "Revisión preoperacional del vehículo y la motocicleta. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Text(
                    "Una secuencia fácil de recordar",
                    "1. Alrededor del vehículo: llantas (presión, desgaste, cortes o abultamientos), fugas en el piso, golpes, placas y vidrios.\n\n"
                    + "2. Bajo el capó: niveles de aceite, refrigerante, líquido de frenos y agua del limpiabrisas.\n\n"
                    + "3. Luces: bajas, altas, direccionales, stop, reversa y parqueo. Pide ayuda o usa un reflejo para ver las de atrás.\n\n"
                    + "4. En la cabina: frenos (el pedal debe sentirse firme), freno de mano, dirección sin juego, pito, limpiabrisas, espejos ajustados y cinturones que enganchen.\n\n"
                    + "5. Equipo y documentos: equipo de carretera completo y documentos al día."),
                Order(
                    "Ordena la inspección de afuera hacia adentro.",
                    ["Dar la vuelta revisando llantas y fugas", "Abrir el capó y revisar niveles", "Probar todas las luces", "Sentarse y probar frenos, dirección, pito y espejos", "Verificar equipo de carretera y documentos"],
                    "Ir de afuera hacia adentro evita olvidar puntos: primero lo que ves alrededor, luego el motor, las luces, los mandos y al final el equipo."),
                Text(
                    "El estado de las llantas",
                    "La profundidad mínima del labrado en vehículos livianos es de 1,6 milímetros (NTC 5375). Muchas llantas traen un testigo de desgaste: "
                    + "un pequeño relieve dentro de las ranuras que, cuando queda al nivel de la banda, indica que hay que cambiarla.\n\n"
                    + "Desgaste en el centro: exceso de presión. Desgaste en ambos bordes: falta de presión. Desgaste en un solo borde: problema de alineación o suspensión."),
                Classify(
                    "Si encuentras esto en la inspección, ¿puedes iniciar la marcha?",
                    ["Puedo salir con precaución y programar la revisión", "No debo iniciar la marcha"],
                    [
                        ("El agua del limpiabrisas está baja", 0),
                        ("Una luz de la placa está fundida", 0),
                        ("El pedal del freno se va hasta el fondo", 1),
                        ("Una llanta tiene un abultamiento en el costado", 1),
                        ("Hay un charco de líquido de frenos bajo una rueda", 1),
                        ("El testigo rojo de aceite queda encendido con el motor en marcha", 1),
                        ("No funcionan las luces de stop", 1)
                    ],
                    "Frenos, llantas, aceite y luces de freno afectan directamente tu capacidad de detenerte o de ser visto. Con esas fallas, el vehículo no está apto para circular."),
                Scenario(
                    "Antes de salir a una práctica nocturna, revisas el vehículo y notas que las luces bajas no encienden, aunque las altas sí. ¿Qué decides?",
                    [
                        Choice("Salgo con las altas encendidas todo el tiempo.", "Las altas encandilan a quienes vienen de frente y a los que van adelante, y eso también puede causar un siniestro."),
                        Choice("No salgo hasta revisar el fusible o la bombilla y tener las luces bajas funcionando.", "Correcto: de noche las luces son tu forma de ver y de ser visto. Sin luces bajas el vehículo no está apto.", true),
                        Choice("Salgo con las luces de parqueo porque la ciudad está iluminada.", "Las luces de parqueo no iluminan la vía y hacen que otros calculen mal tu distancia.")
                    ]),
                TrueFalse(
                    "Si el vehículo pasó la revisión técnico-mecánica este año, no hace falta hacer la inspección preoperacional.",
                    false,
                    "Falso: la revisión técnico-mecánica se hace una vez al año. Una llanta se puede pinchar o una luz fundir cualquier día."),
                Quiz(
                    "¿Qué indica un desgaste mayor en el centro de la banda de rodamiento?",
                    null,
                    ["Falta de presión", "Exceso de presión", "Frenos desgastados", "Llanta nueva"],
                    1,
                    "Con exceso de presión la llanta se abomba y apoya más en el centro. Con poca presión, se apoya en los bordes.")
            ]
        ),
        (
            "Seguridad activa, pasiva y asistencias",
            "ABS, control de estabilidad, cinturón, airbags y apoyacabezas: qué hace cada sistema, qué no puede hacer y por qué no reemplaza al conductor.",
            15,
            [
                Text(
                    "Antes y durante el choque",
                    "Seguridad activa: los sistemas que ayudan a evitar el siniestro, como los frenos, la dirección, las llantas, las luces, el ABS y el control de estabilidad.\n\n"
                    + "Seguridad pasiva: los que reducen las lesiones cuando el choque ya es inevitable, como el cinturón, los airbags, los apoyacabezas, la carrocería con zonas de deformación "
                    + "y los sistemas de retención infantil."),
                Video("ansv-vehiculo-seguro.mp4", "Cómo evolucionó el concepto de vehículo seguro. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Classify(
                    "¿Es un sistema de seguridad activa o pasiva?",
                    ["Activa: ayuda a evitar el choque", "Pasiva: reduce las lesiones"],
                    [
                        ("Frenos ABS", 0),
                        ("Control de estabilidad (ESC)", 0),
                        ("Luces en buen estado", 0),
                        ("Llantas con buen labrado", 0),
                        ("Cinturón de seguridad", 1),
                        ("Airbag", 1),
                        ("Apoyacabezas", 1),
                        ("Silla para niños", 1)
                    ],
                    "Lo activo actúa antes del choque; lo pasivo protege durante el choque."),
                Flip(
                    "Asistencias electrónicas",
                    Card("ABS", "Evita que las ruedas se bloqueen al frenar fuerte, para que puedas seguir girando el volante. Si el pedal vibra, no lo sueltes: es normal."),
                    Card("Control de estabilidad (ESC o ESP)", "Frena ruedas de forma individual para corregir un derrape en una curva o una maniobra brusca."),
                    Card("Control de tracción", "Evita que las ruedas patinen al acelerar en piso mojado o con arena."),
                    Card("CBS en motos", "Frenado combinado: al accionar un freno, actúa también sobre la otra rueda para repartir la frenada."),
                    Card("Sensores y cámaras", "Ayudan a parquear y a ver puntos ciegos, pero no ven todo: siempre mira tú también."),
                    Card("Frenado autónomo de emergencia", "Algunos vehículos frenan solos si detectan un obstáculo. No funciona en todas las condiciones.")),
                Text(
                    "La tecnología no cambia la física",
                    "El ABS no hace que el vehículo frene en menos metros en cualquier piso: en grava o arena la distancia puede ser incluso mayor. "
                    + "El control de estabilidad no puede corregir una curva tomada demasiado rápido. Ningún sistema compensa la distracción, el alcohol o el exceso de velocidad.\n\n"
                    + "Usa la tecnología como apoyo, nunca como excusa para arriesgar más. Y conoce el equipamiento real de tu vehículo: no le atribuyas funciones que no tiene."),
                Text(
                    "Cinturón, airbag y apoyacabezas",
                    "El cinturón mantiene tu cuerpo en el asiento durante un choque. Debe ir sobre la clavícula y la cadera, nunca debajo del brazo ni sobre el abdomen. "
                    + "Es obligatorio para todos los ocupantes de los asientos que lo tienen.\n\n"
                    + "El airbag está diseñado para funcionar junto con el cinturón. Sin cinturón, el cuerpo sale hacia adelante y el airbag puede causar lesiones.\n\n"
                    + "El apoyacabezas evita el «latigazo cervical» en los choques por detrás: su parte superior debe quedar a la altura de la parte superior de tu cabeza, y lo más cerca posible de ella.\n\n"
                    + "Los niños menores de 10 años no pueden viajar en el asiento delantero y deben usar un sistema de retención adecuado a su talla y peso."),
                Scenario(
                    "Frenas de emergencia en un carro con ABS y sientes que el pedal vibra y hace ruido. ¿Qué haces?",
                    [
                        Choice("Suelto el pedal, porque algo se dañó.", "La vibración es el ABS trabajando. Si sueltas el pedal, el carro deja de frenar justo cuando más lo necesitas."),
                        Choice("Mantengo el pedal presionado con fuerza y giro el volante para esquivar si hace falta.", "Correcto: el ABS evita que las ruedas se bloqueen, así puedes frenar a fondo y seguir dirigiendo el vehículo.", true),
                        Choice("Bombeo el freno rápidamente, como en los carros antiguos.", "Bombear era la técnica sin ABS. Con ABS, el sistema ya lo hace por ti, mucho más rápido.")
                    ]),
                TrueFalse(
                    "Si el carro tiene airbags, no es necesario usar el cinturón de seguridad.",
                    false,
                    "Falso: el airbag complementa al cinturón. Sin cinturón el cuerpo se desplaza y el airbag puede causar lesiones graves."),
                Quiz(
                    "¿Qué limitación conserva el conductor aunque el vehículo tenga control de estabilidad?",
                    null,
                    ["Ninguna, el sistema corrige cualquier error", "No puede entrar a una curva a cualquier velocidad: la física no cambia", "No puede usar el cinturón", "Debe desactivarlo en la ciudad"],
                    1,
                    "El ESC ayuda a corregir un derrape, pero no puede crear agarre donde no lo hay. La velocidad adecuada sigue siendo responsabilidad del conductor.")
            ]
        ),
        (
            "Equipo de prevención y protección de la escena",
            "Cómo usar el equipo de carretera cuando el vehículo queda detenido: luces, chaleco, señales, extintor y la seguridad de los pasajeros.",
            12,
            [
                Text(
                    "El equipo sirve si sabes usarlo",
                    "Llevar el equipo de carretera completo es obligatorio, pero lo que salva vidas es usarlo bien. Cuando un vehículo queda detenido en la vía, "
                    + "el mayor peligro es que otro vehículo lo choque: se llama siniestro secundario, y suele ser más grave que la avería misma."),
                Order(
                    "El vehículo se varó en una avenida. Ordena las acciones para proteger la escena.",
                    ["Encender las luces de parqueo y orillarse lo más posible", "Ponerse el chaleco reflectivo antes de bajarse", "Bajarse por el lado contrario al tráfico", "Llevar a los pasajeros a un lugar seguro, fuera de la vía", "Ubicar las señales reflectivas detrás del vehículo"],
                    "Primero te haces visible, luego te proteges tú, sacas a los pasajeros del peligro y finalmente avisas con anticipación a los que vienen."),
                Text(
                    "Dónde poner las señales",
                    "Las señales reflectivas (triángulos) deben verse con tiempo suficiente para que los demás reaccionen. Como referencia práctica, "
                    + "ubícalas a unos 30 metros en vía urbana y a unos 50 a 100 metros en carretera. Si el vehículo quedó después de una curva o de una loma, "
                    + "pon la señal antes de la curva, donde los conductores todavía no te ven.\n\n"
                    + "Camina por fuera de la calzada, de frente al tráfico y con el chaleco puesto. De noche, usa la linterna."),
                Text(
                    "Uso del extintor",
                    "Si hay un conato de incendio y es seguro intentarlo, recuerda cuatro pasos:\n\n"
                    + "1. Halar el pasador de seguridad.\n"
                    + "2. Apuntar la boquilla a la base del fuego, no a las llamas.\n"
                    + "3. Apretar la palanca.\n"
                    + "4. Barrer de lado a lado.\n\n"
                    + "Mantén siempre una salida a tu espalda y no abras completamente el capó si sale humo: el aire aviva el fuego. Si el fuego crece, aléjate y llama al 123."),
                Order(
                    "Ordena los pasos para usar el extintor.",
                    ["Halar el pasador de seguridad", "Apuntar a la base del fuego", "Apretar la palanca", "Barrer de lado a lado"],
                    "Halar, apuntar, apretar y barrer. Siempre con una vía de escape detrás de ti."),
                Text(
                    "Si llevas pasajeros",
                    "En un vehículo de servicio público, los pasajeros son tu responsabilidad. No los dejes dentro de un vehículo detenido en un carril de circulación: "
                    + "guíalos a un lugar seguro, detrás de una barrera o lejos de la calzada, y mantenlos informados mientras llega la asistencia."),
                Scenario(
                    "Se pincha una llanta de noche en la Vía al Mar, justo después de una curva. ¿Qué haces primero?",
                    [
                        Choice("Me bajo de inmediato a cambiar la llanta, para terminar rápido.", "Quedas en un tramo sin visibilidad, donde los vehículos que salen de la curva no alcanzan a verte."),
                        Choice("Enciendo las luces de parqueo, me pongo el chaleco y pongo la señal antes de la curva.", "Correcto: primero haces visible la escena desde donde los demás todavía pueden reaccionar.", true),
                        Choice("Me quedo dentro del carro con las luces apagadas esperando ayuda.", "Un vehículo detenido sin luces en la vía es casi invisible de noche y tú quedas dentro de la zona de impacto.")
                    ]),
                TrueFalse(
                    "Al usar un extintor se debe apuntar a la parte alta de las llamas.",
                    false,
                    "Falso: se apunta a la base del fuego, donde está el material que se quema."),
                Quiz(
                    "¿Por qué los pasajeros no deben quedarse dentro de un vehículo varado en un carril de circulación?",
                    null,
                    ["Porque se pueden aburrir", "Porque un vehículo detenido puede ser chocado por otro que no lo vea a tiempo", "Porque lo prohíbe el SOAT", "No hay ningún problema en que se queden"],
                    1,
                    "El siniestro secundario es uno de los mayores peligros de una avería. Fuera de la vía y detrás de una barrera, los pasajeros están más seguros.")
            ]
        ),
        (
            "Averías frecuentes e inmovilización segura",
            "Pinchazo, batería descargada, recalentamiento, fugas y fallas de luces: qué puedes resolver tú y cuándo debes detenerte y pedir asistencia.",
            20,
            [
                Text(
                    "Continuar o no continuar",
                    "Ante una falla, la pregunta clave es: ¿puedo seguir sin ponerme en riesgo ni poner en riesgo a otros? "
                    + "Algunas fallas permiten llegar con precaución a un taller cercano; otras exigen detenerse de inmediato.\n\n"
                    + "Y si te detienes, el lugar importa: busca una bahía, una calle lateral o un parqueadero, lejos de curvas, puentes y zonas escolares. "
                    + "En Barranquilla, ten en cuenta que las obras y los cierres parciales pueden dejarte sin berma."),
                Classify(
                    "¿Qué debes hacer con cada falla?",
                    ["Detenerme de inmediato en un lugar seguro", "Seguir con precaución a un taller cercano"],
                    [
                        ("El pedal del freno se va al fondo", 0),
                        ("Sale humo del capó", 0),
                        ("Testigo rojo de presión de aceite", 0),
                        ("Una llanta pinchada", 0),
                        ("Una luz de stop fundida, de día", 1),
                        ("Ruido leve al pasar por huecos", 1),
                        ("Testigo amarillo de revisión del motor, sin otros síntomas", 1)
                    ],
                    "Si la falla afecta frenos, llantas, el motor o puede causar un incendio, detente. Si es menor y de día, llega con precaución a revisarla."),
                Text(
                    "Reventón de una llanta",
                    "Si una llanta se revienta en marcha, el vehículo tira hacia ese lado. Sujeta el volante con firmeza, no frenes bruscamente, "
                    + "suelta el acelerador poco a poco y mantén la dirección recta. Cuando la velocidad haya bajado, frena con suavidad y orilla el vehículo."),
                Video("ansv-cambio-llanta.mp4", "Cómo cambiar una llanta de forma segura con el equipo de carretera. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Order(
                    "Ordena los pasos para cambiar una llanta de forma segura.",
                    ["Estacionar en terreno firme y plano, con freno de mano y luces de parqueo", "Señalizar la escena y bloquear las llantas con los tacos", "Aflojar las tuercas con el carro aún en el piso", "Levantar el vehículo con el gato en el punto indicado", "Cambiar la llanta y apretar las tuercas en cruz", "Bajar el vehículo y dar el apriete final"],
                    "Las tuercas se aflojan con la llanta en el piso, para que no gire, y se aprietan en cruz para que el rin asiente parejo."),
                Video("ansv-llanta-moto.mp4", "Procedimiento para atender una llanta pinchada en motocicleta. Video: Agencia Nacional de Seguridad Vial (ANSV)."),
                Text(
                    "Batería descargada",
                    "Si al girar la llave el motor no arranca y las luces se ven débiles, probablemente es la batería. Revisa que los bornes estén apretados y sin sulfato (un polvo blanco o verdoso).\n\n"
                    + "Para pasar corriente con cables: rojo al positivo (+) de la batería descargada, el otro extremo rojo al positivo de la batería buena, "
                    + "negro al negativo (−) de la batería buena y el último extremo negro a una parte metálica sin pintura del motor del vehículo descargado, lejos de la batería. "
                    + "Se retiran en orden inverso."),
                Order(
                    "Ordena la conexión de los cables para pasar corriente.",
                    ["Rojo al positivo de la batería descargada", "Rojo al positivo de la batería buena", "Negro al negativo de la batería buena", "Negro a una parte metálica del motor descargado"],
                    "La última conexión se hace lejos de la batería descargada porque puede soltar una chispa, y las baterías liberan gases inflamables."),
                Text(
                    "Fugas: el color te dice qué es",
                    "Verde, rosado o anaranjado, con olor dulce: refrigerante.\n"
                    + "Café oscuro o negro: aceite de motor.\n"
                    + "Rojizo: aceite de transmisión automática o de dirección.\n"
                    + "Amarillento o transparente y aceitoso: líquido de frenos. ¡No conduzcas!\n"
                    + "Agua transparente bajo el carro después de usar el aire acondicionado: es normal."),
                Pairs(
                    "Une cada mancha con el fluido que probablemente es.",
                    ("Verde o rosada con olor dulce", "Refrigerante"),
                    ("Negra y espesa", "Aceite de motor"),
                    ("Rojiza", "Aceite de transmisión o de dirección"),
                    ("Amarillenta y aceitosa cerca de una rueda", "Líquido de frenos"),
                    ("Agua clara después de usar el aire", "Condensación normal del aire acondicionado")),
                Scenario(
                    "Se pincha una llanta en un carril rápido de la Circunvalar, en pleno tráfico y sin berma por una obra. ¿Qué haces?",
                    [
                        Choice("Cambio la llanta ahí mismo, rápido.", "Arrodillarte junto a un carril rápido sin protección es exponerte a ser atropellado."),
                        Choice("Con luces de parqueo, avanzo despacio hasta un lugar seguro y, si no es posible, me pongo a salvo y pido asistencia.", "Correcto: rodar despacio unos metros puede dañar el rin, pero eso se repara; un atropello no.", true),
                        Choice("Sigo a velocidad normal hasta la casa.", "Rodar rápido con la llanta pinchada destruye la llanta y el rin y puedes perder el control.")
                    ]),
                Quiz(
                    "Al reemplazar un fusible fundido, ¿qué debes tener en cuenta?",
                    null,
                    ["Que sea de mayor amperaje para que no se vuelva a fundir", "Que tenga el mismo amperaje que el original", "Que sea de cualquier color", "Que sea de un tamaño mayor"],
                    1,
                    "Un fusible de mayor amperaje deja pasar más corriente de la que el circuito soporta y puede causar un incendio. Siempre el mismo amperaje.")
            ]
        )
    ];
}
