# Horarios de examen teórico, cupos, reservas y horas del estudiante

## 1. Lo que ya existe (y se reutiliza)

| Pieza | Dónde | Uso en este plan |
|---|---|---|
| `TheoryExamAppointment` (tabla `TheoryExamAppointments`) | `Cale.Modules.TheoreticalTraining/Domain/TheoryEntities.cs` | **Es la reserva** (`ExamBooking`). Se le agregan `Status`, `SeatNumber`, `CancelledAt`, `CancelledByUserId`, `BookedByUserId`. Una fila = un cupo ocupado. |
| Validaciones de quién puede presentar examen | `ApprenticeRegistryService.SaveExamSlotAsync` y `SchoolExcelImportService.ValidateExamSlotStudentAsync` (duplicadas) | Se unifican en `ExamBookingEligibility`. |
| Check-in / no-show / sala «Examen en curso» | `ApprenticeRegistryService` | Se conserva; ignora reservas canceladas. |
| Horas de teoría y taller | `TheoryTrainingService.ComputeHoursBreakdownAsync` (desde asistencia) | Se mueve a `TrainingHoursCalculator` y suma los **ajustes manuales**. No hay un segundo sistema de progreso. |
| Bloqueo de cupos en clases teóricas | `BeginTransactionAsync` + `pg_advisory_xact_lock` | Mismo patrón + índice único de puesto (`SeatNumber`). |
| `ColombiaTime` | `TheoreticalTraining/Application/ColombiaTime.cs` | Toda fecha/hora de examen es hora de Colombia (`DateOnly` + `TimeOnly`, sin zona). |
| Notificaciones (`INotificationPublisher`) | — | Avisos de reserva/cancelación. No se agrega SignalR (no se usa en agenda). |
| Esquema | Schema guards siempre activos en el arranque + migración EF | `ExamScheduleSchemaGuard` (Postgres + SQLite) y migración `AddExamScheduling` idempotente. |

## 2. Lo que faltaba

- Configuración semanal recurrente por escuela, con cupo (predeterminado 1).
- Excepciones por fecha: cerrar una hora, cambiar el cupo, horario extra, cerrar el día.
- Cálculo de disponibilidad real: `Disponible = Capacidad − ReservasActivas`.
- Reserva del estudiante, con protección del último cupo.
- Cancelación sin borrar (estado `Cancelled`).
- Ajuste manual de horas de teoría/taller con motivo.
- Auditoría de la escuela (no existía una general): tabla `SchoolAuditEntries`.
- Agenda semanal que junta clases teóricas, clases de manejo y exámenes.

## 3. Modelo de datos

```
TheoryExamScheduleTemplates   (recurrente)
  Id, SchoolUserId, DayOfWeek (0=domingo … 6=sábado), StartTime (time), Capacity (>=1, def 1),
  IsActive, CreatedAt, UpdatedAt
  UNIQUE (SchoolUserId, DayOfWeek, StartTime)

TheoryExamScheduleOverrides   (excepciones por fecha)
  Id, SchoolUserId, Date (date), StartTime (time; 00:00 si IsWholeDay), IsWholeDay, IsClosed,
  Capacity (null = usa el recurrente), Note, CreatedByUserId, CreatedAt, UpdatedAt
  UNIQUE (SchoolUserId, Date, StartTime)

TheoryExamAppointments        (reservas; tabla existente)
  + Status ('Active' | 'Cancelled' | 'NoShow' | 'Completed'), SeatNumber (1..Capacity),
  + CancelledAt, CancelledByUserId, BookedByUserId
  UNIQUE (SchoolUserId, ExamDate, SlotTime, SeatNumber) WHERE Status <> 'Cancelled'
  UNIQUE (SchoolUserId, StudentUserId, ExamDate, SlotTime) WHERE Status <> 'Cancelled' AND StudentUserId IS NOT NULL

TrainingHoursAdjustments      (ajustes manuales de horas)
  Id, SchoolUserId, StudentUserId, Category ('Theory' | 'Workshop'), DeltaHours, PreviousHours,
  NewHours, Reason, PerformedByUserId, CreatedAt

SchoolAuditEntries            (auditoría de la escuela)
  Id, SchoolUserId, ActorUserId, Area, Action, EntityType, EntityId, StudentUserId,
  Summary, OldValue, NewValue, Reason, CreatedAt
```

### Regla central

```
RECURRENTE (día + hora + cupo)  →  EXCEPCIONES DE LA FECHA  →  HORARIO EFECTIVO
HORARIO EFECTIVO  +  RESERVAS NO CANCELADAS  →  Ocupados / Disponibles / Estado
```

Estados de un horario: `Available` (Disponible), `Full` (Agotado), `Closed` (Cerrado), `Past` (Pasado).
Si hay reservas en una hora que ya no existe (se cerró o se borró el recurrente) la hora se muestra `Closed` con sus reservas.

### Concurrencia

Transacción + `pg_advisory_xact_lock(escuela, fecha·hora)` en Postgres (`BEGIN IMMEDIATE` en SQLite),
recuento de ocupados, elección del primer `SeatNumber` libre y el índice único como garantía final.
Si dos personas chocan, la segunda recibe `409 exam_slot_full` «El horario ya no tiene cupos disponibles.»

## 4. API (JSON camelCase; fechas `yyyy-MM-dd`, horas `HH:mm`, hora de Colombia)

### Escuela — `api/school/theory-exams` (rol School; SchoolId = usuario autenticado)

