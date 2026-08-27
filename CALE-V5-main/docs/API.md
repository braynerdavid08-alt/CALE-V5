# CALE v5 — API (MVP)

Base: `http://127.0.0.1:5000`  
Auth: `Authorization: Bearer <jwt>`  
UI: español · API/DTOs: inglés

## Auth

| Método | Ruta | Acceso |
|--------|------|--------|
| POST | `/api/auth/login` | Anónimo |
| POST | `/api/auth/register` | Anónimo (solo Student) |
| POST | `/api/auth/register-teacher` | Anónimo (solo Teacher) |
| POST | `/api/auth/register-school` | Anónimo (Escuela + facturación + plan) |
| GET | `/api/auth/school-plans` | Anónimo |
| GET | `/api/auth/me` | Autenticado |
| POST | `/api/auth/change-password` | Autenticado |

## Escuela

| Método | Ruta | Acceso |
|--------|------|--------|
| GET | `/api/school/profile` | School (cupos + días de membresía) |
| GET | `/api/school/plans` | School |
| POST | `/api/school/plan/request` | School (solicitar plan; **no activa**) |
| PUT | `/api/school/billing` | School |
| GET | `/api/school/members` | School |
| POST | `/api/school/members` | School (crear Teacher/Student) |
| POST | `/api/school/members/attach` | School (vincular cuenta existente por correo) |
| PUT | `/api/school/members/{id}` | School |
| PATCH | `/api/school/members/{id}/active` | School |
| DELETE | `/api/school/members/{id}` | School (quita de la escuela; no borra la cuenta) |

La escuela **no puede** autoactivar (`PUT /plan` y `POST /plan/activate` → 403).

Flujo E2E: request → pago → `POST /api/school/plan/proof` → admin activate/reject.

Estados: `PendingPayment` → `PaymentSubmitted` → `Active` → `Expired` (o `Rejected`).

Planes (COP) y cupos:

| Plan | Precio | Docentes | Estudiantes |
|------|--------|----------|-------------|
| Mensual | $150.000 | 5 | 50 |
| Semestral | $800.000 | 12 | 150 |
| Anual | $1.500.000 | 25 | 400 |

Alta / solicitud queda en `PendingPayment` hasta que un **Admin** verifique el pago y active.

## Admin / usuarios

Roles: `Admin`, `School`, `Teacher`, `Student`.

| Método | Ruta | Acceso |
|--------|------|--------|
| GET | `/api/admin/dashboard` | Admin |
| GET | `/api/admin/users` | Admin |
| POST | `/api/admin/users/teachers` | Admin |
| PUT | `/api/admin/users/{id}` | Admin (nombre, correo, rol, contraseña opcional) |
| PATCH | `/api/admin/users/{id}/active` | Admin |
| DELETE | `/api/admin/users/{id}` | Admin (no puede borrarse a sí mismo) |
| GET | `/api/admin/memberships/pending` | Admin (solicitudes de escuela) |
| POST | `/api/admin/memberships/{schoolUserId}/activate` | Admin (verificar pago y activar) |
| POST | `/api/admin/memberships/{schoolUserId}/reject` | Admin (rechazar solicitud) |
| GET | `/api/admin/metrics` | Admin (métricas piloto P0) |
| GET | `/api/admin/results` | Admin |

Notas de membresía:
- Escuela solicita (`POST /api/school/plan/request`); si ya está Active, **sigue activa** y queda `RequestedPlanCode` pendiente.
- Cada request/activate/renew/reject escribe `MembershipEvents`.
- Login guarda `UltimoAccesoEn` (DAU/WAU/MAU).
- Intentos con `ExamId` se fuerzan a `Mode = exam`.

## Teacher / student dashboards

| Método | Ruta | Acceso |
|--------|------|--------|
| GET | `/api/student/dashboard` | Student |
| GET | `/api/student/results` | Student |
| GET | `/api/teacher/dashboard` | Teacher/Admin |
| GET | `/api/teacher/results` | Teacher/Admin |

## Catalog

Flujo de herencia: **Admin crea** → Escuela/Docente **leen** → Docente arma exámenes → Estudiante presenta.

Al arrancar, la API siembra (si faltan) dos bancos oficiales desde `SeedData/`:
`Normas de tránsito (Colombia)` (500) y `Reconocimiento visual de señales` (194).

| Método | Ruta | Acceso |
|--------|------|--------|
| GET | `/api/banks` | Admin/School/Teacher (lectura heredada) |
| POST/PUT | `/api/banks` | Admin |
| GET | `/api/questions`… | Admin/School/Teacher (catálogo completo) |
| POST/PUT | `/api/questions` | Admin |
| GET | `/api/questions/blocks` | Admin/School/Teacher |
| GET/POST/PUT | `/api/exams` | Teacher/Admin |
| GET | `/api/exams/published` | Auth (estudiante filtrado por grupo/ventana) |
| POST | `/api/exams/{id}/publish` | Teacher/Admin |
| POST | `/api/exams/{id}/assign` | Teacher/Admin |

## Assessment (intentos)

| Método | Ruta | Acceso |
|--------|------|--------|
| POST | `/api/exams/start` | Auth |
| POST | `/api/exams/{attemptId}/answer` | Dueño |
| POST | `/api/exams/{attemptId}/finish` | Dueño (incluye breakdown por tema/bloque + mejor marca) |
| GET | `/api/exams/{attemptId}/review` | Dueño post-finish |

## Ratings

| Método | Ruta | Acceso |
|--------|------|--------|
| POST | `/api/ratings` | Auth |
| GET | `/api/ratings` | Admin |
| PATCH | `/api/ratings/{id}` | Admin (reviewed/hidden) |

## Groups / classroom / notifications / media

| Método | Ruta | Acceso |
|--------|------|--------|
| GET/POST/PUT | `/api/groups`… | Según rol |
| POST | `/api/groups/join` | Student |
| * | `/api/classroom/...` | Aula por grupo |
| GET/POST | `/api/notifications`… | Auth |
| POST | `/api/media/upload` | Admin |

## Reglas fijas

- Aprobar ≥ 80%
- Tiempo de examen en servidor (`StartedAt`, `ExpiresAt`, `FinishedAt`)
- Respuestas correctas al estudiante solo en review post-finish
- Paginación estándar en listados de preguntas: `{ items, page, pageSize, totalItems, totalPages }`

## Fuera del MVP

`/api/analytics`, gamificación, plan de estudio, IA, calendario, `/api/requests`.
