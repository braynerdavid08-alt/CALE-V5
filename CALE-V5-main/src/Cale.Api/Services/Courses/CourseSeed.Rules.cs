namespace Cale.Api.Services.Courses;

/// <summary>"Normas de tránsito básicas": original texts based on Ley 769 de 2002 and its current amendments.</summary>
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

    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> RulesLessons() =>
    [
        (
            "Las normas y quién las hace cumplir",
            "Qué es el Código Nacional de Tránsito, por qué la autorregulación salva vidas y cuáles son las autoridades de tránsito.",
            10,
            [
                Text(
                    "Un acuerdo para movernos todos",
                    "En Colombia las reglas para circular están en el Código Nacional de Tránsito Terrestre (Ley 769 de 2002), que se ha actualizado con varias leyes posteriores. "
                    + "Aplica a conductores, pasajeros, peatones, ciclistas y a cualquier persona que use la vía.\n\n"
                    + "El código se organiza en títulos: primero las definiciones y autoridades, luego los requisitos para conducir y para los vehículos, "
                    + "después las normas de comportamiento en la vía y, al final, las infracciones, las sanciones y el procedimiento para aplicarlas."),
                Text(
                    "Autorregulación: la norma que te pones tú",
                    "No siempre habrá un agente mirando. La autorregulación es cumplir las normas porque entiendes para qué sirven, no por miedo a la multa.\n\n"
                    + "Un conductor autorregulado respeta el límite de velocidad aunque la vía esté vacía, no usa el celular aunque nadie lo vea y "
                    + "decide no conducir si está cansado o si tomó alcohol. Es la diferencia entre aprobar el examen y ser un buen conductor."),
                Text(
                    "Las autoridades de tránsito",
                    "La ley enumera las autoridades de tránsito en este orden: el Ministro de Transporte; los gobernadores y alcaldes; "
                    + "los organismos de tránsito departamentales, municipales o distritales; la Policía Nacional a través de su Dirección de Tránsito y Transporte; "
                    + "los inspectores de policía y de tránsito, corregidores o quien haga sus veces; la Superintendencia de Transporte; "
                    + "las Fuerzas Militares, solo donde no haya otra autoridad de tránsito; y los agentes de tránsito y transporte."),
                Tip("En la vía, las indicaciones del agente de tránsito están por encima de los semáforos y de las señales. Si te da una orden, la obedeces."),
                Order(
                    "Ordena estas autoridades como aparecen en la ley, de la primera a la última.",
                    ["Ministro de Transporte", "Gobernadores y alcaldes", "Organismos de tránsito departamentales y municipales", "Agentes de tránsito y transporte"],
                    "La ley empieza por el Ministro de Transporte y termina con los agentes, que son quienes ves a diario en la calle."),
                TrueFalse(
                    "Las Fuerzas Militares pueden regular el tránsito en zonas donde no hay ninguna autoridad de tránsito.",
                    true,
                    "Es verdadero: la ley se lo permite solo en las zonas donde no haya presencia de otra autoridad de tránsito."),
                Quiz(
                    "Son las 11 de la noche y la avenida está vacía. ¿Qué haría un conductor autorregulado?",
                    null,
                    ["Ir más rápido, porque no hay agentes", "Mantener el límite de velocidad, porque el riesgo sigue ahí", "Pasarse los semáforos en rojo si no viene nadie", "Usar el celular, porque hay poco tráfico"],
                    1,
                    "La autorregulación consiste en cumplir aunque nadie vigile: de noche hay menos visibilidad y un peatón o un ciclista puede aparecer en cualquier momento.")
            ]
        ),
        (
            "Documentos y equipo obligatorio",
            "Qué debes portar al conducir, cuándo se hace la revisión técnico-mecánica y qué lleva el equipo de carretera.",
            10,
            [
                Text(
                    "Los papeles del conductor y del vehículo",
                    "Para conducir necesitas tu licencia de conducción vigente y de la categoría correcta. El vehículo debe tener su licencia de tránsito (la tarjeta de propiedad), "
                    + "el seguro obligatorio SOAT vigente y, cuando le corresponda, la revisión técnico-mecánica y de emisiones contaminantes.\n\n"
                    + "Hoy muchos de estos datos se consultan en el RUNT, pero es buena práctica llevar los documentos o tenerlos a mano."),
                Text(
                    "¿Cuándo toca la revisión técnico-mecánica?",
                    "Los carros particulares nuevos hacen la primera revisión cuando cumplen cinco años de matriculados. "
                    + "Los vehículos de servicio público y las motocicletas la hacen a los dos años. Después de la primera, la revisión se renueva cada año."),
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
                    "Carro particular nuevo: primera revisión a los 5 años. Motos y servicio público: a los 2 años. Después, cada año."),
                TrueFalse(
                    "Si tu carro particular es nuevo, puedes circular sin revisión técnico-mecánica durante sus primeros cinco años.",
                    true,
                    "Correcto: los particulares nuevos (que no sean motos) hacen la primera revisión a partir del quinto año de matrícula.")
            ]
        ),
        (
            "Velocidad según el tipo de vía",
            "Los límites máximos en ciudad, en zonas escolares y residenciales, y en carretera.",
            12,
            [
                Text(
                    "Los límites que debes saber de memoria",
                    "En vías urbanas la autoridad local fija el límite, pero nunca puede pasar de 50 km/h. En zonas escolares y residenciales el máximo es 30 km/h.\n\n"
                    + "En carreteras nacionales y departamentales el límite tampoco puede superar los 90 km/h, salvo en dobles calzadas que no tengan pasos peatonales, "
                    + "donde puede llegar a 120 km/h. Los vehículos de servicio público de carga nunca pueden pasar de 80 km/h."),
                Text(
                    "Cuándo bajar a 30 km/h",
                    "Aunque la señal diga otra cosa, debes reducir a 30 km/h en lugares donde se concentran personas, en zonas residenciales y escolares, "
                    + "cuando hay poca visibilidad (lluvia fuerte, niebla, de noche sin iluminación), cuando una señal lo ordena y al acercarte a una intersección."),
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
                Tip("El límite es un máximo, no una meta. Con lluvia, tráfico o peatones cerca, lo seguro casi siempre es ir más despacio."),
                Classify(
                    "Arrastra cada situación al límite máximo que le corresponde.",
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
                TrueFalse(
                    "Una alcaldía puede autorizar 60 km/h en una avenida de la ciudad.",
                    false,
                    "Falso: desde la Ley 2251 de 2022 ninguna vía urbana puede tener un límite superior a 50 km/h."),
                Scenario(
                    "Vas a 50 km/h por una avenida y empieza a llover muy fuerte; casi no ves los carros de adelante. ¿Qué haces?",
                    [
                        Choice("Mantengo los 50 km/h, porque es el límite de la vía.", "El límite es para buenas condiciones. Con poca visibilidad la norma te obliga a bajar a 30 km/h; a 50 no alcanzarías a frenar a tiempo."),
                        Choice("Bajo a 30 km/h o menos, enciendo las luces y aumento la distancia con el carro de adelante.", "Es lo correcto: con poca visibilidad debes reducir a 30 km/h, y más distancia te da tiempo para frenar en piso mojado.", true),
                        Choice("Enciendo las luces de parqueo y sigo a la misma velocidad.", "Las estacionarias no reemplazan reducir la velocidad, y en movimiento confunden a los demás conductores.")
                    ]),
                Quiz(
                    "¿Cuál es la velocidad máxima para un camión de servicio público de carga en carretera?",
                    null,
                    ["60 km/h", "90 km/h", "80 km/h", "120 km/h"],
                    2,
                    "Para el servicio público de carga el límite nunca puede superar los 80 km/h, aunque la vía permita más a otros vehículos.")
            ]
        ),
        (
            "Prelación: quién pasa primero",
            "Peatones, vehículos de emergencia, intersecciones sin señal, glorietas y pendientes.",
            12,
            [
                Text(
                    "Primero las personas",
                    "Los conductores de vehículos motorizados deben respetar los derechos y la integridad de los peatones, los ciclistas y los usuarios de vehículos eléctricos livianos, "
                    + "y darles prelación en la vía. Quien va en el vehículo más grande tiene más responsabilidad, porque puede hacer más daño."),
                Text(
                    "Las reglas de prelación en intersecciones",
                    "En una intersección sin señales (que no sea glorieta) pasa primero el vehículo que viene por tu derecha.\n\n"
                    + "Si dos vehículos llegan de frente y uno va a girar a la izquierda, pasa primero el que sigue derecho.\n\n"
                    + "Dentro de una glorieta tiene prelación quien ya circula en ella, siempre que esté en movimiento; quien va a entrar espera.\n\n"
                    + "En una pendiente donde no caben los dos, tiene prelación el vehículo que sube."),
                Text(
                    "Vehículos de emergencia",
                    "Cuando una ambulancia, los bomberos, la policía u otro vehículo de emergencia anuncia su presencia con luces o sirena, "
                    + "debes orillarte al costado derecho y detenerte hasta que pase."),
                Scenario(
                    "Llegas a un cruce sin semáforo ni señales. Al mismo tiempo, un carro llega por la vía de tu derecha. ¿Qué haces?",
                    [
                        Choice("Acelero para cruzar primero.", "En un cruce sin señales tiene prelación quien viene por la derecha. Acelerar puede terminar en un choque lateral."),
                        Choice("Le cedo el paso y cruzo cuando haya pasado.", "Correcto: en intersecciones sin señalizar pasa primero el vehículo de la derecha.", true),
                        Choice("Pito para que el otro me deje pasar.", "Pitar no te da prelación. La regla dice que pasa el de la derecha.")
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
            "Adelantar y estacionar",
            "Dónde está prohibido adelantar y dónde no puedes dejar el vehículo estacionado.",
            12,
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
                    "Dónde y cómo estacionar",
                    "En las vías urbanas donde se permite estacionar, el vehículo debe quedar del lado autorizado, a no más de 30 centímetros del andén y "
                    + "a por lo menos 5 metros de la intersección.\n\n"
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
            "Alcohol, fatiga y conducta en la vía",
            "Grados de alcoholemia, cómo afectan el cansancio y el estrés, y qué conductas son apropiadas al volante.",
            12,
            [
                Text(
                    "Alcohol: no hay cantidad segura",
                    "Desde 20 miligramos de etanol por cada 100 mililitros de sangre ya hay sanción. La ley define cuatro niveles:\n\n"
                    + "Grado cero: de 20 a 39 mg/100 ml.\nPrimer grado: de 40 a 99 mg/100 ml.\nSegundo grado: de 100 a 149 mg/100 ml.\nTercer grado: 150 mg/100 ml o más.\n\n"
                    + "En todos los casos se retiene la licencia de forma preventiva y el vehículo se inmoviliza. Si te niegas a hacer la prueba, te cancelan la licencia "
                    + "y la multa es de 1.440 salarios mínimos diarios. Además, las multas por alcohol no tienen el descuento por hacer el curso."),
                Text(
                    "Cansancio, estrés y comida",
                    "La fatiga hace que reacciones tarde, igual que el alcohol. Si sientes los párpados pesados, bostezas seguido o no recuerdas los últimos kilómetros, "
                    + "detente en un sitio seguro y descansa. En viajes largos para cada dos horas, aproximadamente.\n\n"
                    + "El estrés y la rabia te llevan a acelerar, pitar o cerrar a otros. Respira, sal con tiempo y no respondas a provocaciones.\n\n"
                    + "Una comida muy pesada antes de manejar da sueño; un estómago vacío baja la concentración. Lo mejor es comer liviano e hidratarse."),
                Tip("El café, una ducha fría o comer algo no bajan el alcohol en la sangre. Solo el tiempo lo elimina. Si tomaste, no manejes."),
                FillBlank(
                    "El grado cero de alcoholemia empieza en [[20]] mg de etanol por 100 ml de sangre. El tercer grado empieza en [[150]] mg.",
                    ["0", "50", "100"],
                    "Grado cero: 20 a 39. Primer grado: 40 a 99. Segundo: 100 a 149. Tercero: 150 o más."),
                TrueFalse(
                    "Tomarse un café cargado después de beber permite manejar sin riesgo.",
                    false,
                    "Falso: el café puede hacerte sentir más despierto, pero el alcohol sigue en tu sangre y tus reflejos siguen afectados."),
                Classify(
                    "¿Conducta apropiada o inapropiada al volante?",
                    ["Apropiada", "Inapropiada"],
                    [
                        ("Dejar pasar a un peatón en la cebra", 0),
                        ("Parar a descansar cuando siento sueño", 0),
                        ("Usar la direccional antes de cambiar de carril", 0),
                        ("Ceder el paso a una ambulancia", 0),
                        ("Contestar mensajes en un semáforo en rojo", 1),
                        ("Pitar para que el de adelante arranque", 1),
                        ("Manejar después de \"solo dos cervezas\"", 1),
                        ("Perseguir a un conductor que me cerró", 1)
                    ],
                    "Las conductas apropiadas protegen a todos. Usar el celular, presionar con la bocina, manejar con alcohol o responder con rabia aumentan el riesgo de un siniestro."),
                Scenario(
                    "Estás en una fiesta, tomaste tres cervezas y tu carro está parqueado afuera. Un amigo te dice: «Tranquilo, estás bien, vámonos». ¿Qué haces?",
                    [
                        Choice("Manejo despacio para no llamar la atención.", "Ir despacio no elimina el alcohol de tu sangre. Arriesgas tu vida y la de otros, la inmovilización del carro y la suspensión de la licencia."),
                        Choice("Pido un taxi o un servicio de conductor elegido y recojo el carro al día siguiente.", "Es la decisión correcta: nadie sale lastimado y no arriesgas tu licencia.", true),
                        Choice("Me tomo un café y espero 15 minutos.", "Quince minutos y un café no bajan el alcohol en la sangre. Seguirías conduciendo con los reflejos afectados.")
                    ])
            ]
        ),
        (
            "Infracciones y comparendos",
            "Tipos de multa, qué hacer si te ponen un comparendo y cómo obtener descuentos.",
            10,
            [
                Text(
                    "Las multas se miden en salarios mínimos diarios",
                    "Las infracciones se agrupan en tipos según su gravedad. Cada tipo tiene una multa expresada en salarios mínimos legales diarios vigentes (SMLDV):\n\n"
                    + "A: 4 SMLDV (vehículos no automotores o de tracción animal).\nB: 8 SMLDV.\nC: 15 SMLDV.\nD: 30 SMLDV.\nE: 45 SMLDV.\n\n"
                    + "Las sanciones por alcohol y sustancias tienen sus propias reglas, más duras. Además de la multa, algunas infracciones implican "
                    + "inmovilizar el vehículo o suspender la licencia; si reincides, la sanción aumenta."),
                Text(
                    "Si te ponen un comparendo",
                    "Si aceptas la infracción tienes dos opciones con descuento, siempre que hagas un curso sobre normas de tránsito en un organismo de tránsito, "
                    + "un Centro de Enseñanza Automovilística o un Centro Integral de Atención registrado en el RUNT:\n\n"
                    + "Pagar el 50% de la multa si pagas dentro de los 5 días siguientes al comparendo.\n"
                    + "Pagar el 75% si pagas dentro de los 20 días siguientes.\n\n"
                    + "Si no pagas en esos plazos, pagas el 100% más intereses. Si no estás de acuerdo con el comparendo, puedes presentarte ante la autoridad a defenderte."),
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
                    "¿Cuánto es la multa de una infracción tipo C?",
                    null,
                    ["8 SMLDV", "15 SMLDV", "30 SMLDV", "45 SMLDV"],
                    1,
                    "Las infracciones tipo C se sancionan con 15 salarios mínimos legales diarios vigentes."),
                Scenario(
                    "Un agente te hace un comparendo por estacionar en un sitio prohibido. Sabes que cometiste la infracción. ¿Qué te conviene más?",
                    [
                        Choice("Ignorarlo y esperar a que se olvide.", "El comparendo no desaparece: después de los plazos pagarás el 100% más intereses y te pueden reportar en el SIMIT."),
                        Choice("Hacer el curso y pagar dentro de los 5 días siguientes.", "Es lo más conveniente: pagas solo el 50% de la multa y además repasas las normas.", true),
                        Choice("Pagar el 100% cuando me acuerde.", "Pagar es lo correcto, pero pierdes el descuento y pueden sumarse intereses si te demoras.")
                    ])
            ]
        )
    ];
}