| Verbo | Ruta | Cuerpo / query | Respuesta |
|---|---|---|---|
| GET | `templates` | — | `ExamTemplateDto[]` |
| POST | `templates` | `{ daysOfWeek: number[], time: "09:00", capacity?: 1 }` | `ExamTemplateDto[]` (crea o reactiva; nunca duplica) |
| PUT | `templates/{id}` | `{ time, capacity, isActive }` | `ExamTemplateDto` |
| DELETE | `templates/{id}` | — | 204 (las reservas no se tocan) |
| GET | `week?from=&to=` | por defecto lunes–domingo de la semana actual; máx. 62 días | `ExamWeekDto` |
| PUT | `slots` | `{ date, time, capacity?: number, isClosed: boolean, note?, cancelBookings?: false }` | `ExamDayDto` (excepción de una hora; crea horario extra si no existe) |
| DELETE | `overrides/{id}` | — | `ExamDayDto` (vuelve a lo normal) |
| PUT | `days/{date}/close` | `{ note?, cancelBookings?: false }` | `ExamDayDto` |
| DELETE | `days/{date}/close` | — | `ExamDayDto` |
| POST | `bookings` | `{ date, time, studentUserId?, studentLabel?, notes? }` | `ExamSlotDto` |
| POST | `bookings/{id}/cancel` | `{ reason? }` | `ExamSlotDto` |
| GET | `audit?studentUserId=&area=&take=50` | — | `SchoolAuditEntryDto[]` |

Las rutas existentes (`schedule`, `students`, `control`, check-in, no-show) se mantienen.
`DELETE schedule/{id}` ahora **cancela** (no borra).

### Escuela — horas y agenda

| Verbo | Ruta | Cuerpo | Respuesta |
|---|---|---|---|
| GET | `api/school/apprentices/{studentUserId}/hours` | — | `StudentHoursDto` |
| POST | `api/school/apprentices/{studentUserId}/hours` | `{ category: "Theory" \| "Workshop", newHours: number, reason: string }` | `StudentHoursDto` |
| GET | `api/school/agenda?from=&to=` | máx. 31 días | `SchoolAgendaDto` |

### Estudiante — `api/student/theory/exams`

| Verbo | Ruta | Cuerpo | Respuesta |
|---|---|---|---|
| GET | `availability?from=&to=` | por defecto hoy … +13 días | `StudentExamAvailabilityDto` |
| POST | `book` | `{ date, time }` | `StudentExamBookingDto` |
| GET | `mine` | — | `StudentExamBookingDto[]` |
| POST | `{id}/cancel` | — | 204 |

### DTOs

```ts
ExamTemplateDto   { id, dayOfWeek, time, capacity, isActive }
ExamBookingDto    { id, studentUserId?, studentName, status, noShow, checkedInAt?, notes?,
                    bookedByStudent, createdAt }
ExamSlotDto       { date, time, capacity, occupied, available, status, source: 'template'|'extra'|'none',
                    isOverridden, overrideId?, templateId?, note?, bookings: ExamBookingDto[] }
ExamDayDto        { date, isClosed, closureOverrideId?, closureNote?, slots: ExamSlotDto[] }
ExamWeekDto       { from, to, hasTemplates, days: ExamDayDto[] }
StudentExamSlotDto        { date, time, available, status: 'Available'|'Full' }
StudentExamDayDto         { date, slots: StudentExamSlotDto[] }
StudentExamBookingDto     { id, date, time, status, canCancel, cancelDeadline? }
StudentExamAvailabilityDto{ from, to, canBook, blockReason?, myBooking?: StudentExamBookingDto,
                            minCancelHours, days: StudentExamDayDto[] }
StudentHoursDto   { studentUserId, studentName, theory: HoursLineDto, workshop: HoursLineDto,
                    history: HoursAdjustmentDto[] }
HoursLineDto      { required, attended, adjusted, total, pending }
HoursAdjustmentDto{ id, category, previousHours, newHours, deltaHours, reason, performedBy?, createdAt }
SchoolAuditEntryDto { id, area, action, summary?, oldValue?, newValue?, reason?, studentUserId?,
                      studentName?, actorName?, createdAt }
SchoolAgendaDto   { from, to, items: AgendaItemDto[] }
AgendaItemDto     { kind: 'theory'|'practical'|'exam', id, date, startTime, endTime?, title,
                    instructorName?, status, capacity, occupied, available,
                    students: { studentUserId?, name, status }[] }
```

Códigos de error nuevos: `exam_slot_full`, `exam_slot_closed`, `exam_slot_not_found`, `exam_slot_past`,
`exam_already_booked`, `exam_template_duplicate`, `capacity_invalid`, `capacity_below_bookings`,
`slot_has_bookings`, `exam_cancel_too_late`, `booking_not_found`, `hours_reason_required`,
`hours_invalid`, `hours_unchanged`, `exam_booking_too_soon`, `exam_booking_too_far`.

## 5. Frontend

- **Escuela**
  - `/school/theory-exams` — vista semanal (semana anterior / actual / siguiente), tarjetas por hora con
    cupo, ocupados, disponibles y estado; detalle de la hora (estudiantes, agregar, cancelar, cambiar cupo
    de esa fecha, cerrar/reabrir), cerrar día, horario extra.
  - `/school/theory-exams/schedule` — configuración semanal (lunes a domingo, agregar/editar/activar/desactivar/borrar, cupo).
  - `/school/agenda` — agenda de la semana (clases teóricas, de manejo y exámenes).
  - Expediente del estudiante — tarjeta «Horas» con ajuste manual y su historial.
- **Estudiante**
  - `/student/exam` — «Agendar examen»: semana con horas disponibles/agotadas, confirmar, mi reserva y cancelar.
  - Enlace desde la tarjeta «Cita de examen teórico» de `/student/training`.

## 6. Pruebas

Casos 1–18 del requerimiento en `tests/Cale.UnitTests/ExamScheduling/*` con SQLite real
(índices únicos y transacciones de verdad), incluida la reserva simultánea del último cupo.
