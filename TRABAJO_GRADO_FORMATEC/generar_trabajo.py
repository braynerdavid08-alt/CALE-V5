# -*- coding: utf-8 -*-
"""Genera el trabajo guiado FORMATEC (docx y pdf) a partir de evidencias revisadas."""
from pathlib import Path

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor, Twips
from fpdf import FPDF

OUT = Path(r"D:\APPS programacion\CALE-V5-main\TRABAJO_GRADO_FORMATEC")
FONT = r"C:\Windows\Fonts\times.ttf"
FONTB = r"C:\Windows\Fonts\timesbd.ttf"
FONTI = r"C:\Windows\Fonts\timesi.ttf"
FONTBI = r"C:\Windows\Fonts\timesbi.ttf"

COURSES = [
    ("Señales de tránsito", [
        ("Para qué sirven las señales", 12),
        ("Señales reglamentarias", 15),
        ("Señales preventivas", 15),
        ("Señales informativas", 12),
        ("Repaso final", 10),
    ]),
    ("Normas de tránsito básicas", [
        ("Autorregulación y responsabilidad", 10),
        ("Autoridades de tránsito y el Código", 20),
        ("Documentos, habilitación y restricciones para circular", 15),
        ("Velocidad segura y adaptación al entorno", 17),
        ("Prelación: quién pasa primero", 12),
        ("Adelantar, carriles y estacionamiento", 14),
        ("Factores humanos, alcohol y sustancias", 80),
        ("Dimensiones, pesos y elementos de seguridad de la carga", 12),
        ("Infracciones y comparendos", 10),
    ]),
    ("Señalización vial e infraestructura", [
        ("Líneas en el centro y en los bordes", 12),
        ("Marcas transversales, símbolos y dispositivos", 10),
        ("Fases del semáforo vehicular y peatonal", 12),
        ("Repaso final", 8),
    ]),
    ("Primeros auxilios en la vía", [
        ("El primer respondiente", 16),
        ("Proteger la escena y avisar", 10),
        ("Valorar a la víctima", 12),
        ("Heridas y hemorragias", 10),
        ("Quemaduras", 10),
        ("Atragantamiento: maniobra de Heimlich", 10),
        ("Actuación ante derrames o peligros en la vía", 12),
    ]),
    ("Movilidad segura y sostenible", [
        ("Seguridad vial y Sistema Seguro", 24),
        ("Víctimas y consecuencias de los siniestros", 22),
        ("Usuarios vulnerables, prioridad y convivencia", 15),
        ("Conducción preventiva y gestión del riesgo", 22),
        ("Visibilidad y clima: noche, lluvia y luces", 16),
        ("Movilidad sostenible y conducción eficiente", 20),
    ]),
    ("La vía y el espacio público", [
        ("La vía, su función y sus riesgos", 14),
        ("Posición en el carril e incorporaciones", 13),
        ("Cicloinfraestructura y convivencia con ciclistas", 13),
        ("Espacio público y conflictos de movilidad", 12),
        ("Puentes, túneles y cunetas", 12),
    ]),
    ("El vehículo", [
        ("Reconocimiento y funcionamiento del vehículo", 16),
        ("Inspección preoperacional", 15),
        ("Seguridad activa, pasiva y asistencias", 17),
        ("Equipo de prevención y protección de la escena", 12),
        ("Averías frecuentes e inmovilización segura", 20),
    ]),
    ("Conducción segura en motocicleta (A2)", [
        ("Antes de salir: elementos de protección y alistamiento", 20),
        ("Revisión preoperacional de la motocicleta", 20),
        ("Técnicas de manejo: frenado y curvas", 16),
        ("Posición en la vía, puntos ciegos y tráfico urbano", 16),
        ("Lluvia, calor y fatiga", 14),
        ("Acompañante, carga y fin del recorrido", 20),
    ]),
    ("Dominio seguro del automóvil (B1)", [
        ("Puesto de conducción y mandos", 14),
        ("Embrague, cambios, arranque y detención", 16),
        ("Frenado y pendientes", 15),
        ("Reversa y estacionamiento", 16),
        ("Giros e intersecciones con el automóvil", 14),
    ]),
    ("Conducción profesional de servicio público (C1)", [
        ("Régimen del servicio público, documentos y seguros", 16),
        ("Atención al usuario y resolución de conflictos", 14),
        ("Pasajeros con discapacidad, usuarios vulnerables y ascenso seguro", 15),
        ("Fatiga, somnolencia y presión por tiempos", 14),
        ("Conducción urbana intensiva y rutas en Barranquilla", 14),
    ]),
]

TITLE = (
    "Diseño, estructuración y aplicación de estrategias pedagógicas para la "
    "formación en seguridad vial en el Centro de Enseñanza Automovilística "
    "CEA VIP del Norte (Luz Verde)"
)


def course_rows():
    rows = []
    lessons = minutes = 0
    for name, items in COURSES:
        m = sum(x[1] for x in items)
        lessons += len(items)
        minutes += m
        rows.append((name, str(len(items)), str(m)))
    rows.append(("Total", str(lessons), str(minutes)))
    return rows, lessons, minutes


