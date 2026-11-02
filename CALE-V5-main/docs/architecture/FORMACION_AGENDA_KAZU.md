# Formación, agenda central y Kazu (bot-first)

## Objetivo

Conectar programación escolar, Mi formación del estudiante y Kazu sin duplicar reglas de elegibilidad ni reescribir módulos de teoría / examen / práctica.

## Contratos

| Pieza | Rol |
| --- | --- |
| `StudentFormationFacade` + `StudentFormationStatusDto` | Agrega dashboards existentes; el frontend no calcula elegibilidad. |
| `GET /api/student/formation` | Fuente única para `/student/formacion` y herramienta `mi_siguiente_paso`. |
| `SchoolOpsAlertService` | Alertas por reglas (cupos vs demanda, sin cita, estancados). |
| `GET .../ops-alerts` | Alimenta agenda escuela y Quick `alertas_operativas`. |
| `SchoolAgendaService` | Agenda unificada (teoría / examen / práctica); UI día/semana/mes. |

## Kazu (0 LLM primero)

1. Chips Quick → `AssistantService.QuickAsync` (sin cuota LLM).
2. Router de intención (`TryMapIntentToQuickKey`) → mismos tools.
3. Guías instantáneas (`TryInstantGuidance`).
4. Solo entonces LLM + toolbox (confirmación en escrituras).

Herramientas nuevas de lectura: `mi_siguiente_paso`, `alertas_operativas`, `que_estudiar` (readiness + errores + lecciones débiles).

## Bots (hosted)

| Bot | Config | Qué hace |
| --- | --- | --- |
| `FormationNudgeBot` | `FormationOps:EnableFormationNudgeBot` | Nudge diario si puede agendar y no tiene próximas. |
| `PracticalReminderBot` | `FormationOps:EnablePracticalReminderBot` | Recordatorio 1–36 h antes de manejo/examen. |
| `SchoolOpsAlertBot` | `FormationOps:EnableSchoolOpsAlertBot` | Notifica alertas `high` a la escuela 1×/día. |

Lógica compartida: `FormationOpsBotRunner` + `DedupeKey` en notificaciones.

## Legal / memoria

No se añadió memoria conversacional durable. Ver `docs/legal/PENDIENTE_SISTEMA_LEGAL.md`.

## Reprogramación y lista de espera

- Teoría / manejo: `RescheduleReservationAsync` (locks; estado `Rescheduled` solo tras asegurar el nuevo).
- Examen teórico: `RescheduleAsStudentAsync` vía `BookCoreAsync(ReplaceBookingId)` en la misma TX.
- Lista de espera (`ScheduleWaitlistEntry`): teoría, manejo y examen; oferta 2 h; confirm con elegibilidad; sin auto-reserva.
- Mi formación lista todas las entradas y confirma según `kind`.
- Kazu: `reprogramar_*`, `lista_espera_*`, `confirmar_lista_espera_*` (teoría / manejo / examen).

## Agenda operativa

- Creación rápida in-agenda (teoría / manejo / examen) reutilizando `createSession` / `createLesson` / `saveSlot`.
- Cancelación / cierre in-agenda: teoría (`cancelSession`), manejo (`cancelLesson`), examen (`saveSlot` cerrado + `cancelBookings` si aplica).
- Ítems cancelados/cerrados no se listan en la agenda unificada.
- Vista previa de cruces con ítems ya cargados; validación final en backend.
- Resumen de ocupación del rango + alertas altas.
- Duplicar semana de manejo (instructor/vehículo del catálogo cargado).
- Edición fina in-agenda: teoría (fecha/hora/tema/aula/cupos), manejo (fecha/hora/instructor/vehículo vía `PUT .../practical/lessons/{id}`), examen (hora/cupos).

## Instructor

- `GET /api/teacher/agenda` + página `/teacher/agenda` (solo lectura: teoría y manejo asignados).

## Estudio con Kazu

- Quick / intent `que_estudiar` → tool sin LLM: preparación, errores, lecciones a reforzar y rutas en la app.
- Quick / intent `practicar` → una pregunta en el chat (errores pendientes o reto del día) con opciones tocables.
- `POST /api/assistant/quiz/answer` responde vía `PlayService` y ofrece la siguiente (0 LLM).
- No inventa bancos: reutiliza mistakes / daily.

## Pendiente (no bloqueante de esta entrega)

- Jobs de reportes diarios pagos (siguen desactivados).
