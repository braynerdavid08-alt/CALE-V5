# -*- coding: utf-8 -*-
"""Genera propuesta-comercial-luz-verde.pdf a partir de marca y capturas reales."""
from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm, mm
from reportlab.platypus import (
    Image,
    KeepTogether,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[2]  # CALE-V5-main/
MONOREPO = ROOT.parent  # workspace root that may hold _capturas
OUT = Path(__file__).resolve().parent / "propuesta-comercial-luz-verde.pdf"

LOGO = ROOT / "frontend" / "public" / "brand" / "luz-verde-logo.png"
CAPTURA_DIR = MONOREPO / "_capturas"
if not CAPTURA_DIR.exists():
    CAPTURA_DIR = ROOT / "_capturas"

# Brand palette (Luz Verde: dark + green)
GREEN = colors.HexColor("#1B7A3A")
GREEN_SOFT = colors.HexColor("#E8F5EC")
DARK = colors.HexColor("#0F1712")
MUTED = colors.HexColor("#5A6B62")
LINE = colors.HexColor("#D5DDD8")
WHITE = colors.white


def img_path(*names: str) -> Path | None:
    for n in names:
        p = CAPTURA_DIR / n
        if p.exists():
            return p
    return None


def styles():
    base = getSampleStyleSheet()
    return {
        "cover_title": ParagraphStyle(
            "cover_title",
            parent=base["Normal"],
            fontName="Helvetica-Bold",
            fontSize=22,
            textColor=DARK,
            alignment=TA_CENTER,
            spaceAfter=6,
            leading=26,
        ),
        "cover_sub": ParagraphStyle(
            "cover_sub",
            parent=base["Normal"],
            fontName="Helvetica",
            fontSize=12,
            textColor=MUTED,
            alignment=TA_CENTER,
            spaceAfter=4,
            leading=16,
        ),
        "slogan": ParagraphStyle(
            "slogan",
            parent=base["Normal"],
            fontName="Helvetica-Oblique",
            fontSize=13,
            textColor=GREEN,
            alignment=TA_CENTER,
            spaceAfter=18,
        ),
        "h1": ParagraphStyle(
            "h1",
            parent=base["Normal"],
            fontName="Helvetica-Bold",
            fontSize=14,
            textColor=GREEN,
            spaceBefore=4,
            spaceAfter=10,
            leading=18,
        ),
        "h2": ParagraphStyle(
            "h2",
            parent=base["Normal"],
            fontName="Helvetica-Bold",
            fontSize=11,
            textColor=DARK,
            spaceBefore=8,
            spaceAfter=4,
            leading=14,
        ),
        "body": ParagraphStyle(
            "body",
            parent=base["Normal"],
            fontName="Helvetica",
            fontSize=9.5,
            textColor=DARK,
            alignment=TA_JUSTIFY,
            spaceAfter=6,
            leading=13,
        ),
        "bullet": ParagraphStyle(
            "bullet",
            parent=base["Normal"],
            fontName="Helvetica",
            fontSize=9.5,
            textColor=DARK,
            leftIndent=12,
            spaceAfter=3,
            leading=12.5,
        ),
        "note": ParagraphStyle(
            "note",
            parent=base["Normal"],
            fontName="Helvetica-Oblique",
            fontSize=8.5,
            textColor=MUTED,
            alignment=TA_JUSTIFY,
            spaceAfter=6,
            leading=11,
        ),
        "footer": ParagraphStyle(
            "footer",
            parent=base["Normal"],
            fontName="Helvetica",
            fontSize=8,
            textColor=MUTED,
            alignment=TA_CENTER,
        ),
        "cell": ParagraphStyle(
            "cell",
            parent=base["Normal"],
            fontName="Helvetica",
            fontSize=8.5,
            textColor=DARK,
            leading=11,
        ),
        "cell_h": ParagraphStyle(
            "cell_h",
            parent=base["Normal"],
            fontName="Helvetica-Bold",
            fontSize=8.5,
            textColor=WHITE,
            leading=11,
        ),
        "cta": ParagraphStyle(
            "cta",
            parent=base["Normal"],
            fontName="Helvetica-Bold",
            fontSize=11,
            textColor=GREEN,
            alignment=TA_CENTER,
            spaceBefore=8,
            spaceAfter=6,
            leading=15,
        ),
    }


def footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.5)
    y = 1.4 * cm
    canvas.line(2 * cm, y + 10, A4[0] - 2 * cm, y + 10)
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(MUTED)
    canvas.drawString(2 * cm, y, "Luz Verde — Formación que impulsa tu camino.")
    canvas.drawRightString(A4[0] - 2 * cm, y, f"{doc.page}")
    canvas.restoreState()