def build():
    b = []

    def add(kind, *rest):
        b.append((kind, *rest))

    def P(text):
        add("p", text)

    def H1(text):
        add("h1", text)

    def H2(text):
        add("h2", text)

    def LI(text):
        add("li", text)

    add("cover_h", "CENTRO DE CAPACITACIÓN Y FORMACIÓN TÉCNICA FORMATEC")
    add("cover", "Programa Técnico Laboral por Competencias en Tránsito, Transporte y Seguridad Vial")
    add("cover", "Trabajo guiado alternativo a la práctica de grado")
    add("cover_gap", "")
    add("cover_title", TITLE)
    add("cover_gap", "")
    add("cover", "Nombre del estudiante: Braynner David Rodríguez Obredor")
    add("cover", "Número de documento: 1.143.460.787")
    add("cover", "Nombre del proyecto: " + TITLE)
    add("cover", "Fecha de entrega: 24 de noviembre de 2026")
    add("cover", "Municipio: Soledad, Atlántico")
    add("cover", "Año: 2026")
    add("pb", "")

    H1("1. Introducción")
    P(
        "La formación de un conductor empieza antes de la primera salida a la vía. Quien aspira "
        "a una licencia necesita reconocer la norma, leer la señal, anticipar el riesgo y entender "
        "que un error de pocos segundos puede lesionar a otra persona. La Ley 2251 de 2022 incorporó "
        "en la política de seguridad vial de Colombia el enfoque de Sistema Seguro: se acepta que las "
        "personas se equivocan, y se pide que la conducta, el vehículo, la vía, la velocidad y la "
        "atención posterior al siniestro reduzcan la posibilidad de que ese error termine en una muerte "
        "o en una lesión grave (Congreso de la República de Colombia, 2022)."
    )
    P(
        "En ese marco trabaja un Centro de Enseñanza Automovilística. Organiza la formación teórica, "
        "la formación básica aplicada y la formación específica de quienes van a conducir. Este trabajo "
        "recoge la experiencia realizada en el Centro de Enseñanza Automovilística CEA VIP del Norte. "
        "Dentro del proyecto y del entorno de trabajo, el componente académico y las herramientas de "
        "estudio se conocen como Luz Verde."
    )
    P(
        "La certificación expedida por el centro el 19 de noviembre de 2026 deja constancia de mi "
        "vinculación durante un año y seis meses, con la denominación de instructor teórico y "
        "coordinador de formación en seguridad vial. El mismo documento registra, además, "
        "aproximadamente siete años de experiencia general como instructor de conducción en el "
        "componente teórico (Centro de Enseñanza Automovilística CEA VIP del Norte [CEA VIP del Norte], 2026a). "
        "Esas dos cifras se usan tal como aparecen certificadas. La primera corresponde al tiempo en "
        "esta institución. La segunda corresponde a la experiencia general en la enseñanza teórica, "
        "no a un conteo de clases que este trabajo haya medido."
    )
    P(
        "El texto se presenta como trabajo guiado alternativo a la práctica de grado del Técnico "
        "Laboral por Competencias en Tránsito, Transporte y Seguridad Vial, conforme a la Circular "
        "académica 005-2026 y al formato institucional de trabajo guiado (Centro de Capacitación y "
        "Formación Técnica FORMATEC [FORMATEC], 2026, 2022). La presentación sigue las normas APA, "
        "séptima edición, con letra Times New Roman de 12 puntos, interlineado 2,0 y márgenes de "
        "2,54 cm, como exige la circular. La estructura de apartados es la del formato: de la "
        "introducción a las referencias, con el marco jurídico y las recomendaciones que ese formato "
        "pide. El propósito es mostrar cómo se aplicaron, en un centro de enseñanza real, conocimientos "
        "de legislación, señalización, comportamiento vial, conducción preventiva y planificación de "
        "la seguridad vial."
    )

    H1("2. Planteamiento del problema")
    P(
        "Un aspirante puede memorizar el nombre de una señal y, aun así, dudar cuando esa señal "
        "aparece en un cruce con peatones, con lluvia o con un motociclista en el punto ciego. La "
        "formación teórica de un CEA tiene que cerrar ese vacío. La Resolución 20223040009425 de 2022, "
        "compilada en la Resolución 20223040045295 de 2022, organiza los contenidos en cuatro núcleos: "
        "movilidad segura y sostenible, normas de tránsito, señalización vial e infraestructura, y el "
        "vehículo, con lo propio de cada categoría de licencia. Pide además una malla que favorezca el "
        "aprendizaje significativo y el uso de recursos de la Agencia Nacional de Seguridad Vial "
        "(Ministerio de Transporte, 2022a, 2022b)."
    )
    P(
        "El problema que aborda este trabajo es formativo. En la operación cotidiana de una escuela, "
        "los temas de legislación, señales, conducta y riesgo se dispersan con facilidad. Una sesión "
        "explica el comparendo, otra muestra láminas y otra recomienda conducir con cuidado, sin una "
        "secuencia que una la norma con la decisión en la vía. Cuando eso ocurre, el aspirante acumula "
        "datos y puede salir sin una manera clara de reconocer una situación peligrosa, ni de entender "
        "su responsabilidad con el peatón, el ciclista o el motociclista."
    )
    P(
        "En el CEA VIP del Norte esa necesidad se atendió desde la coordinación del componente teórico. "
        "Había que ordenar módulos y contenidos, proponer una secuencia y disponer de actividades en "
        "las que el aspirante eligiera una conducta, además de comprender una definición. La certificación "
        "laboral describe esas funciones de planeación de clases, estructuración del pensum y propuesta "
        "de estrategias (CEA VIP del Norte, 2026a). Este documento las analiza con los materiales que "
        "quedaron organizados en Luz Verde. El planteamiento se mantiene en el terreno de la formación. "
        "Se apoya en la norma que rige a los CEA y en la experiencia certificada."
    )
    P(
        "A esa tarea se suma la planificación institucional de la seguridad vial. La Ley 1503 de 2011, "
        "en el artículo 12 modificado por el Decreto Ley 2106 de 2019, obliga a determinadas "
        "organizaciones a diseñar e implementar un Plan Estratégico de Seguridad Vial y a articularlo "
        "con el Sistema de Gestión de la Seguridad y Salud en el Trabajo (Congreso de la República de "
        "Colombia, 2011; Presidencia de la República de Colombia, 2019). La certificación incluye, entre "
        "mis funciones, el diseño y la estructuración del PESV del centro y la identificación de "
        "factores de riesgo (CEA VIP del Norte, 2026a). El archivo de trabajo conserva una matriz de "
        "peligros propuesta para el cargo de instructor de motocicleta. La propia hoja advierte que la "
        "valoración debe validarse con las condiciones reales de la empresa (CEA VIP del Norte, 2026b). "
        "Parte del problema consiste, entonces, en dejar escrito qué pieza de esa planificación está "
        "documentada y en qué condición queda: como propuesta de diagnóstico del cargo, pendiente de "
        "contraste con la operación."
    )

    H1("3. Justificación")
    P(
        "El trabajo importa, primero, para la formación de quien lo escribe. El programa técnico pide "
        "aplicar en un contexto real la educación vial, la lectura de la norma, la identificación del "
        "riesgo y la organización de acciones preventivas. Esas tareas se ejercieron en un CEA, con "
        "aspirantes a conductores y con la responsabilidad de ordenar el componente teórico. "
        "Documentarlas permite mostrar la competencia en actos concretos: una secuencia de cursos, unas "
        "actividades de decisión y una matriz de peligros con su nota de validación."
    )
    P(
        "También importa para la formación de conductores. Una clase teórica ordenada ayuda a que la "
        "persona reconozca la prelación, la velocidad que corresponde al entorno, el estado del vehículo "
        "antes de salir y el cuidado con quien va a pie, en bicicleta o en moto. Esos contenidos están "
        "en la malla de Luz Verde y coinciden con los núcleos que el Ministerio de Transporte exige a "
        "los CEA (Ministerio de Transporte, 2022a; Rodríguez Obredor, 2026). El beneficio se presenta "
        "como la disponibilidad de una ruta de estudio y de actividades para decidir ante un caso. En "
        "el archivo revisado hay diseño, estructura y la función certificada de orientar clases."
    )
    P(
        "Desde la prevención, el trabajo deja por escrito los peligros identificados para el cargo de "
        "instructor de motocicleta: el evento de tránsito en vía pública, la postura prolongada, el "
        "ruido y la vibración, el calor y la carga de una jornada larga. Esa identificación sigue la "
        "GTC 45, que es el método declarado en el instructivo de la matriz (ICONTEC, 2010). Las medidas "
        "propuestas —capacitación, inspección del vehículo antes de la clase y pausas— pertenecen al "
        "lenguaje de la seguridad vial y de la salud en el trabajo. Dejar la matriz analizada, con la "
        "advertencia de validación que ella misma trae, evita que una propuesta se archive como si ya "
        "fuera el diagnóstico oficial de la empresa (CEA VIP del Norte, 2026b)."
    )
    P(
        "Para el programa de FORMATEC, el documento evidencia la aplicación del perfil que la "
        "certificación declara afín: educar actores viales, interpretar normas, identificar riesgos, "
        "prevenir siniestros, promover la movilidad segura y participar en la planificación de "
        "estrategias de seguridad vial (CEA VIP del Norte, 2026a; FORMATEC, 2026)."
    )

    H1("4. Objetivos generales")
    P(
        "El formato de trabajo guiado titula este apartado en plural. La Circular 005-2026 pide un "
        "objetivo general. Se formula uno solo, coherente con el título y con el desarrollo que sigue."
    )
    P(
        "Analizar el diseño, la estructuración y la aplicación de las estrategias pedagógicas del "
        "componente teórico de seguridad vial en el Centro de Enseñanza Automovilística CEA VIP del "
        "Norte (Luz Verde), y su relación con las competencias del Técnico Laboral por Competencias "
        "en Tránsito, Transporte y Seguridad Vial."
    )
    P(
        "Este objetivo se desarrolla en el propio documento. El análisis recorre las actividades "
        "certificadas —clases teóricas, organización del pensum, estrategias de enseñanza e "
        "identificación de riesgos— y las contrasta con los materiales de Luz Verde: diez cursos, "
        "cincuenta y siete lecciones y las actividades de decisión, repaso y video que las acompañan "
        "(Rodríguez Obredor, 2026). El alcance es descriptivo. Se expone lo que fue diseñado, lo que "
        "la certificación registra como función ejercida y lo que el archivo permite comprobar."
    )
    P(
        "El título y el objetivo se corresponden. Diseño y estructuración aluden al pensum, a la "
        "secuencia de núcleos y a la matriz de peligros. Aplicación alude a la orientación de clases "
        "y al uso de esas estrategias con aspirantes, en los términos en que el CEA lo certifica. "
        "Luz Verde nombra el entorno en el que ese componente académico quedó organizado."
    )

    H1("5. Objetivos específicos")
    P(
        "Identificar las actividades de formación, de organización de contenidos y de planificación "
        "en seguridad vial desarrolladas en el CEA VIP del Norte, ciñéndose a la certificación laboral "
        "y a los materiales disponibles. Este objetivo se cumple en la contextualización, en el "
        "apartado de formación vial y en el de evidencias. Cada actividad nombrada tiene un soporte: "
        "la certificación, la malla de cursos o la matriz de peligros."
    )
    P(
        "Analizar la secuencia de contenidos y las estrategias pedagógicas usadas para enseñar "
        "legislación, señalización, comportamiento vial, conducción preventiva y reconocimiento del "
        "riesgo. El desarrollo describe el orden de los cursos y el tipo de actividades: explicación, "
        "pregunta, ordenamiento, clasificación y escenario. También describe el apoyo de los videos "
        "de la Agencia Nacional de Seguridad Vial y de las clases grabadas incorporadas a las lecciones."
    )
    P(
        "Relacionar las actividades realizadas con las competencias del programa técnico y con los "
        "núcleos de formación exigidos a los Centros de Enseñanza Automovilística. La relación se "
        "apoya en la Resolución 20223040009425 de 2022 y en las funciones que la certificación declara "
        "afines al programa (Ministerio de Transporte, 2022a; CEA VIP del Norte, 2026a)."
    )
    P(
        "Describir el alcance verificable del trabajo sobre el Plan Estratégico de Seguridad Vial, "
        "a partir de la matriz de peligros del cargo de instructor de motocicleta y de la nota de "
        "validación que esa matriz contiene (CEA VIP del Norte, 2026b). Este objetivo fija el límite "
        "del apartado correspondiente: se analiza la propuesta documentada, con los valores que fueron "
        "diligenciados, y se deja expresa la validación pendiente."
    )

    H1("6. Marco jurídico")
    P(
        "La formación que se analiza ocurre dentro de un marco ya escrito. El Código Nacional de "
        "Tránsito Terrestre, Ley 769 de 2002, regula la circulación y el comportamiento de conductores, "
        "pasajeros y peatones. Su artículo 55 exige que todo actor de la vía se comporte de modo que "
        "no obstaculice, perjudique ni ponga en riesgo a los demás (Congreso de la República de "
        "Colombia, 2002). De esa ley salen, para la clase teórica, los temas de documentos para "
        "circular, velocidad, prelación, maniobras, infracciones y deber de cuidado. El Decreto 1079 "
        "de 2015 compila la reglamentación del sector transporte y es una de las normas con las que "
        "la certificación relaciona las actividades realizadas (Ministerio de Transporte, 2015; "
        "CEA VIP del Norte, 2026a)."
    )
    P(
        "La Ley 1503 de 2011 orienta la formación de hábitos y conductas seguras en la vía (Congreso "
        "de la República de Colombia, 2011). Su artículo 12, modificado por el artículo 110 del "
        "Decreto Ley 2106 de 2019, dispone que toda entidad u organización pública o privada con una "
        "flota superior a diez vehículos, o que contrate o administre personal de conductores, debe "
        "diseñar e implementar un Plan Estratégico de Seguridad Vial, según la metodología del "
        "Ministerio de Transporte, y articularlo con el Sistema de Gestión de la Seguridad y Salud en "
        "el Trabajo. El plan puede implementarse sin aval previo. Como mínimo, debe contener un "
        "diagnóstico de los riesgos asociados a la flota o a los conductores, capacitaciones, "
        "compromisos de la dirección e inspección y mantenimiento de los vehículos (Presidencia de "
        "la República de Colombia, 2019)."
    )
    P(
        "La metodología de ese plan fue adoptada por la Resolución 20223040040595 del 12 de julio de "
        "2022 y quedó compilada en la Resolución Única de Tránsito, Resolución 20223040045295 del 4 "
        "de agosto de 2022 (Ministerio de Transporte, 2022c, 2022b). La Agencia Nacional de Seguridad "
        "Vial, creada por la Ley 1702 de 2013, difunde esa línea y los materiales de apoyo para la "
        "formación (Congreso de la República de Colombia, 2013b; Agencia Nacional de Seguridad Vial, "
        "s. f.-b). La Ley 2251 de 2022 incorpora el enfoque de Sistema Seguro y sostiene que una muerte "
        "en la vía es evitable cuando se actúa sobre el conjunto del sistema, y no solo sobre el "
        "conductor al que se le atribuye el último error (Congreso de la República de Colombia, 2022)."
    )
    P(
        "Para los CEA, la norma de contenidos es la Resolución 20223040009425 de 2022, que actualizó "
        "el anexo curricular de la Resolución 3245 de 2009 y quedó compilada en el artículo 3.2.3.1 "
        "de la Resolución 20223040045295 de 2022. El curso debe formar en cuatro núcleos: movilidad "
        "segura y sostenible, normas de tránsito, señalización vial e infraestructura vial, y el "
        "vehículo, incluidos los conocimientos de la categoría. Se organiza en tres módulos. El teórico "
        "busca que la persona reconozca esos núcleos. El de formación básica aplicada trabaja la "
        "revisión preoperacional y la mecánica básica con criterio de seguridad. El específico lleva "
        "lo aprendido a las maniobras y a la circulación. La resolución pide una malla con aprendizaje "
        "basado en problemas u otra metodología de aprendizaje significativo, una guía para planear y "
        "evaluar la sesión, y el uso de los recursos de la Escuela Virtual de la Agencia Nacional de "
        "Seguridad Vial (Ministerio de Transporte, 2022a)."
    )
    P(
        "Dentro del núcleo de movilidad, la misma resolución detalla saberes que la malla tuvo que "
        "cubrir: Visión Cero, víctimas, usuarios vulnerables y su prioridad, corresponsabilidad, "
        "movilidad sostenible, omisión de socorro y protocolo de proteger, avisar y socorrer, "
        "conducción preventiva —distancia, adelantamiento, velocidad según la vía, noche y lluvia— "
        "y conducción eficiente. En normas exige autorregulación, autoridades, documentos, infracciones "
        "frecuentes, estructura del Código, restricciones de la ciudad, conductas apropiadas e "
        "inapropiadas, alimentación, estrés y fatiga, alcohol y otras sustancias, y velocidad. En "
        "señalización exige clases de señales y su prelación, vías y sus riesgos, carriles, "
        "cicloinfraestructura, espacio público y semaforización. En el vehículo exige conocimiento "
        "general, revisión, seguridad activa y pasiva, y lo propio de la categoría (Ministerio de "
        "Transporte, 2022a). El apartado de desarrollo muestra en qué curso quedó cada bloque."
    )
    P(
        "La señalización tiene un referente actualizado. La Resolución 20243040045005 del 17 de "
        "septiembre de 2024, publicada en el Diario Oficial 52.896 del 1 de octubre de 2024, sustituyó "
        "el capítulo correspondiente de la resolución única y adoptó el Manual de Señalización Vial "
        "de Colombia (Ministerio de Transporte, 2024; Agencia Nacional de Seguridad Vial, s. f.-a). "
        "Las familias que se enseñan en clase —reglamentarias, preventivas e informativas—, la "
        "demarcación y los semáforos siguen siendo la puerta de entrada para el aspirante. El manual "
        "de 2024 es la fuente para comprobar la forma y el uso vigente de cada dispositivo."
    )
    P(
        "En la malla también se trabaja la distancia lateral mínima de 1,5 metros al adelantar a un "
        "ciclista, prevista en la Ley 1811 de 2016 (Congreso de la República de Colombia, 2016). En "
        "el curso de servicio público se usa la Ley 1618 de 2013 como marco del acceso de las personas "
        "con discapacidad al transporte en condiciones de igualdad (Congreso de la República de "
        "Colombia, 2013a). La identificación de peligros del cargo de instructor de motocicleta se "
        "apoyó en la Guía Técnica Colombiana GTC 45, citada en el instructivo de la matriz con fecha "
        "de referencia 15 de diciembre de 2010 (ICONTEC, 2010). Esa guía valora el riesgo a partir de "
        "la deficiencia de los controles, la exposición y la consecuencia. Se utiliza porque el PESV "
        "debe articularse con la seguridad y salud en el trabajo, y porque el instrumento diligenciado "
        "declara esa metodología. La GTC 45 aporta el diagnóstico de condiciones de trabajo del cargo. "
        "La metodología del PESV sigue siendo la de la Resolución 20223040040595 de 2022."
    )

    H1("7. Desarrollo del proyecto")
    H2("7.1 Contextualización del entorno de aplicación")
    P(
        "El CEA VIP del Norte es un Centro de Enseñanza Automovilística. La certificación del 19 de "
        "noviembre de 2026, suscrita por su representante legal, describe mi función como instructor "
        "teórico y coordinador de formación en seguridad vial. La vinculación con esta institución es "
        "de un año y seis meses. La experiencia general como instructor del componente teórico, según "
        "el mismo documento, es de aproximadamente siete años (CEA VIP del Norte, 2026a). El análisis "
        "se concentra en las actividades de formación. Por eso se omiten el correo institucional, el "
        "identificador tributario y la reproducción de la firma."
    )
    P(
        "Luz Verde es el nombre con el que, en el proyecto y en el entorno de trabajo, se organizó el "
        "componente académico: los cursos, las lecciones, las actividades y las herramientas de "
        "práctica del aspirante y del instructor (Rodríguez Obredor, 2026). La malla revisada para "
        "este trabajo está construida sobre los núcleos del CEA y sobre las categorías de licencia A2, "
        "B1 y C1. Varias lecciones usan situaciones de Barranquilla. En el curso de servicio público, "
        "la última lección trabaja rutas urbanas y la convivencia con Transmetro. Esos ejemplos sitúan "
        "la formación en el área metropolitana del Atlántico, que es el territorio donde se aplica. "
        "Los documentos revisados permiten describir la estructura académica. El tamaño de los grupos "
        "y el parque automotor de la escuela quedan por fuera de este análisis."
    )
    P(
        "La certificación agrupa las funciones en cuatro bloques: formación y educación vial; diseño "
        "y estructuración académica; Plan Estratégico de Seguridad Vial; y afinidad con el programa "
        "técnico (CEA VIP del Norte, 2026a). Los apartados siguientes siguen ese orden. Cuando un "
        "recurso está en la malla, se presenta como recurso estructurado. Cuando la certificación "
        "registra que las clases fueron planeadas y orientadas, se presenta como actividad ejercida. "
        "Cuando la matriz advierte que la valoración es una propuesta, se presenta como propuesta."
    )

    H2("7.2 Formación y educación vial")
    P(
        "En el primer bloque, la certificación registra tres tareas. La primera es planear y orientar "
        "clases teóricas para aspirantes a conductores sobre legislación y normas de tránsito, "
        "señalización, comportamiento vial y responsabilidades de los actores. La segunda es desarrollar "
        "procesos pedagógicos sobre movilidad segura, conducción preventiva, prevención de siniestros "
        "y protección de peatones, ciclistas, motociclistas y demás usuarios. La tercera es enseñar a "
        "reconocer factores de riesgo y situaciones peligrosas, y promover comportamientos responsables "
        "(CEA VIP del Norte, 2026a)."
    )
    P(
        "Esas tareas se reconocen en la secuencia de los cursos de formación general. El curso de "
        "normas empieza por la autorregulación y la responsabilidad, sigue con las autoridades y el "
        "Código, los documentos para circular, la velocidad, la prelación, el adelantamiento y el "
        "estacionamiento, los factores humanos —incluido el alcohol y otras sustancias—, la carga y "
        "las infracciones. El curso de señales trabaja para qué sirve una señal, las reglamentarias, "
        "las preventivas, las informativas y un repaso. La demarcación, los semáforos y los dispositivos "
        "están en el curso de señalización e infraestructura, de modo que el aspirante distinga lo que "
        "ve en el poste, lo que ve en el pavimento y lo que regula el paso con luces."
    )
    P(
        "La protección de los usuarios vulnerables tiene cursos propios. El de movilidad segura trata "
        "el Sistema Seguro, las consecuencias de los siniestros, la prioridad de peatones, ciclistas, "
        "motociclistas, niños, personas mayores y personas con discapacidad, la conducción preventiva, "
        "la visibilidad, el clima y la movilidad sostenible. El curso de la vía y el espacio público "
        "baja esos temas a la posición en el carril, la cicloinfraestructura, los andenes, los paraderos "
        "y los conflictos del espacio público. Los primeros auxilios en la vía cubren el protocolo de "
        "proteger, avisar y socorrer, con un límite que la lección del primer respondiente deja claro: "
        "quien llega primero protege la escena y pide ayuda, y se abstiene de mover a una persona "
        "lesionada cuando esa maniobra excede lo que sabe hacer."
    )
    P(
        "La enseñanza por categorías respeta lo que cada vehículo exige. La motocicleta tiene curso "
        "de protección personal, revisión antes de salir, frenado y curvas, posición en el carril y "
        "puntos ciegos, lluvia, calor, fatiga, acompañante y carga. El automóvil trabaja el puesto de "
        "conducción, el embrague y los cambios, el frenado, la reversa, el estacionamiento y los giros. "
        "El servicio público trabaja el régimen y los documentos, la atención al usuario, el ascenso "
        "de pasajeros en condición de vulnerabilidad, la fatiga por los tiempos de la operación y la "
        "conducción urbana. Lo propio de cada categoría permanece en su curso."
    )

    H2("7.3 Organización y estructuración de los contenidos")
    P(
        "La certificación señala dos labores de coordinación académica: diseñar y estructurar el "
        "pensum teórico, con módulos sobre normas, legislación, señalización, seguridad vial, "
        "prevención de siniestros y factores de riesgo; y proponer la secuencia de contenidos y las "
        "estrategias para fortalecer las competencias de los aspirantes (CEA VIP del Norte, 2026a)."
    )
    rows, n_lessons, n_minutes = course_rows()
    hours, mins = divmod(n_minutes, 60)
    P(
        f"La malla organizada en Luz Verde tiene diez cursos y {n_lessons} lecciones. Sumadas las "
        f"duraciones previstas de estudio, el recorrido es de {n_minutes} minutos, es decir, {hours} horas "
        f"y {mins} minutos (Rodríguez Obredor, 2026). Esa cifra es tiempo de lección diseñado. La lección "
        "de factores humanos, alcohol y sustancias está prevista en 80 minutos porque incorpora una "
        "clase grabada extensa, partida en cinco segmentos. Leída así, la duración del curso de normas "
        "se entiende: el tiempo adicional corresponde al material audiovisual de ese tema, no a una "
        "lista más larga de artículos. La Tabla 1 resume los cursos. El Anexo A lista cada lección."
    )
    add(
        "table",
        "Tabla 1",
        "Cursos del componente teórico organizado en Luz Verde",
        ["Curso", "Lecciones", "Minutos"],
        rows,
        [11.2, 2.6, 2.6],
    )
    P(
        "Nota. Los minutos corresponden a la duración prevista de cada lección en la malla. El total "
        "de normas incluye los 80 minutos de la lección de factores humanos, que contiene la clase "
        "grabada sobre alcoholemia."
    )
    P(
        "El orden es progresivo. Primero se reconoce la señal y la norma. Después se lee la vía, el "
        "vehículo y la conducta preventiva. Los primeros auxilios quedan como respuesta cuando el "
        "siniestro ya ocurrió. Al cierre están las categorías. Hubo decisiones de integración para "
        "que un mismo tema no ocupara dos cursos con el mismo desarrollo. La meta de Visión Cero quedó "
        "dentro de la lección de Sistema Seguro. La conducción eficiente quedó en la lección de "
        "movilidad sostenible. Las restricciones de la ciudad se repartieron entre los documentos para "
        "circular y la conducción preventiva. La revisión preoperacional de la motocicleta, alineada "
        "con la lista de chequeo que difunde la Agencia Nacional de Seguridad Vial, está en el curso "
        "A2, que es donde el aspirante de esa categoría la necesita. La revisión del vehículo en "
        "general está en el curso del vehículo. Esa decisión coincide con la formación básica aplicada "
        "que pide la resolución: identificar la revisión preoperacional con criterio de seguridad vial "
        "(Ministerio de Transporte, 2022a)."
    )
    P(
        "Las preguntas de práctica se clasifican con la misma malla. Un árbol de núcleos, temas y "
        "subtemas apunta a la lección donde se estudia cada asunto. Una pregunta sobre distancia de "
        "seguimiento queda ligada a la lección de velocidad o a la de conducción preventiva, según el "
        "énfasis. Ese árbol organiza el banco de preguntas. Acompaña la malla y permite que el repaso "
        "vuelva al tema en el que la pregunta tiene sentido (Rodríguez Obredor, 2026)."
    )

    H2("7.4 Estrategias pedagógicas aplicadas")
    P(
        "La resolución de contenidos pide aprendizaje basado en problemas, u otra metodología que "
        "produzca aprendizaje significativo, y una guía para planear la sesión (Ministerio de "
        "Transporte, 2022a). En la malla, la explicación escrita abre el tema y fija el lenguaje. "
        "Después viene la actividad. Hay preguntas de selección con su explicación, enunciados de "
        "verdadero o falso, ejercicios para clasificar elementos, secuencias para ordenar un "
        "procedimiento, tarjetas de repaso, emparejamiento y escenarios en los que el aspirante elige "
        "entre varias conductas y lee la consecuencia de cada una."
    )
    P(
        "El escenario cubre lo que una definición deja abierto. En la lección de Sistema Seguro se "
        "plantea un cruce escolar sin cebra, con un límite que sigue en 50 km/h. Alguien afirma que, "
        "si un niño resulta atropellado, la responsabilidad es solo del conductor. La actividad pide "
        "responder desde Visión Cero. La opción que la lección sostiene es doble: el conductor debe "
        "reducir la velocidad y el cruce debería estar diseñado para que un error no mate. Las otras "
        "opciones —atribuir todo al conductor, o esperar una muerte para intervenir— se explican como "
        "lecturas incompletas del enfoque. El aspirante contrasta su decisión con ese criterio "
        "(Rodríguez Obredor, 2026; Congreso de la República de Colombia, 2022)."
    )
    P(
        "Cada tipo de actividad tiene un uso. Clasificar separa la señal vertical, la marca en el piso "
        "y el dispositivo físico. Ordenar pasos sirve para la revisión antes de salir o para proteger "
        "la escena. Las tarjetas recogen normas que conviene tener a la mano, entre ellas la Ley 769 "
        "de 2002, la Ley 1503 de 2011, la Ley 1702 de 2013 y la Ley 2251 de 2022, cada una con la idea "
        "central que le corresponde en la clase. El video ilustra el concepto después de la explicación."
    )
    P(
        "Hay 26 materiales audiovisuales incorporados a las lecciones. La mayoría son piezas públicas "
        "de la Agencia Nacional de Seguridad Vial, identificadas como tales en la propia lección: "
        "Sistema Seguro, víctimas, respeto de los límites, alistamiento y revisión de la motocicleta, "
        "adelantamiento, lluvia, acompañante, carga, revisión preoperacional y cambio de llanta, entre "
        "otras. También hay una clase grabada sobre alcoholemia y pruebas, dividida en cinco partes "
        "para estudiarla por tramos; un material sobre la diferencia práctica de circular a 30 km/h; "
        "otro sobre el exceso de velocidad; uno sobre puntos ciegos; y una clase de curva en "
        "motocicleta. Esos recursos apoyan la explicación. La lección sigue teniendo texto, pregunta "
        "y, cuando el tema lo pide, un caso (Rodríguez Obredor, 2026)."
    )
    P(
        "La certificación respalda que las clases teóricas fueron planeadas y orientadas, y que se "
        "propusieron estrategias pedagógicas (CEA VIP del Norte, 2026a). En el entorno de Luz Verde "
        "el instructor dispone de aula en vivo, presentaciones y un juego de repaso para la sesión. "
        "El aspirante dispone de los cursos, de un simulacro de preguntas y de la biblioteca de normas. "
        "El simulacro está presentado como práctica de preparación. La herramienta advierte que esa "
        "práctica es distinta del examen oficial. Mantener esa advertencia forma parte de la estrategia, "
        "porque el aspirante necesita ensayar sin confundir el ensayo con la prueba de la autoridad."
    )

    H2("7.5 Legislación, señalización y comportamiento vial")
    P(
        "La legislación se enseña unida a la conducta. La lección de autoridades y del Código presenta "
        "la Ley 769 de 2002 como la norma de circulación y trabaja el procedimiento ante la autoridad "
        "con el criterio del trato digno (Congreso de la República de Colombia, 2002). La lección de "
        "documentos trabaja la licencia de conducción, la licencia de tránsito, el seguro obligatorio "
        "y la revisión técnico-mecánica, saberes del núcleo de normas (Ministerio de Transporte, 2022a). "
        "La velocidad se estudia como límite y como velocidad segura según la vía, la lluvia y la "
        "presencia de peatones. La prelación se estudia en intersecciones, glorietas, vehículos de "
        "emergencia y paso de peatones y ciclistas. Las infracciones y los comparendos cierran el "
        "curso, de modo que la sanción se entienda como consecuencia de una conducta."
    )
    P(
        "La señalización sigue la lógica de las familias y de la relación entre dispositivos. Primero "
        "se distingue la orden, la advertencia y la información por la forma y el color. Después se "
        "estudian grupos de señales reglamentarias, preventivas e informativas, con actividades para "
        "clasificarlas. El curso de infraestructura toma las líneas del centro y de los bordes, las "
        "marcas transversales, los símbolos, los dispositivos y las fases del semáforo vehicular y "
        "peatonal. El manual vigente para contrastar cada dispositivo es el adoptado en 2024 "
        "(Ministerio de Transporte, 2024). La malla enseña la estructura que el conductor necesita "
        "para leer la vía. Si alguna placa cambió de detalle en el manual nuevo, el ajuste de esa "
        "figura es una revisión posterior. Así se recomienda al cierre de este trabajo. La secuencia "
        "por familias, demarcación y semáforos se mantiene, porque es la que permite aprender."
    )
    P(
        "El comportamiento se trabaja desde la primera lección de normas, con la autorregulación, y "
        "regresa en movilidad cuando se habla de corresponsabilidad. El artículo 55 de la Ley 769 de "
        "2002 da el criterio de no poner en riesgo a los demás (Congreso de la República de Colombia, "
        "2002). La Ley 1811 de 2016 concreta ese criterio en el adelantamiento al ciclista (Congreso "
        "de la República de Colombia, 2016). En el curso de servicio público, el comportamiento incluye "
        "el trato al pasajero, la resolución de un conflicto sin convertir el vehículo en un escenario "
        "de disputa, y el ascenso seguro de la persona con discapacidad, en el marco de la Ley 1618 "
        "de 2013 (Congreso de la República de Colombia, 2013a). La formación del conductor de servicio "
        "público incluye, entonces, la maniobra y el trato."
    )

    H2("7.6 Conducción preventiva y prevención de riesgos")
    P(
        "El núcleo de movilidad, tal como lo detalla la resolución, incluye Visión Cero, víctimas, "
        "usuarios vulnerables, corresponsabilidad, movilidad sostenible, el protocolo de la escena, "
        "la conducción preventiva y la conducción eficiente (Ministerio de Transporte, 2022a). En la "
        "malla, esos saberes están repartidos con una razón pedagógica. Sistema Seguro, víctimas, "
        "usuarios vulnerables, conducción preventiva, visibilidad y movilidad sostenible están en el "
        "curso de movilidad. El protocolo de la escena está en primeros auxilios, con el tiempo que "
        "el tema pide: proteger, avisar al 123, valorar, hemorragias, quemaduras, atragantamiento y "
        "derrames. Así el protocolo tiene un curso completo, y el curso de movilidad conserva el "
        "espacio para el enfoque de sistema y para la decisión preventiva."
    )
    P(
        "La conducción preventiva se trabaja como anticipación. La lección correspondiente aborda la "
        "percepción del riesgo, la separación entre vehículos que regula el Código, la decisión de "
        "esperar cuando el adelantamiento no tiene espacio y la lectura de la conducta de los demás. "
        "Los factores que el árbol de contenidos agrupa como riesgo son la velocidad, el alcohol y "
        "otras sustancias, la fatiga, el celular, las emociones, la conducción agresiva, el exceso de "
        "confianza, la presión del grupo y el clima. Varios se estudian en la lección de factores "
        "humanos del curso de normas. La velocidad y el clima tienen lecciones propias. El celular se "
        "relaciona con la prohibición de usarlo al conducir y, además, con la pérdida de atención "
        "cuando la conversación exige concentración, aunque exista un dispositivo de manos libres."
    )
    P(
        "En motocicleta, la prevención empieza antes de ponerla en marcha: casco abrochado y acorde "
        "con el reglamento técnico que la lección cita, revisión de llantas, frenos, luces y cadena, "
        "y una posición en el carril que haga visible a quien conduce. El curso trabaja el zigzag "
        "entre vehículos y el punto ciego del carro y del vehículo pesado. En automóvil, la prevención "
        "está en el ajuste del puesto, el frenado progresivo, la pendiente y el control de la reversa. "
        "En servicio público, el riesgo específico es la fatiga y la presión por cumplir el tiempo del "
        "recorrido. La lección trata esa presión como factor de riesgo: cumplir el horario de la ruta "
        "sigue importando, y apurar el cruce por esa presión es una decisión que la clase pone a examen."
    )

    H2("7.7 Recursos y herramientas de apoyo a la enseñanza")
    P(
        "Luz Verde reúne, para el aspirante, los cursos de la malla, el simulacro de preguntas, la "
        "biblioteca normativa y actividades de repaso. Para el instructor, reúne el aula, las "
        "presentaciones y el seguimiento de resultados. Para la escuela, la plataforma también tiene "
        "agenda, expedientes y control de clases. Esos módulos permiten operar el centro. Este trabajo "
        "se ocupa de la formación vial y describe los recursos que sirven para enseñar y para estudiar "
        "(Rodríguez Obredor, 2026)."
    )
    P(
        "El aporte de los cursos está en la secuencia ya descrita y en la actividad que obliga a "
        "decidir. El simulacro permite ensayar preguntas del mismo campo —normas, señales, vía, "
        "vehículo y categorías— antes del día de la evaluación, con la advertencia de que la práctica "
        "es distinta del examen oficial. La biblioteca permite consultar la norma cuando la clase la "
        "cita, de modo que el aspirante pueda volver al texto y no depender solo de una diapositiva."
    )
    P(
        "Los 26 videos cumplen una función de apoyo. Los de la Agencia Nacional de Seguridad Vial "
        "conectan la clase con el material que la resolución de contenidos invita a usar (Ministerio "
        "de Transporte, 2022a; Agencia Nacional de Seguridad Vial, s. f.-a). La clase grabada de "
        "alcoholemia permite ver un procedimiento que en el tablero queda abstracto. El material de "
        "velocidad muestra la diferencia de circular más despacio en un entorno con personas. El de "
        "puntos ciegos le da imagen a un riesgo que el motociclista y el conductor de carro comparten "
        "desde lados distintos. El resultado que estas herramientas permiten afirmar es de orden "
        "organizativo: el componente teórico quedó secuenciado, con actividades y con la fuente de "
        "cada video identificada en la lección."
    )

    H2("7.8 Planificación, riesgos y alcance del trabajo sobre el PESV")
    P(
        "La certificación incluye diseñar y estructurar el PESV de la institución, según sus "
        "características, e identificar y analizar factores de riesgo, además de organizar estrategias, "
        "acciones preventivas, actividades de sensibilización y componentes documentales de "
        "seguimiento, con la metodología nacional (CEA VIP del Norte, 2026a). La Resolución "
        "20223040040595 de 2022 fija esa metodología y se apoya en los mínimos del artículo 12 de la "
        "Ley 1503 de 2011: diagnóstico, capacitación, compromiso directivo e inspección de vehículos "
        "(Ministerio de Transporte, 2022c; Presidencia de la República de Colombia, 2019)."
    )
    P(
        "Entre los archivos revisados, el documento de trabajo que corresponde a esa función es la "
        "matriz de identificación de peligros del cargo de instructor de motocicleta del CEA VIP. El "
        "formato es una matriz de peligros laborales, código F-SST-21, y su instructivo remite a la "
        "GTC 45 (ICONTEC, 2010; CEA VIP del Norte, 2026b). El proceso consignado es la prestación del "
        "servicio de enseñanza de conducción. La hoja trae una nota que condiciona el instrumento: la "
        "valoración es una propuesta elaborada con la información suministrada sobre el cargo, y debe "
        "validarse con las condiciones reales de la empresa. Los valores que siguen se leen, por esa "
        "nota, como propuesta de trabajo. La matriz analiza el cargo. Las clases que la certificación "
        "detalla en mi caso son las del componente teórico y la coordinación de la formación."
    )
    P(
        "Se diligenciaron cinco peligros, con un expuesto directo y con la actividad marcada como "
        "rutinaria. La Tabla 2 recoge la clasificación y los tres valores escritos en el archivo: "
        "nivel de deficiencia (ND), nivel de exposición (NE) y nivel de consecuencia (NC). En la escala "
        "que el instructivo de la matriz transcribe, el ND de 10 es muy alto, el de 6 es alto y el de "
        "2 es medio. El NE de 4 es exposición continua y el de 3 es frecuente. El NC de 100 corresponde "
        "a una consecuencia mortal o catastrófica, y el de 25 a una consecuencia grave con incapacidad "
        "temporal (ICONTEC, 2010). El formulario está hecho para calcular la probabilidad y el nivel "
        "de riesgo con esos datos. Esas casillas calculadas no quedaron almacenadas en el archivo. "
        "Este trabajo las deja sin completar, para que una cifra calculada después no se lea como un "
        "nivel ya adoptado por la empresa."
    )
    add(
        "table",
        "Tabla 2",
        "Peligros propuestos para el cargo de instructor de motocicleta",
        ["Peligro propuesto", "ND", "NE", "NC"],
        [
            ("Evento de tránsito en la práctica en vía pública", "10", "3", "100"),
            ("Postura prolongada en la motocicleta y en teoría", "2", "4", "25"),
            ("Ruido y vibración del motor y del tráfico", "2", "3", "25"),
            ("Calor y radiación solar en la práctica abierta", "2", "3", "25"),
            ("Jornada prolongada y carga mental", "6", "3", "25"),
        ],
        [10.5, 2.0, 2.0, 2.0],
    )
    P(
        "Nota. ND = nivel de deficiencia; NE = nivel de exposición; NC = nivel de consecuencia, según "
        "la escala que el instructivo de la matriz toma de la GTC 45. La hoja indica que la valoración "
        "debe validarse con las condiciones reales de la empresa (CEA VIP del Norte, 2026b)."
    )
    P(
        "En la fila del evento de tránsito, la fuente descrita es la circulación en vía pública, la "
        "interacción con otros actores, el error propio o del aprendiz y el estado de la vía. Los "
        "controles ya anotados son el chaleco de identificación y el casco. Las medidas propuestas "
        "incluyen escoger vías de menor exposición cuando exista alternativa, revisar el estado "
        "mecánico antes de cada clase, planear la ruta, acordar un límite de velocidad para la "
        "práctica, definir un protocolo de emergencia y mantener casco y chaleco reflectivo. En la "
        "fila de calor, las medidas propuestas son hidratación, pausas y evitar las horas de mayor "
        "exposición cuando la programación lo permita. En la fila de carga mental, la descripción de "
        "la tarea consigna una jornada de referencia de diez horas, seis de teoría y cuatro de "
        "práctica. Ese dato entra a la matriz como información suministrada para el cargo. La nota de "
        "validación indica que debe contrastarse con la operación. La medida propuesta es distribuir "
        "las clases, programar pausas y hacer seguimiento a la fatiga (CEA VIP del Norte, 2026b)."
    )
    P(
        "Estas acciones de capacitación, inspección antes de la clase y organización de la jornada "
        "dialogan con dos piezas del PESV: la capacitación de las personas y la inspección de los "
        "vehículos con los que se presta el servicio. La sensibilización tiene un soporte en las clases "
        "y en los materiales sobre Sistema Seguro, víctimas y usuarios vulnerables (Agencia Nacional "
        "de Seguridad Vial, s. f.-b). El archivo revisado conserva la matriz del cargo y la malla de "
        "formación. El diagnóstico del conjunto de la flota, el acta de compromisos de la dirección y "
        "un expediente único de seguimiento del plan son piezas del mínimo legal que este archivo no "
        "reúne (Presidencia de la República de Colombia, 2019). La recomendación final es integrarlas, "
        "junto con la matriz ya validada, si la escuela continúa la estructuración del PESV."
    )

    H2("7.9 Relación con las competencias del programa")
    P(
        "La certificación declara que las funciones guardan relación con el programa técnico porque "
        "comprenden educación y formación de actores viales, interpretación de normas de tránsito, "
        "identificación de riesgos, prevención de siniestros, promoción de la movilidad segura y "
        "planificación de estrategias institucionales de seguridad vial (CEA VIP del Norte, 2026a). "
        "El desarrollo permite sostener esa relación con actividades concretas."
    )
    P(
        "La educación de actores viales está en la planeación y orientación de las clases y en la "
        "malla de diez cursos. La interpretación de normas está en el curso de legislación y en el uso "
        "de la Ley 769 de 2002, la Ley 1503 de 2011, la Ley 2251 de 2022 y la Ley 1811 de 2016 como "
        "contenido de clase. La identificación de riesgos está en las lecciones de factores humanos, "
        "conducción preventiva y categorías, y en la matriz del cargo de instructor de motocicleta. "
        "La prevención está en la lógica de Sistema Seguro, en la revisión preoperacional y en el "
        "protocolo de la escena. La movilidad segura está en el curso que lleva ese nombre y en el "
        "trato a peatones, ciclistas y pasajeros. La planificación institucional está en la "
        "estructuración del pensum y en el trabajo documental del PESV, con el alcance ya precisado: "
        "hay diseño del componente académico y hay una matriz propuesta para un cargo."
    )
    add(
        "table",
        "Tabla 3",
        "Núcleos exigidos al CEA y cursos en los que quedaron organizados",
        ["Núcleo de la Resolución 20223040009425 de 2022", "Dónde quedó en la malla"],
        [
            ("Movilidad segura y sostenible", "Curso de movilidad segura. El protocolo de la escena, en primeros auxilios."),
            ("Normas de tránsito", "Curso de normas de tránsito básicas."),
            ("Señalización e infraestructura", "Cursos de señales, de señalización e infraestructura y de la vía."),
            ("El vehículo y la categoría", "Curso del vehículo, y cursos de motocicleta A2, automóvil B1 y servicio público C1."),
        ],
        [7.2, 9.2],
    )
    P(
        "Nota. La correspondencia toma los cuatro núcleos del anexo curricular compilado en la "
        "Resolución 20223040045295 de 2022 (Ministerio de Transporte, 2022a, 2022b)."
    )

    H2("7.10 Evidencias y alcance de los resultados")
    P(
        "Las evidencias que sostienen este documento son de tres clases. La primera es la certificación "
        "laboral del 19 de noviembre de 2026, que fija el cargo, el tiempo de vinculación, la "
        "experiencia general en el componente teórico y las funciones (CEA VIP del Norte, 2026a). El "
        "número de documento figura en la portada porque el formato de FORMATEC lo pide (FORMATEC, "
        "2022). La certificación completa permanece como soporte del estudiante."
    )
    P(
        f"La segunda evidencia es la malla de Luz Verde: diez cursos, {n_lessons} lecciones, "
        f"{n_minutes} minutos de estudio diseñado, actividades de decisión y 26 videos (Rodríguez "
        "Obredor, 2026). El Anexo A detalla las lecciones. La tercera es la matriz de peligros del "
        "cargo de instructor de motocicleta, resumida en la Tabla 2 y en el Anexo B, incluida la nota "
        "que exige validarla (CEA VIP del Norte, 2026b)."
    )
    P(
        "El resultado que estas evidencias permiten formular es descriptivo. El componente teórico "
        "quedó secuenciado conforme a los núcleos del CEA. Las estrategias piden una decisión, además "
        "de la comprensión del concepto. La identificación de riesgos del cargo de instructor de "
        "motocicleta quedó propuesta, con controles de capacitación, de inspección y de pausas, a la "
        "espera de validación con la operación real. Ese es el alcance del trabajo."
    )

    H1("8. Conclusiones y recomendaciones")
    H2("Conclusiones")
    P(
        "El objetivo general se cumple en la medida en que el documento analiza, con fuentes "
        "identificables, el diseño y la aplicación de las estrategias de formación en el CEA VIP del "
        "Norte. La experiencia certificada y la malla de Luz Verde muestran un mismo esfuerzo: que el "
        "aspirante estudie la norma, la señal, la vía, el vehículo y la conducta preventiva en un "
        "orden, y que practique la decisión antes de salir a circular."
    )
    P(
        "El primer objetivo específico queda atendido con la identificación de las funciones "
        "certificadas y de los materiales que les corresponden. Hubo orientación de clases teóricas, "
        "organización de módulos y una propuesta de identificación de riesgos. Las actividades "
        "descritas son las que tienen soporte en la certificación o en los archivos."
    )
    P(
        "El segundo objetivo queda atendido con el análisis de la secuencia y de las estrategias. La "
        "malla separa la formación general de las categorías A2, B1 y C1, reúne en una sola lección "
        "los temas que se repetirían en dos cursos, y usa escenarios, clasificación, ordenamiento y "
        "video. El caso del cruce escolar muestra el tipo de pregunta de la lección de Sistema Seguro: "
        "la respuesta que se sostiene mira la velocidad del conductor y el diseño del lugar. El tiempo "
        "total de estudio diseñado se lee junto con la salvedad de la lección de alcoholemia, más larga "
        "por la clase grabada en cinco partes."
    )
    P(
        "El tercer objetivo queda atendido con el cruce entre las funciones, las competencias del "
        "programa y los núcleos de la Resolución 20223040009425 de 2022. Educación vial, lectura de "
        "la norma, riesgo, prevención, movilidad segura y planificación aparecen en actividades "
        "concretas. El programa técnico y el trabajo en el CEA se encuentran en ese punto, que es el "
        "que la propia certificación destaca (CEA VIP del Norte, 2026a; FORMATEC, 2026)."
    )
    P(
        "El cuarto objetivo queda atendido al describir la matriz con su condición de propuesta. Hay "
        "cinco peligros para el cargo de instructor de motocicleta, con valores de deficiencia, "
        "exposición y consecuencia, y con medidas de capacitación, inspección y pausas. La hoja pide "
        "validación. El diagnóstico del conjunto de la flota, los compromisos de dirección y el "
        "expediente de seguimiento son componentes del mínimo legal que los archivos revisados no "
        "reúnen. Dejar ese límite escrito también es un resultado del trabajo (CEA VIP del Norte, "
        "2026b; Presidencia de la República de Colombia, 2019)."
    )
    P(
        "De la experiencia quedan aprendizajes para el ejercicio técnico. La norma se comprende mejor "
        "cuando el aspirante tiene que usarla en un caso. La señal se comprende mejor cuando se "
        "distingue de la demarcación y del semáforo, y cuando se acepta que el manual de 2024 puede "
        "precisar el detalle de una placa. La prevención incluye al peatón, al ciclista y al "
        "motociclista, y también la fatiga de quien conduce un servicio público presionado por el "
        "tiempo. La planificación de la seguridad vial pide el mismo cuidado que la clase: un formato "
        "diligenciado es un avance, y se vuelve diagnóstico de la empresa cuando se contrasta con la "
        "operación real."
    )
    H2("Recomendaciones")
    P(
        "Validar la matriz del cargo de instructor de motocicleta con las condiciones reales de las "
        "rutas, de los vehículos y de la jornada, antes de usarla como diagnóstico oficial. La nota "
        "del instrumento ya lo pide (CEA VIP del Norte, 2026b). Hasta esa validación, la matriz debe "
        "seguir presentada como propuesta, que es la condición en la que fue elaborada."
    )
    P(
        "Si la escuela continúa la estructuración del PESV, reunir los mínimos que la Ley 1503 de "
        "2011 y la Resolución 20223040040595 de 2022 establecen y que este archivo no contiene: el "
        "diagnóstico asociado a la flota, los compromisos de la dirección, el registro de inspección "
        "de los vehículos de enseñanza y el seguimiento del plan (Presidencia de la República de "
        "Colombia, 2019; Ministerio de Transporte, 2022c). La capacitación y la sensibilización ya "
        "tienen un soporte en las clases y en la malla. Ese soporte puede alimentar el componente de "
        "formación del plan."
    )
    P(
        "Contrastar las láminas y los ejemplos de señales con el Manual de Señalización Vial adoptado "
        "por la Resolución 20243040045005 de 2024, y ajustar la figura que haya cambiado (Ministerio "
        "de Transporte, 2024). La secuencia por familias, demarcación y semáforos puede mantenerse, "
        "porque es la que ordena el aprendizaje."
    )
    P(
        "Mantener la separación entre los cursos generales y los de categoría, y seguir usando los "
        "escenarios en la clase presencial, además del estudio individual. El simulacro debe seguir "
        "anunciado como práctica. El aspirante necesita saber que ensayar preguntas prepara la "
        "evaluación y que esa práctica es distinta del examen oficial."
    )
    P(
        "Al entregar este trabajo en el módulo de prácticas académicas de la plataforma institucional, "
        "conviene anexar la certificación laboral en su archivo original, si la institución lo pide "
        "como soporte. El análisis que aquí se lee puede comprenderse con las citas y con los anexos "
        "de la malla y de la matriz (FORMATEC, 2026)."
    )

    H1("9. Referencias bibliográficas")
    refs = [
        "Agencia Nacional de Seguridad Vial. (s. f.-a). Manual de señalización vial. https://ansv.gov.co/es/publicaciones/manual-de-senalizacion-vial",
        "Agencia Nacional de Seguridad Vial. (s. f.-b). Planes Estratégicos de Seguridad Vial. https://ansv.gov.co/es/escuela/10446",
        "CEA VIP del Norte. (2026a). Certificación laboral y de experiencia en actividades afines al programa técnico en seguridad vial [Certificación institucional]. Centro de Enseñanza Automovilística CEA VIP del Norte.",
        "CEA VIP del Norte. (2026b). Matriz de identificación de peligros del cargo de instructor de motocicleta [Documento de trabajo]. Centro de Enseñanza Automovilística CEA VIP del Norte.",
        "Centro de Capacitación y Formación Técnica FORMATEC. (2022). Formato de trabajo guiado (Versión 001). Sistema Integrado de Gestión.",
        "Centro de Capacitación y Formación Técnica FORMATEC. (2026). Circular académica n.º 005-2026: Lineamientos para el desarrollo y entrega del trabajo alternativo a la práctica de grado.",
        "Congreso de la República de Colombia. (2002). Ley 769 de 2002. Por la cual se expide el Código Nacional de Tránsito Terrestre y se dictan otras disposiciones. Diario Oficial 44.893. https://www.secretariasenado.gov.co/senado/basedoc/ley_0769_2002.html",
        "Congreso de la República de Colombia. (2011). Ley 1503 de 2011. Por la cual se promueve la formación de hábitos, comportamientos y conductas seguros en la vía y se dictan otras disposiciones. https://www.secretariasenado.gov.co/senado/basedoc/ley_1503_2011.html",
        "Congreso de la República de Colombia. (2013a). Ley 1618 de 2013. Por la cual se establecen las disposiciones para garantizar el pleno ejercicio de los derechos de las personas con discapacidad. https://www.secretariasenado.gov.co/senado/basedoc/ley_1618_2013.html",
        "Congreso de la República de Colombia. (2013b). Ley 1702 de 2013. Por la cual se crea la Agencia Nacional de Seguridad Vial y se dictan otras disposiciones. https://www.secretariasenado.gov.co/senado/basedoc/ley_1702_2013.html",
        "Congreso de la República de Colombia. (2016). Ley 1811 de 2016. Por la cual se otorgan incentivos para promover el uso de la bicicleta en el territorio nacional y se modifica el Código Nacional de Tránsito. https://www.secretariasenado.gov.co/senado/basedoc/ley_1811_2016.html",
        "Congreso de la República de Colombia. (2022). Ley 2251 de 2022. Por la cual se dictan normas para el diseño e implementación de la política de seguridad vial con enfoque de sistema seguro y se dictan otras disposiciones. https://www.secretariasenado.gov.co/senado/basedoc/ley_2251_2022.html",
        "ICONTEC. (2010). Guía para la identificación de los peligros y la valoración de los riesgos en seguridad y salud ocupacional (GTC 45).",
        "Ministerio de Transporte. (2015). Decreto 1079 de 2015. Por medio del cual se expide el Decreto Único Reglamentario del Sector Transporte. https://www.secretariasenado.gov.co/senado/basedoc/decreto_1079_2015.html",
        "Ministerio de Transporte. (2022a). Resolución 20223040009425 de 2022. Por la cual se modifican los contenidos curriculares para los cursos de formación de conductores e instructores de los Centros de Enseñanza Automovilística.",
        "Ministerio de Transporte. (2022b). Resolución 20223040045295 de 2022. Por medio de la cual se expide la Resolución Única Compilatoria en materia de tránsito. https://www.runt.gov.co/sites/default/files/normas/MinTransporte-Resolucion-2022-N0045295_20220804.pdf",
        "Ministerio de Transporte. (2022c). Resolución 20223040040595 de 2022. Por la cual se adopta la metodología para el diseño, implementación y verificación de los Planes Estratégicos de Seguridad Vial. https://www.alcaldiabogota.gov.co/sisjur/normas/Norma1.jsp?i=126477",
        "Ministerio de Transporte. (2024). Resolución 20243040045005 de 2024. Por medio de la cual se adopta el Manual de Señalización Vial de Colombia. Diario Oficial 52.896.",
        "Presidencia de la República de Colombia. (2019). Decreto Ley 2106 de 2019. Por el cual se dictan normas para simplificar, suprimir y reformar trámites, procesos y procedimientos innecesarios existentes en la administración pública. https://www.secretariasenado.gov.co/senado/basedoc/decreto_2106_2019.html",
        "Rodríguez Obredor, B. D. (2026). Componente académico de formación vial organizado en Luz Verde [Material de formación no publicado]. Centro de Enseñanza Automovilística CEA VIP del Norte.",
    ]
    for ref in refs:
        add("ref", ref)

    add("pb", "")
    H1("Anexos")
    H2("Anexo A. Lecciones de la malla de Luz Verde")
    P(
        "La tabla reúne las 57 lecciones y la duración prevista de estudio, en el orden de la malla. "
        "Sirve de respaldo a la Tabla 1 y al apartado de organización de contenidos (Rodríguez Obredor, 2026)."
    )
    lesson_rows = []
    for name, items in COURSES:
        for title, minutes in items:
            lesson_rows.append((name, title, str(minutes)))
    add(
        "table",
        "Tabla A1",
        "Lecciones por curso y minutos previstos",
        ["Curso", "Lección", "Min"],
        lesson_rows,
        [5.2, 10.2, 1.4],
    )

    H2("Anexo B. Nota y medidas de la matriz de peligros")
    P(
        "La matriz de peligros del cargo de instructor de motocicleta incluye esta advertencia, que "
        "se recoge aquí por su efecto sobre la lectura de la Tabla 2: la valoración fue propuesta con "
        "base en la información suministrada sobre el cargo en el CEA VIP, y debe validarse con las "
        "condiciones reales de la empresa (CEA VIP del Norte, 2026b). Las medidas de intervención "
        "propuestas en el archivo, resumidas, son las siguientes."
    )
    LI("Evento de tránsito: elegir vías de menor exposición cuando exista alternativa, inspección mecánica antes de la clase, ruta planificada, límite de velocidad de la práctica, protocolo de emergencia, casco y chaleco reflectivo.")
    LI("Postura prolongada: alternar teoría y práctica cuando la programación lo permita, pausas activas y higiene postural.")
    LI("Ruido y vibración: mantenimiento preventivo, pausas entre clases y casco; la protección auditiva solo si resulta compatible con la comunicación en la vía.")
    LI("Calor: hidratación, pausas, puntos de descanso con sombra y programación que reduzca las horas de mayor exposición cuando sea posible.")
    LI("Carga mental: distribución de las clases, pausas, seguimiento de la fatiga y capacitación en el manejo del estrés asociado a la enseñanza.")
    P(
        "Estas medidas son propuestas del instrumento. Su puesta en práctica depende de la validación "
        "con la operación del centro y de las decisiones de la dirección."
    )
    return b, refs


