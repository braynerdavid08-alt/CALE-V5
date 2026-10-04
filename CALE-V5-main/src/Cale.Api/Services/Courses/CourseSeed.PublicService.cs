namespace Cale.Api.Services.Courses;

/// <summary>
/// "Conducción profesional de servicio público": the C1-specific competencies of the school's curriculum (C1-ES01).
/// </summary>
public sealed partial class CourseSeed
{
    private static List<(string Title, string Summary, int Minutes, object[] Blocks)> PublicServiceLessons() =>
    [
        (
            "Régimen del servicio público, documentos y seguros",
            "Qué significa prestar un servicio público de transporte, qué documentos debes llevar tú y el vehículo, y qué seguros protegen al pasajero.",
            16,
            [
                Text(
                    "Conducir para otros es una responsabilidad mayor",
                    "El transporte público es un servicio público esencial: el Estado lo regula y lo vigila porque de él dependen la vida y la movilidad de muchas personas. "
                    + "Las reglas generales están en el Estatuto Nacional de Transporte (Ley 336 de 1996) y en el Decreto Único Reglamentario del Sector Transporte (Decreto 1079 de 2015).\n\n"
                    + "Una idea clave: el servicio no lo presta el conductor por su cuenta, sino una empresa habilitada por la autoridad de transporte. "
                    + "El vehículo debe estar vinculado a esa empresa, y tú respondes ante ella, ante el pasajero y ante la autoridad."),
                Flip(
                    "Las categorías de licencia de servicio público",
                    Card("C1", "Automóviles, camperos, camionetas y microbuses de servicio público. Es la categoría para taxi."),
                    Card("C2", "Camiones rígidos, busetas y buses de servicio público."),
                    Card("C3", "Vehículos articulados de servicio público."),
                    Card("Vigencia", "Las licencias de servicio público se renuevan cada tres años, y cada año desde los 60 años de edad, con un nuevo examen de aptitud física, mental y de coordinación motriz.")),
                Text(
                    "Los documentos del servicio",
                    "Además de los documentos de cualquier vehículo (licencia de conducción, licencia de tránsito, SOAT y revisión técnico-mecánica), el servicio público exige:\n\n"
                    + "• Tarjeta de operación: la expide la autoridad de transporte y autoriza a ese vehículo a prestar el servicio con esa empresa. Tiene vigencia y se debe renovar.\n"
                    + "• Los documentos que exija la autoridad local para el conductor, como la tarjeta de control en el servicio de taxi.\n\n"
                    + "Ojo con la revisión técnico-mecánica: en el servicio público la primera se hace a los dos años de matriculado el vehículo y después cada año, no a los cinco años como en el particular."),
                Classify(
                    "¿Es un documento del vehículo o del conductor?",
                    ["Del vehículo", "Del conductor"],
                    [
                        ("Tarjeta de operación", 0),
                        ("Licencia de tránsito", 0),
                        ("SOAT", 0),
                        ("Certificado de revisión técnico-mecánica", 0),
                        ("Licencia de conducción C1", 1),
                        ("Tarjeta de control del taxista", 1)
                    ],
                    "La tarjeta de operación es del vehículo: si cambias de carro, la tarjeta no se va contigo."),
                Text(
                    "Los seguros que protegen al pasajero",
                    "El SOAT cubre la atención médica de cualquier víctima de un siniestro de tránsito. Pero en el servicio público la ley exige más: "
                    + "la empresa debe tener pólizas de responsabilidad civil contractual, que protegen a los pasajeros que transportas, "
                    + "y de responsabilidad civil extracontractual, que protegen a terceros (peatones, otros conductores) a quienes el vehículo cause daño.\n\n"
                    + "Sin estas pólizas vigentes, la empresa no puede operar y el vehículo no debe salir a prestar el servicio."),
                Pairs(
                    "Une cada seguro con a quién protege.",
                    ("SOAT", "Cualquier víctima de un siniestro, para su atención médica"),
                    ("Responsabilidad civil contractual", "Los pasajeros que van en el vehículo"),
                    ("Responsabilidad civil extracontractual", "Los terceros afectados, como peatones u otros conductores")),
                Scenario(
                    "Al iniciar tu turno notas que la tarjeta de operación del taxi venció la semana pasada. La empresa te dice que «salgas igual, que eso se arregla después». ¿Qué haces?",
                    [
                        Choice("Salgo, porque la responsabilidad es de la empresa.", "Si un agente te detiene, el vehículo puede ser inmovilizado y tú también respondes. Y si hay un siniestro, la cobertura del pasajero queda en duda."),
                        Choice("No salgo a prestar el servicio hasta que la tarjeta esté renovada.", "Correcto: sin tarjeta de operación vigente, el vehículo no está autorizado para prestar el servicio.", true),
                        Choice("Salgo, pero solo recojo pasajeros conocidos.", "La falta de tarjeta de operación no depende de a quién lleves: el vehículo no está habilitado.")
                    ]),
                TrueFalse(
                    "Un taxi hace la primera revisión técnico-mecánica a los cinco años de matriculado, igual que un carro particular.",
                    false,
                    "Falso: los vehículos de servicio público hacen la primera revisión a los dos años de matriculados y después cada año."),
                Quiz(
                    "¿Quién presta legalmente el servicio público de transporte?",
                    null,
                    ["El conductor, con su licencia C1", "El dueño del vehículo, con la licencia de tránsito", "Una empresa habilitada, con vehículos vinculados", "Cualquier persona con un vehículo en buen estado"],
                    2,
                    "La empresa habilitada responde por el servicio. El vehículo debe estar vinculado a ella y tener tarjeta de operación.")
            ]
        ),
        (
            "Atención al usuario y resolución de conflictos",
            "Cómo tratar al pasajero, qué derechos tiene, y cómo manejar una discusión sin poner en riesgo la seguridad del viaje.",
            14,
            [
                Text(
                    "El pasajero confía su vida en ti",
                    "Quien se sube a tu vehículo no eligió cómo vas a conducir: confía en que lo vas a llevar seguro. Por eso la atención al usuario empieza por la conducción: "
                    + "sin frenazos, sin aceleraciones bruscas, sin usar el celular y respetando los límites de velocidad, aunque el pasajero tenga prisa.\n\n"
                    + "Después vienen el trato y la presentación: saludar, confirmar el destino, mantener el vehículo limpio y explicar con claridad la tarifa o la ruta."),
                Flip(
                    "Lo que el pasajero tiene derecho a esperar",
                    Card("Seguridad", "Un vehículo en buen estado y un conductor que respete las normas de tránsito."),
                    Card("Trato digno", "Sin groserías, sin discriminación y sin comentarios sobre su apariencia, su origen o su condición."),
                    Card("Información clara", "La tarifa, el recorrido y cualquier cambio de ruta, antes de hacerlo."),
                    Card("Prestación del servicio", "Negarse a prestar el servicio sin una causa justificada es una conducta sancionable."),
                    Card("Objetos olvidados", "Lo que el pasajero deje en el vehículo se reporta a la empresa para su devolución.")),
                Classify(
                    "¿Esta conducta es una buena atención al usuario o una mala práctica?",
                    ["Buena atención", "Mala práctica"],
                    [
                        ("Confirmar el destino antes de arrancar", 0),
                        ("Avisar si vas a tomar una ruta distinta por un cierre", 0),
                        ("Ayudar con el equipaje sin dejar el vehículo mal estacionado", 0),
                        ("Negarte a llevar a alguien porque el trayecto es corto", 1),
                        ("Contestar el celular mientras conduces para no perder un servicio", 1),
                        ("Acelerar porque el pasajero dice que va tarde", 1),
                        ("Poner la música a todo volumen sin preguntar", 1)
                    ],
                    "La prisa del pasajero no cambia las reglas: llegar tarde es mejor que no llegar."),
                Text(
                    "Cuando aparece el conflicto",
                    "Un pasajero molesto, otro conductor que te cierra, una discusión por la tarifa: los conflictos van a ocurrir. Lo que no puede ocurrir es que te hagan perder el control del vehículo.\n\n"
                    + "Tres reglas: no respondas en caliente (respira y baja el tono), no discutas mientras conduces (si hace falta, detente en un lugar seguro) "
                    + "y no persigas ni enfrentes a nadie en la vía. Si hay riesgo para tu seguridad o la del pasajero, busca un lugar concurrido y pide apoyo a la empresa o a la Policía."),
                Order(
                    "Un pasajero empieza a gritarte porque cree que estás dando vueltas para cobrarle más. Ordena cómo actúas.",
                    [
                        "Mantengo la calma y sigo conduciendo con seguridad",
                        "Le explico con tono tranquilo la ruta que tomé y por qué",
                        "Si la discusión sigue, me detengo en un lugar seguro y permitido",
                        "Le ofrezco terminar el servicio ahí o seguir por la ruta que él prefiera",
                        "Reporto la situación a la empresa"
                    ],
                    "Detenerte en un lugar seguro te permite atender la discusión sin dividir tu atención entre el pasajero y la vía."),
                Scenario(
                    "Un conductor particular te cierra bruscamente y te insulta. Llevas un pasajero. ¿Qué haces?",
                    [
                        Choice("Lo sigo para reclamarle en el siguiente semáforo.", "Perseguir a otro conductor pone en riesgo a tu pasajero y puede terminar en un enfrentamiento violento."),
                        Choice("Le pito y le respondo con un gesto.", "Responder la agresión la hace crecer. Y tu pasajero no tiene por qué vivir esa situación."),
                        Choice("Aumento la distancia, lo dejo ir y sigo mi recorrido.", "Correcto: tu responsabilidad es el pasajero. Dejar ir al otro conductor es la decisión más segura.", true)
                    ]),
                TrueFalse(
                    "Si el pasajero tiene prisa, es parte de un buen servicio exceder un poco el límite de velocidad.",
                    false,
                    "Falso: un buen servicio es un servicio seguro. Exceder el límite pone en riesgo al pasajero, y la responsabilidad es tuya."),
                Quiz(
                    "¿Qué es lo primero que debes hacer cuando una discusión con un pasajero se pone tensa mientras conduces?",
                    null,
                    ["Responderle con firmeza para que no se repita", "Mantener la calma y la atención en la vía", "Subir el volumen de la música", "Bajarlo inmediatamente donde estés"],
                    1,
                    "Mientras conduces, la prioridad es la vía. La discusión se puede atender después, detenido en un lugar seguro.")
            ]
        ),
        (
            "Pasajeros con discapacidad, usuarios vulnerables y ascenso seguro",
            "Cómo atender a personas con discapacidad, adultos mayores, niños y mujeres embarazadas, y cómo hacer que el ascenso y el descenso sean seguros.",
            15,
            [
                Text(
                    "Un servicio para todas las personas",
                    "En Colombia, la Ley 1618 de 2013 garantiza a las personas con discapacidad el acceso al transporte en igualdad de condiciones. "
                    + "Para el conductor de servicio público eso significa: no negar el servicio, dar el tiempo que la persona necesita para subir y bajar, "
                    + "y ayudar solo cuando la persona lo acepte y de la forma en que ella lo indique.\n\n"
                    + "Las personas con discapacidad visual pueden viajar con su perro guía. Negarles el servicio por el perro es discriminación."),
                Flip(
                    "Cómo apoyar a cada pasajero",
                    Card("Persona con discapacidad visual", "Preséntate, dile dónde está la puerta y la manija, y avísale cuando llegue al destino y de qué lado debe bajar. No la tomes del brazo sin preguntar."),
                    Card("Persona usuaria de silla de ruedas", "Pregúntale cómo prefiere subir. Guarda la silla con cuidado en el baúl y entrégasela armada al llegar."),
                    Card("Persona con discapacidad auditiva", "Mírala de frente al hablarle, habla despacio y, si hace falta, escribe el valor o la dirección."),
                    Card("Adulto mayor", "Espera a que esté sentado y con el cinturón puesto antes de arrancar. Arranca y frena con más suavidad."),
                    Card("Mujer embarazada", "El cinturón va por debajo del abdomen, sobre la cadera, y la banda diagonal entre los senos."),
                    Card("Niños", "Los menores de 10 años no viajan en el asiento delantero. Los más pequeños deben ir con un sistema de retención adecuado.")),
                Text(
                    "El ascenso y el descenso: el momento de mayor riesgo",
                    "Muchos siniestros con pasajeros no ocurren durante el viaje, sino al subir o al bajar: una puerta que golpea a un ciclista, "
                    + "un pasajero que se baja hacia el lado de la vía, un vehículo que arranca antes de que la persona termine de bajar.\n\n"
                    + "Detente solo en sitios permitidos, junto al andén y nunca en doble fila. Pide que se suba y se baje por el lado del andén, "
                    + "revisa el espejo antes de que se abra la puerta y no arranques hasta confirmar que la persona ya está segura en la acera."),
                Order(
                    "Ordena los pasos para un descenso seguro del pasajero.",
                    [
                        "Busco un sitio permitido para detenerme junto al andén",
                        "Pongo la direccional, me detengo y enciendo las luces de parqueo",
                        "Le indico al pasajero que baje por el lado del andén",
                        "Reviso el espejo por si vienen motos o bicicletas antes de que abra la puerta",
                        "Espero a que esté en la acera y la puerta cerrada antes de arrancar"
                    ],
                    "Revisar el espejo antes de que se abra la puerta evita el «puertazo», uno de los golpes más graves para motociclistas y ciclistas."),
                Classify(
                    "¿Este lugar es adecuado para recoger o dejar un pasajero?",
                    ["Adecuado", "No adecuado"],
                    [
                        ("Junto al andén, en una zona sin prohibición de detenerse", 0),
                        ("Una bahía de taxis señalizada", 0),
                        ("En doble fila, frente a un centro comercial", 1),
                        ("Sobre un paso peatonal", 1),
                        ("En medio de una intersección", 1),
                        ("En el carril exclusivo de Transmetro", 1)
                    ],
                    "Detenerse donde no se puede no solo es una infracción: obliga al pasajero a bajar entre vehículos en movimiento."),
                Scenario(
                    "Recoges a una señora mayor con bastón que va a una cita médica. Vas atrasado para tu siguiente servicio. ¿Qué haces al llegar?",
                    [
                        Choice("Me detengo cerca y le pido que se baje rápido porque estoy en doble fila.", "En doble fila, la señora tendría que bajar entre carros en movimiento, y con prisa aumenta el riesgo de caída."),
                        Choice("Busco un sitio junto al andén, le doy el tiempo que necesite y espero a que esté en la acera.", "Correcto: el tiempo del pasajero vulnerable es parte del servicio. Tu atraso no puede convertirse en su riesgo.", true),
                        Choice("La dejo en la esquina, a una cuadra, donde es más fácil detenerse.", "Una cuadra puede ser un trayecto difícil para alguien con bastón. Si hay un sitio permitido más cerca, úsalo.")
                    ]),
                TrueFalse(
                    "Un conductor de taxi puede negarse a llevar a una persona con discapacidad visual porque viaja con su perro guía.",
                    false,
                    "Falso: el perro guía es un apoyo para la persona y puede viajar con ella. Negarle el servicio es discriminación."),
                Quiz(
                    "¿Por qué lado debe bajar el pasajero?",
                    null,
                    ["Por el que le quede más cómodo", "Por el lado del andén", "Por el lado de la vía, para salir más rápido", "Por el lado del conductor"],
                    1,
                    "Bajar por el lado del andén evita que el pasajero quede entre vehículos en movimiento y que la puerta golpee a una moto o una bicicleta.")
            ]
        ),
        (
            "Fatiga, somnolencia y presión por tiempos",
            "Por qué las jornadas largas y la presión por cumplir tiempos causan siniestros, cómo reconocer el cansancio y qué hacer.",
            14,
            [
                Text(
                    "Conducir cansado es como conducir bajo efectos del alcohol",
                    "Quien conduce profesionalmente pasa muchas horas al volante, a menudo con calor, tráfico y turnos nocturnos. Con el cansancio, el tiempo de reacción aumenta, "
                    + "la atención se reduce y aparecen los microsueños: segundos en los que el cerebro se «apaga» sin que lo notes. A 60 km/h, un microsueño de tres segundos son 50 metros recorridos sin control.\n\n"
                    + "Después de muchas horas sin dormir, el efecto en la conducción es comparable al de conducir con alcohol en la sangre."),
                Flip(
                    "Señales de que necesitas parar",
                    Card("Bostezos y parpadeo", "Bostezas seguido, te pesan los párpados o te cuesta enfocar la vista."),
                    Card("Desvíos del carril", "Te sales del carril sin darte cuenta o pasas por encima de las líneas."),
                    Card("Lagunas", "No recuerdas los últimos kilómetros o te pasas de una dirección conocida."),
                    Card("Irritabilidad", "Todo te molesta: el pasajero, el tráfico, los demás conductores."),
                    Card("Cabeceos", "La cabeza se te va hacia adelante. Es la última señal: detente de inmediato.")),
                Classify(
                    "¿Esta medida realmente te ayuda contra la fatiga o es un mito?",
                    ["Ayuda", "Es un mito"],
                    [
                        ("Dormir bien antes del turno", 0),
                        ("Hacer pausas activas durante la jornada", 0),
                        ("Detenerte a dormir 15 o 20 minutos en un lugar seguro", 0),
                        ("Comer liviano e hidratarte", 0),
                        ("Tomar varios tintos para aguantar el turno", 1),
                        ("Abrir la ventana y subir el volumen de la música", 1),
                        ("Tomar bebidas energizantes durante toda la jornada", 1)
                    ],
                    "La cafeína, el aire y la música solo disimulan el cansancio por unos minutos. Lo único que lo quita es descansar."),
                Text(
                    "La presión por tiempos",
                    "Cuando el ingreso depende del número de pasajeros o de vueltas, aparece la tentación de correr: la llamada «guerra del centavo». "
                    + "Es una de las causas de siniestros en el transporte público: adelantamientos peligrosos, frenadas para recoger pasajeros donde no se puede, semáforos en rojo.\n\n"
                    + "Respeta las jornadas y los descansos que fijan la ley y la empresa, y recuerda que ningún servicio vale más que una vida. "
                    + "Si la empresa te presiona para conducir sin descanso, tienes derecho a negarte y a reportarlo a la autoridad de transporte."),
                Scenario(
                    "Llevas 10 horas de turno y empiezas a cabecear. Te sale un servicio largo y bien pagado al otro lado de la ciudad. ¿Qué haces?",
                    [
                        Choice("Lo tomo y me compro un café y una bebida energizante.", "La cafeína solo disimula el sueño por un rato. El riesgo de un microsueño en un trayecto largo sigue ahí, y llevas un pasajero."),
                        Choice("Lo rechazo, me detengo en un lugar seguro y descanso antes de seguir o termino el turno.", "Correcto: cabecear es la señal de que el cuerpo ya no da más. Ningún servicio vale ese riesgo.", true),
                        Choice("Lo tomo y abro todas las ventanas para mantenerme despierto.", "El aire frío despierta solo por unos minutos. Tu pasajero viajaría con un conductor a punto de dormirse.")
                    ]),
                FillBlank(
                    "Un [[microsueño]] es un lapso de pocos segundos en el que el cerebro se desconecta. Lo único que quita el cansancio es [[descansar|dormir]].",
                    ["tomar café", "acelerar", "parpadeo"],
                    "Los microsueños ocurren sin aviso y son más frecuentes en vías monótonas, de noche y después de muchas horas de conducción."),
                TrueFalse(
                    "Si llevas pasajeros a bordo, conducir cansado es menos peligroso porque la conversación te mantiene despierto.",
                    false,
                    "Falso: la conversación no evita los microsueños y, si te duermes, pones en riesgo también la vida del pasajero."),
                Quiz(
                    "¿Cuál es la señal de fatiga más grave, que exige detenerse de inmediato?",
                    null,
                    ["Tener sed", "Cabecear", "Sentir calor", "Tener hambre"],
                    1,
                    "Cabecear significa que ya estás teniendo microsueños. Detente en el primer lugar seguro.")
            ]
        ),
        (
            "Conducción urbana intensiva y rutas en Barranquilla",
            "Cómo conducir muchas horas en el tráfico de la ciudad, planear rutas, convivir con Transmetro y adaptarte a cierres, eventos y lluvias.",
            14,
            [
                Text(
                    "Muchas horas en la ciudad",
                    "La conducción urbana intensiva es exigente: cientos de detenciones, peatones que cruzan por cualquier parte, motos a ambos lados, paraderos y obras. "
                    + "Quien conduce todo el día tiende a volverse confiado y a normalizar conductas de riesgo.\n\n"
                    + "Tu ventaja como profesional es la anticipación: conoces los puntos conflictivos de la ciudad y puedes prepararte antes de llegar a ellos."),
                Text(
                    "Conocer la ciudad no es memorizar",
                    "Barranquilla se organiza en calles y carreras numeradas. Conocer sus ejes principales te permite planear rutas y encontrar alternativas cuando una vía se cierra: "
                    + "la Vía 40, la Avenida Circunvalar, la Calle 30 hacia el aeropuerto, la Avenida Murillo (Calle 45), la Carrera 46 (Olaya Herrera) y la Calle 84, entre otras.\n\n"
                    + "Pero las rutas cambian por obras, eventos, cierres y medidas temporales. Las aplicaciones de navegación ayudan, pero la señalización vigente y las órdenes de los agentes mandan sobre cualquier aplicación."),
                Pairs(
                    "Une cada situación con la mejor decisión de ruta.",
                    ("Partido en el estadio Metropolitano", "Evitar los alrededores y avisar al pasajero de la demora"),
                    ("Desfiles del Carnaval", "Consultar los cierres anunciados y planear una ruta alterna"),
                    ("Aguacero fuerte", "Evitar las calles con arroyos y esperar a que pase si no hay ruta segura"),
                    ("Obra con desvío señalizado", "Seguir el desvío aunque la aplicación indique otra ruta")),
                Text(
                    "Convivir con Transmetro y el transporte público",
                    "Transmetro circula por carriles exclusivos en sus troncales, como la Avenida Murillo y la Olaya Herrera. Esos carriles no se usan para adelantar, recoger pasajeros ni esperar. "
                    + "En el resto de la ciudad, los buses alimentadores y urbanos se detienen en paraderos: anticipa sus paradas y no los adelantes por la derecha cuando están detenidos, "
                    + "porque de ahí bajan pasajeros.\n\n"
                    + "El Distrito anunció en agosto de 2026 la llegada de 30 buses nuevos para mejorar las frecuencias en las rutas de mayor demanda: más buses también significa más paradas que anticipar."),
                Classify(
                    "¿Esta conducta en el tráfico urbano es segura o riesgosa?",
                    ["Segura", "Riesgosa"],
                    [
                        ("Reducir la velocidad al acercarte a un paradero con bus detenido", 0),
                        ("Planear la ruta antes de empezar el servicio", 0),
                        ("Seguir el desvío señalizado por una obra", 0),
                        ("Usar el carril de Transmetro para adelantar en un trancón", 1),
                        ("Detenerte de golpe porque alguien te hace señas desde el andén", 1),
                        ("Adelantar por la derecha a un bus detenido en el paradero", 1),
                        ("Cruzar un arroyo porque el pasajero tiene prisa", 1)
                    ],
                    "Detenerte de golpe para recoger un pasajero sorprende al de atrás. Pon la direccional, mira el espejo y busca un sitio permitido."),
                Scenario(
                    "Llevas a un pasajero al aeropuerto y la aplicación te indica una ruta por una calle que un agente de tránsito está cerrando por un evento. ¿Qué haces?",
                    [
                        Choice("Sigo la aplicación y le explico al agente que voy al aeropuerto.", "La orden del agente está por encima de cualquier aplicación. Insistir te hace perder tiempo y puede generar una infracción."),
                        Choice("Sigo la indicación del agente, tomo una ruta alterna por los ejes principales y le explico al pasajero.", "Correcto: conocer los ejes de la ciudad te permite adaptarte, y el pasajero agradece que le expliques.", true),
                        Choice("Me devuelvo en reversa para tomar la calle anterior.", "Retroceder en una vía con tráfico es peligroso. Busca la siguiente salida.")
                    ]),
                TrueFalse(
                    "Los carriles exclusivos de Transmetro se pueden usar para recoger un pasajero si la parada es rápida.",
                    false,
                    "Falso: los carriles exclusivos son solo para los vehículos autorizados. Detenerte ahí bloquea el sistema y es una infracción."),
                Quiz(
                    "Si la aplicación de navegación y la señalización de la vía indican cosas distintas, ¿qué debes seguir?",
                    null,
                    ["La aplicación, porque está actualizada", "La señalización vigente y las órdenes del agente", "Lo que diga el pasajero", "La ruta que siempre tomas"],
                    1,
                    "La señalización y las órdenes de los agentes son las que valen legalmente. La aplicación puede no conocer un cierre temporal.")
            ]
        )
    ];
}