def shot(path: Path | None, max_w=16 * cm, max_h=7.2 * cm, caption: str | None = None, S=None):
    flow = []
    if path and path.exists():
        im = Image(str(path))
        iw, ih = im.imageWidth, im.imageHeight
        scale = min(max_w / iw, max_h / ih)
        im.drawWidth = iw * scale
        im.drawHeight = ih * scale
        flow.append(im)
        if caption and S:
            flow.append(Paragraph(caption, S["note"]))
    elif S:
        box = Table(
            [[Paragraph(
                f"<i>[Espacio para captura real: {caption or 'pantalla Luz Verde'}]</i>",
                S["note"],
            )]],
            colWidths=[max_w],
        )
        box.setStyle(
            TableStyle(
                [
                    ("BOX", (0, 0), (-1, -1), 0.6, LINE),
                    ("BACKGROUND", (0, 0), (-1, -1), GREEN_SOFT),
                    ("TOPPADDING", (0, 0), (-1, -1), 28),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 28),
                    ("LEFTPADDING", (0, 0), (-1, -1), 12),
                    ("RIGHTPADDING", (0, 0), (-1, -1), 12),
                ]
            )
        )
        flow.append(box)
    return flow


def price_table(S):
    data = [
        [
            Paragraph("Plan", S["cell_h"]),
            Paragraph("Precio", S["cell_h"]),
            Paragraph("Observación", S["cell_h"]),
        ],
        [
            Paragraph("Mensual", S["cell"]),
            Paragraph("<b>$500.000 COP</b>", S["cell"]),
            Paragraph("Pago mes a mes", S["cell"]),
        ],
        [
            Paragraph("Anual", S["cell"]),
            Paragraph("<b>$5.000.000 COP</b>", S["cell"]),
            Paragraph("Ahorro de $1.000.000 frente a 12 × $500.000", S["cell"]),
        ],
    ]
    t = Table(data, colWidths=[3.5 * cm, 4.5 * cm, 8.5 * cm])
    t.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), GREEN),
                ("BACKGROUND", (0, 1), (-1, 1), WHITE),
                ("BACKGROUND", (0, 2), (-1, 2), GREEN_SOFT),
                ("GRID", (0, 0), (-1, -1), 0.4, LINE),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("TOPPADDING", (0, 0), (-1, -1), 7),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
            ]
        )
    )
    return t


def feat_table(rows, S):
    data = [[
        Paragraph("Función", S["cell_h"]),
        Paragraph("Quién la usa", S["cell_h"]),
        Paragraph("Beneficio práctico", S["cell_h"]),
    ]]
    for a, b, c in rows:
        data.append([
            Paragraph(a, S["cell"]),
            Paragraph(b, S["cell"]),
            Paragraph(c, S["cell"]),
        ])
    t = Table(data, colWidths=[4.2 * cm, 3.8 * cm, 8.5 * cm])
    style_cmds = [
        ("BACKGROUND", (0, 0), (-1, 0), GREEN),
        ("GRID", (0, 0), (-1, -1), 0.35, LINE),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
    ]
    for i in range(1, len(data)):
        if i % 2 == 0:
            style_cmds.append(("BACKGROUND", (0, i), (-1, i), GREEN_SOFT))
    t.setStyle(TableStyle(style_cmds))
    return t