def set_run_font(run, size=12, bold=False, italic=False):
    run.font.name = "Times New Roman"
    run.bold = bold
    run.italic = italic
    run.font.size = Pt(size)
    run.font.color.rgb = RGBColor(0, 0, 0)
    r = run._element
    rPr = r.get_or_add_rPr()
    rFonts = rPr.find(qn("w:rFonts"))
    if rFonts is None:
        rFonts = OxmlElement("w:rFonts")
        rPr.append(rFonts)
    for attr in ("w:ascii", "w:hAnsi", "w:cs", "w:eastAsia"):
        rFonts.set(qn(attr), "Times New Roman")


def set_paragraph(p, align="justify", indent=True, before=0, after=0, line=2.0, hanging=False):
    pf = p.paragraph_format
    pf.line_spacing_rule = WD_LINE_SPACING.DOUBLE if line == 2 else WD_LINE_SPACING.SINGLE
    if line != 2:
        pf.line_spacing = line
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    align_map = {
        "justify": WD_ALIGN_PARAGRAPH.JUSTIFY,
        "center": WD_ALIGN_PARAGRAPH.CENTER,
        "left": WD_ALIGN_PARAGRAPH.LEFT,
        "right": WD_ALIGN_PARAGRAPH.RIGHT,
    }
    p.alignment = align_map[align]
    if hanging:
        pf.left_indent = Cm(1.27)
        pf.first_line_indent = Cm(-1.27)
    elif indent:
        pf.first_line_indent = Cm(1.27)
    else:
        pf.first_line_indent = Cm(0)
        pf.left_indent = Cm(0)