def build():
    S = styles()
    doc = SimpleDocTemplate(
        str(OUT),
        pagesize=A4,
        leftMargin=2 * cm,
        rightMargin=2 * cm,
        topMargin=1.6 * cm,
        bottomMargin=2.2 * cm,
        title="Propuesta comercial — Luz Verde",
        author="Luz Verde",
    )
    story = []

    # ===== PAGE 1 COVER =====
    story.append(Spacer(1, 1.2 * cm))
    if LOGO.exists():
        logo = Image(str(LOGO))
        logo.drawWidth = 9 * cm
        logo.drawHeight = 9 * cm * (logo.imageHeight / logo.imageWidth)
        story.append(logo)
    story.append(Spacer(1, 0.4 * cm))
    story.append(Paragraph("Formación que impulsa tu camino.", S["slogan"]))
    story.append(Paragraph("Propuesta comercial", S["cover_title"]))
    story.append(
        Paragraph(
            "Solución digital para la gestión y formación<br/>de Centros de Enseñanza Automovilística",
            S["cover_sub"],
        )
    )
    story.append(Spacer(1, 0.35 * cm))
    story.append(Paragraph("Documento B2B · Colombia · Octubre 2026", S["cover_sub"]))
    story.append(Spacer(1, 0.5 * cm))
    story.extend(
        shot(
            img_path("diseno-escuela.png", "appr-pc-list.png"),
            max_w=15.5 * cm,
            max_h=8 * cm,
            caption="Captura real: panel de escuela en Luz Verde.",
            S=S,
        )
    )
    story.append(Spacer(1, 0.4 * cm))
    story.append(
        Paragraph(
            "Dirigido a propietarios, gerentes y administradores de CEA.<br/>"
            "<b>Contacto comercial por definir.</b>",
            S["cover_sub"],
        )
    )
    story.append(PageBreak())

    # ===== PAGE 2 CHALLENGE =====
    story.append(Paragraph("1. El desafío de los CEA", S["h1"]))
    story.append(
        Paragraph(
            "En muchas escuelas de conducción la operación diaria combina hojas de cálculo, "
            "mensajes de WhatsApp, carpetas físicas y la memoria del equipo. Ese modelo puede "
            "funcionar con pocos aprendices; se vuelve frágil cuando crece la operación, rota el "
            "personal o hay que responder con rapidez.",
            S["body"],
        )
    )
    story.append(Paragraph("Preguntas frecuentes en el día a día:", S["h2"]))
    for q in [
        "¿Dónde está la información completa de este estudiante?",
        "¿Qué clases, cupos y exámenes tengo esta semana?",
        "¿Cuántas horas de teoría o práctica lleva el aprendiz?",
        "¿Ya está listo para presentar el examen teórico?",
        "¿Cómo reduzco tareas repetitivas sin perder control?",
    ]:
        story.append(Paragraph(f"• {q}", S["bullet"]))
    story.append(Spacer(1, 0.2 * cm))
    story.append(
        Paragraph(
            "Estos son desafíos habituales del sector, no un diagnóstico de su escuela en particular. "
            "La pregunta comercial es si conviene concentrar esa operación en una sola plataforma.",
            S["body"],
        )
    )
    story.extend(
        shot(
            img_path("appr-pc-dossier.png", "appr-pc-list.png"),
            max_h=7.5 * cm,
            caption="Captura real: expediente / listado de aprendices.",
            S=S,
        )
    )
    story.append(PageBreak())

    # ===== PAGE 3 SOLUTION =====
    story.append(Paragraph("2. La solución: Luz Verde", S["h1"]))
    story.append(
        Paragraph(
            "<b>Luz Verde</b> es una plataforma digital de gestión y formación vial para "
            "Centros de Enseñanza Automovilística. Reúne, en un mismo entorno, lo que hoy suele "
            "estar repartido entre archivos, chats y herramientas sueltas.",
            S["body"],
        )
    )
    story.append(Paragraph("Cómo se usa, en la práctica:", S["h2"]))
    for t in [
        "<b>Escuela:</b> agenda, aprendices, asistencia, teoría, práctica, exámenes y resultados.",
        "<b>Instructor:</b> aula en vivo, presentaciones, grupos, biblioteca e informes.",
        "<b>Estudiante:</b> simulador tipo examen, cursos, progreso y agenda autorizada.",
        "<b>Asistente de IA:</b> apoyo a consultas académicas y operativas, con límites diarios transparentes.",
    ]:
        story.append(Paragraph(f"• {t}", S["bullet"]))
    story.append(
        Paragraph(
            "La escuela invita o acepta a instructores y estudiantes; cada persona entra con su "
            "cuenta. La información queda centralizada y visible según el rol. No hace falta "
            "conocer arquitectura técnica: el valor está en organizar la operación y la formación.",
            S["body"],
        )
    )
    story.extend(
        shot(
            img_path("student-home-pc.png", "auto-home-pc.png", "diseno-estudiante-celular.png"),
            max_h=7.2 * cm,
            caption="Captura real: experiencia del estudiante.",
            S=S,
        )
    )
    story.append(PageBreak())

    # ===== PAGE 4 FEATURES =====
    story.append(Paragraph("3. Funcionalidades principales", S["h1"]))
    story.append(
        Paragraph(
            "Solo se presentan capacidades verificadas en el producto. No se anuncian módulos futuros como disponibles.",
            S["note"],
        )
    )
    story.append(Paragraph("Gestión de la escuela", S["h2"]))
    story.append(
        feat_table(
            [
                ("Agenda semanal", "Administración", "Ver teoría, práctica y examen en una vista"),
                ("Aprendices y expediente", "Administración", "Estado, progreso y abonos sin buscar en archivos"),
                ("Asistencia del día", "Escuela / instructores", "Registro claro de asistencia a teoría"),
                ("Teoría y manejo / flota", "Escuela", "Programar y seguir la operación formativa"),
                ("Exámenes (cupos y control)", "Escuela", "Citas, cupos y sala de examen en curso"),
                ("Resultados", "Escuela / instructores", "Desempeño académico visible"),
                ("Invitar usuarios", "Escuela", "Vincular personas con consentimiento"),
                ("Importación Excel", "Administración", "Cargar información sin reescribir todo"),
            ],
            S,
        )
    )
    story.append(Paragraph("Formación e instructores", S["h2"]))
    story.append(
        feat_table(
            [
                ("Aula en vivo", "Instructor y estudiantes", "Clase sincronizada en la plataforma"),
                ("Presentaciones y grupos", "Instructor", "Material y actividades organizados"),
                ("Simulador y evaluaciones", "Estudiante", "Práctica tipo examen CALE"),
                ("Cursos y biblioteca jurídica", "Estudiante / instructor", "Contenido de formación y normas"),
                ("Progreso, retos y ranking", "Estudiante", "Seguimiento y motivación al estudio"),
                ("Asistente de IA", "Estudiante y escuela", "Apoyo con cuota diaria de mensajes"),
            ],
            S,
        )
    )
    story.append(Paragraph("Qué no se presenta como disponible hoy", S["h2"]))
    for t in [
        "Pasarela automática de pagos (PSE / tarjeta).",
        "Facturación electrónica DIAN generada por la plataforma.",
        "Cobro automático por exceso de uso de inteligencia artificial.",
        "Creación masiva de cuentas por la escuela sin consentimiento del usuario.",
    ]:
        story.append(Paragraph(f"• {t}", S["bullet"]))
    story.append(PageBreak())

    # ===== PAGE 5 BENEFITS =====
    story.append(Paragraph("4. Beneficios para la escuela", S["h1"]))
    story.append(Paragraph("Capacidades demostrables del producto", S["h2"]))
    for t in [
        "Organización de la información de aprendices y de la operación diaria.",
        "Visibilidad del progreso académico y de los resultados.",
        "Gestión centralizada: escuela, instructor y estudiante en un mismo sistema.",
        "Experiencia digital de estudio con simulador, cursos y aula en vivo.",
        "Apoyo al aprendizaje con inteligencia artificial sujeta a límites transparentes.",
        "Información disponible para autorizar exámenes o revisar el día a día.",
    ]:
        story.append(Paragraph(f"• {t}", S["bullet"]))
    story.append(Paragraph("Resultados que dependen del uso", S["h2"]))
    story.append(
        Paragraph(
            "La plataforma facilita la organización. El impacto concreto —menos tiempo en tareas "
            "repetitivas, mejor preparación de los aprendices— depende de la adopción del equipo "
            "y de la disciplina de uso. <b>No prometemos porcentajes de ahorro, aumentos de "
            "matrícula ni rentabilidad garantizada.</b>",
            S["body"],
        )
    )
    story.extend(
        shot(
            img_path("asistente-pc.png", "diseno-instructor.png"),
            max_h=7 * cm,
            caption="Captura real: asistente / herramientas de formación.",
            S=S,
        )
    )
    story.append(PageBreak())

    # ===== PAGE 6 PRICING =====
    story.append(Paragraph("5. Planes y precios", S["h1"]))
    story.append(
        Paragraph(
            "Precios comerciales de referencia, alineados con el catálogo del producto "
            "(SchoolPlans / CommercialPlans: Mensual $500.000 · Anual $5.000.000).",
            S["body"],
        )
    )
    story.append(Spacer(1, 0.15 * cm))
    story.append(price_table(S))
    story.append(Spacer(1, 0.25 * cm))
    story.append(
        Paragraph(
            "El plan anual representa un <b>ahorro de $1.000.000 COP</b> frente a doce mensualidades "
            "a $500.000.",
            S["body"],
        )
    )
    story.append(Paragraph("Qué incluye la suscripción", S["h2"]))
    for t in [
        "Acceso a los paneles de escuela, instructores y estudiantes vinculados.",
        "Herramientas de gestión y formación descritas en este documento.",
        "Asistente de IA con límites diarios; al llegar al tope se invita a contactar al administrador para revisar una ampliación.",
        "No hay cobro automático de excesos de IA.",
    ]:
        story.append(Paragraph(f"• {t}", S["bullet"]))
    story.append(Paragraph("Condiciones por confirmar antes de formalizar", S["h2"]))
    for t in [
        "Límites de estudiantes, instructores u otras condiciones de cupo (el catálogo técnico no fija un tope comercial anunciable como «ilimitado»).",
        "Si el precio incluye impuestos aplicables — <b>pendiente de confirmación comercial y contable</b>.",
        "Medio de pago, permanencia, renovación, cancelación y reembolsos.",
        "Canal y horario de soporte.",
    ]:
        story.append(Paragraph(f"• {t}", S["bullet"]))
    story.append(
        Paragraph(
            "Confirmar un pago dentro de Luz Verde es un control administrativo interno. "
            "No equivale a emitir factura electrónica ni reemplaza obligaciones tributarias.",
            S["note"],
        )
    )
    story.append(PageBreak())

    # ===== PAGE 7 IMPLEMENTATION =====
    story.append(Paragraph("6. Implementación y acompañamiento", S["h1"]))
    story.append(Paragraph("Lo que el producto ya permite", S["h2"]))
    for t in [
        "Registro y aprobación de la escuela.",
        "Asignación de plan y vigencia por el administrador de Luz Verde.",
        "Acceso de cortesía o transición, cuando se acuerde.",
        "Vinculación de instructores y estudiantes por invitación o solicitud.",
        "Uso inmediato de los módulos de gestión y formación descritos.",
    ]:
        story.append(Paragraph(f"• {t}", S["bullet"]))
    story.append(Paragraph("Paquete comercial sugerido (pendiente de aprobación operativa)", S["h2"]))
    data = [
        [Paragraph("Actividad", S["cell_h"]), Paragraph("Estado", S["cell_h"])],
        [Paragraph("Configuración inicial de la escuela", S["cell"]), Paragraph("Sugerido", S["cell"])],
        [Paragraph("Habilitación / invitación de usuarios", S["cell"]), Paragraph("Producto + acompañamiento sugerido", S["cell"])],
        [Paragraph("Capacitación inicial", S["cell"]), Paragraph("Sugerido — alcance por definir", S["cell"])],
        [Paragraph("Acompañamiento en la puesta en marcha", S["cell"]), Paragraph("Sugerido — duración por definir", S["cell"])],
        [Paragraph("Soporte", S["cell"]), Paragraph("Canal y horario por definir", S["cell"])],
    ]
    t = Table(data, colWidths=[9 * cm, 7.5 * cm])
    t.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), GREEN),
                ("GRID", (0, 0), (-1, -1), 0.35, LINE),
                ("BACKGROUND", (0, 2), (-1, 2), GREEN_SOFT),
                ("BACKGROUND", (0, 4), (-1, 4), GREEN_SOFT),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("TOPPADDING", (0, 0), (-1, -1), 6),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
            ]
        )
    )
    story.append(t)
    story.append(Spacer(1, 0.3 * cm))
    story.append(
        Paragraph(
            "No se prometen tiempos de respuesta fijos ni capacitaciones ilimitadas sin acuerdo escrito.",
            S["note"],
        )
    )
    story.append(PageBreak())

    # ===== PAGE 8 CLOSE =====
    story.append(Paragraph("7. Próximo paso", S["h1"]))
    story.append(
        Paragraph(
            "Luz Verde concentra agenda, expediente, clases, examen y práctica del estudiante e "
            "instructor en un solo entorno, con precios claros y una inteligencia artificial "
            "incluida bajo límites transparentes.",
            S["body"],
        )
    )
    story.append(
        Paragraph(
            "Solicite una demostración de aproximadamente diez minutos. Verá el producto real "
            "—no un catálogo de promesas— y evaluaremos juntos si encaja con la operación de su CEA.",
            S["body"],
        )
    )
    story.append(Spacer(1, 0.3 * cm))
    cta_box = Table(
        [[
            Paragraph(
                "Agende su demostración<br/><br/>"
                "<font size='10' color='#5A6B62'>Contacto comercial por definir</font><br/>"
                "<font size='9' color='#5A6B62'>o el canal publicado en la página Contacto de Luz Verde</font>",
                S["cta"],
            )
        ]],
        colWidths=[16.5 * cm],
    )
    cta_box.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), GREEN_SOFT),
                ("BOX", (0, 0), (-1, -1), 1.2, GREEN),
                ("TOPPADDING", (0, 0), (-1, -1), 16),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 16),
            ]
        )
    )
    story.append(cta_box)
    story.append(Spacer(1, 0.6 * cm))
    story.extend(
        shot(
            img_path("diseno-escuela.png"),
            max_h=6.5 * cm,
            caption="Luz Verde — Formación que impulsa tu camino.",
            S=S,
        )
    )
    story.append(Spacer(1, 0.4 * cm))
    story.append(
        Paragraph(
            "Documento basado en funcionalidades verificadas en el producto. "
            "Precios según catálogo comercial vigente al 23 de octubre de 2026.",
            S["note"],
        )
    )

    doc.build(story, onFirstPage=footer, onLaterPages=footer)
    print(f"OK {OUT}")
    return OUT


if __name__ == "__main__":
    build()