def shade_header(cell):
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), "E8E8E8")
    shd.set(qn("w:val"), "clear")
    tcPr.append(shd)


def set_cell_border(cell):
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    tcBorders = OxmlElement("w:tcBorders")
    for edge in ("top", "left", "bottom", "right"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:val"), "single")
        el.set(qn("w:sz"), "4")
        el.set(qn("w:color"), "666666")
        tcBorders.append(el)
    tcPr.append(tcBorders)


def add_page_number(paragraph):
    run = paragraph.add_run()
    fld1 = OxmlElement("w:fldChar")
    fld1.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = " PAGE "
    fld2 = OxmlElement("w:fldChar")
    fld2.set(qn("w:fldCharType"), "end")
    run._r.append(fld1)
    run._r.append(instr)
    run._r.append(fld2)
    set_run_font(run, 10)


def prevent_row_split(row):
    tr = row._tr
    trPr = tr.get_or_add_trPr()
    cant = OxmlElement("w:cantSplit")
    trPr.append(cant)


def write_docx(blocks):
    doc = Document()
    section = doc.sections[0]
    section.page_width = Cm(21.59)
    section.page_height = Cm(27.94)
    section.top_margin = Cm(2.54)
    section.bottom_margin = Cm(2.54)
    section.left_margin = Cm(2.54)
    section.right_margin = Cm(2.54)
    section.header_distance = Cm(0.8)
    section.footer_distance = Cm(0.6)

    header = section.header
    header.is_linked_to_previous = False
    hp = header.paragraphs[0]
    hp.clear()
    set_paragraph(hp, "left", False, 0, 0, 1)
    r = hp.add_run("FORMATEC  ·  Trabajo guiado")
    set_run_font(r, 10)
    r2 = hp.add_run("\t")
    set_run_font(r2, 10)
    add_page_number(hp)
    tab = hp.paragraph_format.tab_stops
    tab.add_tab_stop(Cm(16.5), WD_ALIGN_PARAGRAPH.RIGHT)

    footer = section.footer
    footer.is_linked_to_previous = False
    fp = footer.paragraphs[0]
    fp.clear()
    set_paragraph(fp, "center", False, 0, 0, 1)
    fr = fp.add_run("Carrera 19 No. 24A-27, Soledad, Atlántico  ·  www.formatec.edu.co")
    set_run_font(fr, 9)

    normal = doc.styles["Normal"]
    normal.font.name = "Times New Roman"
    normal.font.size = Pt(12)
    normal.font.color.rgb = RGBColor(0, 0, 0)

    for block in blocks:
        kind = block[0]
        if kind == "pb":
            doc.add_page_break()
            continue
        if kind in ("cover_h", "cover_title"):
            p = doc.add_paragraph()
            set_paragraph(p, "center", False, 0, 6, 2)
            run = p.add_run(block[1])
            set_run_font(run, 14, True)
            continue
        if kind == "cover":
            p = doc.add_paragraph()
            set_paragraph(p, "center", False, 0, 0, 2)
            run = p.add_run(block[1])
            set_run_font(run, 12)
            continue
        if kind == "cover_gap":
            p = doc.add_paragraph()
            set_paragraph(p, "center", False, 0, 0, 2)
            continue
        if kind == "h1":
            p = doc.add_paragraph()
            set_paragraph(p, "center", False, 12, 6, 2)
            run = p.add_run(block[1])
            set_run_font(run, 14, True)
            continue
        if kind == "h2":
            p = doc.add_paragraph()
            set_paragraph(p, "left", False, 12, 4, 2)
            run = p.add_run(block[1])
            set_run_font(run, 12, True)
            continue
        if kind == "p":
            p = doc.add_paragraph()
            set_paragraph(p, "justify", True, 0, 0, 2)
            run = p.add_run(block[1])
            set_run_font(run, 12)
            continue
        if kind == "li":
            p = doc.add_paragraph()
            set_paragraph(p, "justify", False, 0, 0, 2)
            p.paragraph_format.left_indent = Cm(1.27)
            p.paragraph_format.first_line_indent = Cm(-0.4)
            run = p.add_run("•  " + block[1])
            set_run_font(run, 12)
            continue
        if kind == "ref":
            p = doc.add_paragraph()
            set_paragraph(p, "left", False, 0, 0, 2, hanging=True)
            run = p.add_run(block[1])
            set_run_font(run, 12)
            continue
        if kind == "table":
            _, caption_no, caption, headers, rows, widths = block
            cap = doc.add_paragraph()
            set_paragraph(cap, "left", False, 12, 0, 2)
            rn = cap.add_run(caption_no)
            set_run_font(rn, 12, True)
            cap2 = doc.add_paragraph()
            set_paragraph(cap2, "left", False, 0, 6, 1.15)
            ri = cap2.add_run(caption)
            set_run_font(ri, 12, False, True)
            table = doc.add_table(rows=1 + len(rows), cols=len(headers))
            table.autofit = False
            table.allow_autofit = False
            for i, h in enumerate(headers):
                cell = table.rows[0].cells[i]
                cell.text = ""
                cp = cell.paragraphs[0]
                set_paragraph(cp, "left", False, 0, 0, 1)
                rr = cp.add_run(h)
                set_run_font(rr, 10, True)
                shade_header(cell)
                set_cell_border(cell)
                cell.width = Cm(widths[i])
            for r_i, row in enumerate(rows, start=1):
                for c_i, value in enumerate(row):
                    cell = table.rows[r_i].cells[c_i]
                    cell.text = ""
                    cp = cell.paragraphs[0]
                    set_paragraph(cp, "left", False, 0, 0, 1)
                    bold = row[0] == "Total"
                    rr = cp.add_run(value)
                    set_run_font(rr, 10, bold)
                    set_cell_border(cell)
                    cell.width = Cm(widths[i] if False else widths[c_i])
                prevent_row_split(table.rows[r_i])
            prevent_row_split(table.rows[0])
            continue

    core = doc.core_properties
    core.author = "Braynner David Rodríguez Obredor"
    core.title = TITLE
    core.language = "es-CO"
    core.category = "Trabajo guiado FORMATEC"
    path = OUT / "Trabajo_Alternativo_Practica_Grado_FORMATEC.docx"
    doc.save(path)
    return path


class TrabajoPDF(FPDF):
    def header(self):
        self.set_font("TimesNew", "", 9)
        self.set_y(1.0)
        if self.page_no() == 1:
            self.cell(0, 0.45, str(self.page_no()), align="R", new_x="LMARGIN", new_y="NEXT")
        else:
            self.cell(12, 0.45, "FORMATEC  ·  Trabajo guiado", align="L")
            self.cell(0, 0.45, str(self.page_no()), align="R", new_x="LMARGIN", new_y="NEXT")

    def footer(self):
        self.set_y(-1.3)
        self.set_font("TimesNew", "", 8)
        self.cell(0, 0.4, "Carrera 19 No. 24A-27, Soledad, Atlántico  ·  www.formatec.edu.co", align="C")


def write_pdf(blocks):
    pdf = TrabajoPDF(format="Letter", unit="cm")
    pdf.set_auto_page_break(auto=True, margin=2.2)
    pdf.set_margins(2.54, 2.2, 2.54)
    pdf.add_font("TimesNew", "", FONT)
    pdf.add_font("TimesNew", "B", FONTB)
    pdf.add_font("TimesNew", "I", FONTI)
    pdf.add_font("TimesNew", "BI", FONTBI)
    pdf.add_page()
    usable = 21.59 - 5.08

    def ensure(height):
        if pdf.get_y() + height > 25.5:
            pdf.add_page()

    def text(content, size=12, style="", align="J", indent=True, lh=None, before=0.08):
        if lh is None:
            lh = {14: 0.95, 12: 0.85, 11: 0.75, 10: 0.7, 9: 0.55, 8: 0.42}.get(size, 0.85)
        pdf.set_font("TimesNew", style, size)
        pdf.ln(before)
        if indent and align == "J":
            content = "     " + content
        pdf.multi_cell(usable, lh, content, align=align, new_x="LMARGIN", new_y="NEXT")

    for block in blocks:
        kind = block[0]
        if kind == "pb":
            pdf.add_page()
            continue
        if kind == "cover_h":
            pdf.ln(1.2)
            text(block[1], 14, "B", "C", False, 0.95, 0.2)
            continue
        if kind == "cover_title":
            text(block[1], 14, "B", "C", False, 0.95, 0.3)
            continue
        if kind == "cover":
            text(block[1], 12, "", "C", False, 0.85, 0.05)
            continue
        if kind == "cover_gap":
            pdf.ln(0.4)
            continue
        if kind == "h1":
            ensure(2.2)
            text(block[1], 14, "B", "C", False, 0.95, 0.35)
            continue
        if kind == "h2":
            ensure(1.6)
            text(block[1], 12, "B", "L", False, 0.85, 0.25)
            continue
        if kind == "p":
            text(block[1], 12, "", "J", True, 0.85, 0.08)
            continue
        if kind == "li":
            text("•  " + block[1], 12, "", "J", False, 0.85, 0.08)
            continue
        if kind == "ref":
            text(block[1], 12, "", "L", False, 0.85, 0.1)
            continue
        if kind == "table":
            _, caption_no, caption, headers, rows, widths = block
            ensure(2.5)
            text(caption_no, 12, "B", "L", False, 0.55, 0.3)
            text(caption, 11, "I", "L", False, 0.5, 0.02)
            pdf.set_font("TimesNew", "B", 8)
            scale = usable / sum(widths)
            col_w = [w * scale for w in widths]
            line_h = 0.42
            y0 = pdf.get_y()
            x0 = pdf.l_margin
            def row_height(values, font_style, font_size):
                pdf.set_font("TimesNew", font_style, font_size)
                heights = []
                for i, value in enumerate(values):
                    lines = pdf.multi_cell(col_w[i], line_h, value, dry_run=True, output="LINES")
                    heights.append(max(1, len(lines)) * line_h)
                return max(heights)

            def draw_row(values, font_style, font_size, fill=False):
                nonlocal y0
                h = row_height(values, font_style, font_size)
                if y0 + h > 25.6:
                    pdf.add_page()
                    y0 = pdf.get_y()
                x = x0
                pdf.set_font("TimesNew", font_style, font_size)
                for i, value in enumerate(values):
                    pdf.set_xy(x, y0)
                    if fill:
                        pdf.set_fill_color(232, 232, 232)
                    pdf.rect(x, y0, col_w[i], h, style="DF" if fill else "D")
                    pdf.multi_cell(col_w[i], line_h, value, align="L", new_x="RIGHT", new_y="TOP")
                    x += col_w[i]
                y0 += h
                pdf.set_y(y0)

            draw_row(headers, "B", 8, True)
            for row in rows:
                style = "B" if row[0] == "Total" else ""
                draw_row(list(row), style, 8, False)
            pdf.ln(0.2)
            continue

    path = OUT / "Trabajo_Alternativo_Practica_Grado_FORMATEC.pdf"
    pdf.output(str(path))
    return path, pdf.pages_count


def write_support(refs):
    evid = OUT / "Evidencias_y_Anexos"
    fuentes = OUT / "Fuentes_y_Referencias"
    evid.mkdir(exist_ok=True)
    fuentes.mkdir(exist_ok=True)
    (evid / "Inventario_de_evidencias.txt").write_text(
        "\n".join([
            "INVENTARIO DE EVIDENCIAS USADAS EN EL TRABAJO GUIADO",
            "",
            "1. Certificación laboral del CEA VIP del Norte, 19 de noviembre de 2026.",
            "   Archivo original del estudiante. No se reproduce en el trabajo porque contiene",
            "   el número de cédula y la firma. El número de documento sí va en la portada,",
            "   como lo pide el formato de FORMATEC.",
            "",
            "2. Malla de formación de Luz Verde: 10 cursos y 57 lecciones.",
            "   Resumida en la Tabla 1 y en el Anexo A del trabajo.",
            "",
            "3. Matriz de peligros del cargo de instructor de motocicleta, CEA VIP.",
            "   Archivo de trabajo. El Anexo B recoge la nota de validación y las medidas",
            "   propuestas. La valoración está marcada como propuesta.",
            "",
            "4. Circular académica 005-2026 y formato de trabajo guiado (versión 001), FORMATEC.",
            "",
            "Los anexos van dentro del documento. Este inventario solo orienta la revisión.",
            "",
        ]),
        encoding="utf-8",
    )
    (fuentes / "Referencias_APA.txt").write_text("\n\n".join(refs) + "\n", encoding="utf-8")


def word_counts(blocks):
    current = "Portada"
    counts = {}
    for block in blocks:
        kind = block[0]
        if kind == "h1":
            current = block[1]
        if kind in ("p", "li", "ref", "cover", "cover_title", "h2"):
            counts[current] = counts.get(current, 0) + len(block[1].split())
    return counts


def main():
    OUT.mkdir(exist_ok=True)
    blocks, refs = build()
    docx_path = write_docx(blocks)
    pdf_path, pages = write_pdf(blocks)
    write_support(refs)
    print("DOCX", docx_path)
    print("PDF", pdf_path, "PAGES", pages)
    for name, n in word_counts(blocks).items():
        print(f"WORDS {n:4}  {name}")


if __name__ == "__main__":
    main()
