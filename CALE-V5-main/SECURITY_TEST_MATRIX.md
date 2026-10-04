# Matriz de ataque controlado — Luz Verde (CALE-V5)

Cada fila es una prueba automática que levanta la API real (middleware, controladores, autorización) sobre una base
SQLite temporal y envía la petición "a mano", como lo haría un atacante con DevTools, Burp o cURL. **Nunca toca datos
reales:** el arnés usa el entorno `Testing`, fuerza SQLite y se niega a sembrar si la conexión no es el archivo temporal.

- Código: `tests/Cale.UnitTests/Security/Integration/` (`AttackMatrixTests`, `SessionAndRequestForgeryTests`, `RateLimitTests`).
- Ejecutar: `dotnet test tests/Cale.UnitTests --filter "FullyQualifiedName~Security.Integration"`.
- Último resultado: **45/45 superadas** (suite completa: 339 pruebas unitarias + 4 de arquitectura).

**Cuentas de prueba** (contraseña común solo en la base temporal):

| Cuenta | Rol | Escuela |
|--------|-----|---------|
| Admin_Global | Admin | — |
| School_Admin_1 / School_Admin_2 | School (es el tenant) | 1 / 2 |
| Teacher_School_1 / Teacher_School_2 | Teacher | 1 / 2 |
| Student_A_School_1 / Student_B_School_1 | Student | 1 |
| Student_C_School_2 | Student | 2 |
| Student_Inactive_School_1 | Student desactivado | 1 |

Datos: banco y pregunta **privados** de Teacher_School_2 (con solucionario), grupo de Teacher_School_2, intento abierto de
Student_A con esa pregunta, y la misma pregunta en la lista de "errores" de Student_A.

## Escalada vertical

| Petición | Usuario | Esperado | Actual | Estado | Severidad si fallara |
|----------|---------|----------|--------|--------|-----------|
| `GET /api/admin/users` | anónimo | 401 | 401 | ✅ | Crítica |
| `GET /api/admin/users` | Student_A / Teacher_1 / School_1 | 403 | 403 | ✅ | Crítica |
| `GET /api/admin/users` | Admin_Global | 200 | 200 | ✅ | — (control positivo) |
| `GET /api/school/members` | Student_A / Teacher_1 | 403 | 403 | ✅ | Alta |
| `POST /api/groups` | Student_A | 403 | 403 | ✅ | Media |
| `GET /api/questions/{id}` | Student_A | 403 | 403 | ✅ | Alta |

## JWT y sesión

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| JWT con rol editado a `Admin` (firma original) | Student_A | 401 | 401 | ✅ | Crítica |
| JWT firmado con otra clave | atacante | 401 | 401 | ✅ | Crítica |
| JWT `alg: none` sin firma | atacante | 401 | 401 | ✅ | Crítica |
| JWT con otra audiencia | Student_A | 401 | 401 | ✅ | Alta |
| JWT vencido | Student_A | 401 | 401 | ✅ | Alta |
| JWT válido de usuario desactivado | Student_Inactive | 401 | 401 | ✅ | Alta |
| `POST /api/auth/refresh` encadenado (rota y la nueva cookie sirve) | Teacher_1 | 200, 200 | 200, 200 | ✅ | Alta (P-A1) |
| Misma cookie de refresh canjeada 4 veces seguidas | Teacher_1 | 200, 200, 200, 401 | igual | ✅ | Media (P-A2) |
| Refresh rotado reutilizado tras la gracia de 60 s | Teacher_2 | 401 y **todas** sus sesiones revocadas | igual | ✅ | Alta (P-A2) |
| Refresh después de `logout` | Teacher_1 | 401 | 401 | ✅ | Alta |
| Cookie de refresh inventada | atacante | 401 | 401 | ✅ | Media |
| `POST /api/auth/login` ×12 con contraseña errónea | atacante | 429 al superar 10/min | 429 | ✅ | Media |

## Multi-tenant y BOLA/IDOR

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| `GET /api/school/members` | School_1 | sin correos de escuela 2 | sin correos de escuela 2 | ✅ | Alta |
| `PUT /api/school/members/{Student_C}` (nombre + contraseña) | School_1 | 403/404, sin cambios | 403/404, sin cambios | ✅ | Crítica |
| `GET /api/questions/{privada}` | Teacher_1 | 404 | 404 | ✅ | Alta |
| `GET /api/questions?bankId={privado}` | Teacher_1 | sin el texto privado | sin el texto privado | ✅ | Alta |
| `GET /api/questions/{privada}` | Teacher_2 (dueño) | 200 | 200 | ✅ | — (control positivo) |
| `GET /api/groups/{grupo T2}` y sus miembros | Teacher_1 | 403/404 | 403/404 | ✅ | Media |
| `POST /api/groups/{grupo T2}/members` | Teacher_1 | 403/404 | 403/404 | ✅ | Media |
| `POST /api/groups/{grupo propio}/members` con Student_C | Teacher_1 | 403 | 403 | ✅ | Media |
| `GET /api/exams/{intento A}/review` | Student_B | 403/404 | 403/404 | ✅ | Alta |
| `POST /api/exams/{intento A}/answer` y `/finish` | Student_B | 403/404 | 403/404 | ✅ | Alta |

## Manipulación de datos (mass assignment)

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| `POST /api/exams/{id}/finish` con `score`, `percent`, `passed` falsos | Student_B | ignorado, nota del servidor | `passed=false`, `percent=0` | ✅ | Crítica |
| `PUT /api/auth/me` con `role=Admin`, `schoolId`, `isPaid`, `isActive` | Student_B | ignorados | rol y escuela sin cambios | ✅ | Crítica |
| `POST /api/school/plan/activate` y `PUT /api/school/plan` con `plan`, `isPaid` | School_1 | 403 | 403 | ✅ | Alta |

## Fugas de respuestas y archivos

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| `POST /api/student/play/mistakes/answer` con pregunta de un examen abierto | Student_A | 409, sin clave ni solucionario | 409 | ✅ | **Crítica (P-C1)** |
| `GET /uploads/receipts/{guid}.png` | Student_A | 403 | 403 | ✅ | Alta |

## CSRF, cabeceras y respuestas seguras

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| `POST /api/auth/logout` con `Origin: https://evil.example` | navegador víctima | 403 | 403 | ✅ | Media (P-M1) |
| `POST /api/auth/logout` con `Sec-Fetch-Site: cross-site` | navegador víctima | 403 | 403 | ✅ | Media |
| `POST /api/auth/logout` desde el mismo origen | usuario | 204 | 204 | ✅ | — (control positivo) |
| `GET /api/auth/me` con Bearer y `Origin` ajeno | Student_A | 200 (lecturas no se bloquean) | 200 | ✅ | — |
| `GET /api/auth/me` | Student_A | `Cache-Control: no-store` | `no-store` | ✅ | Media (P-M4) |
| `GET /api/esto-no-existe/26`, `POST /api/no-existe/26` | cualquiera | 404 `application/problem+json` | igual | ✅ | Media (P-M5) |
| `GET /api/auth/me` sin cabecera `X-Cale-Wire` | Student_A | JSON plano (el cifrado **no** es un control) | JSON plano | ✅ | Informativo |
| `POST /api/push/subscriptions` a `https://10.0.0.5:8443/` | Student_A | 400 | 400 | ✅ | Baja (P-B1) |

## Cuentas y vinculación con escuelas (`SchoolLinkingTests`)

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| `POST /api/school/members`, `/members/attach`, `/imports/preview` | School_1 | 404/405 y ninguna cuenta creada | igual | ✅ | Media (P-M7) |
| `POST /api/school/invitations` y luego nada | School_1 | el usuario sigue sin escuela | sin escuela | ✅ | Media (P-M7) |
| Aceptar la invitación de otro / la escuela acepta su propia invitación / otra escuela la cancela | Student_C, School_1, School_2 | 403 | 403 | ✅ | Media |
| El invitado acepta | estudiante sin escuela | vinculado a School_1 | vinculado | ✅ | — (control positivo) |
| El invitado rechaza y luego intenta aceptar | estudiante sin escuela | 400, sigue sin escuela | igual | ✅ | Media |
| Solicitud del estudiante: otra escuela acepta / el propio estudiante acepta | School_2, estudiante | 403 | 403 | ✅ | Media |
| Invitar a un estudiante de otra escuela | School_1 | 404, sin cambios | igual | ✅ | Media |
| `PUT /api/auth/me` con otro `email` | Teacher sin escuela | 400 `email_change_disabled` | 400 | ✅ | Media (P-M8) |
| `PUT /api/school/members/{id}` con otro `email` | School_1 | 400, correo sin cambios | 400 | ✅ | Media (P-M8) |

## Pentest desde una cuenta Student (`StudentPentestTests`)

Barrido automático (`StudentPentestSweepTests`): enumera los 355 endpoints HTTP y envía 1.281 peticiones como anónimo y como Student_B con ids ajenos (Student_A, Student_C, Teacher_2, School_2, Admin, banco/pregunta privados, grupo de la escuela 2, intentos de A y C, notificación de A) en la ruta y en la query. Busca en cada respuesta correos ajenos, hashes de contraseña, la pregunta y el solucionario privados, el código del grupo ajeno y la notificación de A. Reporte en `%TEMP%/cale-student-pentest.txt`.

| Petición | Usuario | Esperado | Actual | Estado | Severidad |
|----------|---------|----------|--------|--------|-----------|
| Los 227 endpoints Admin/School/Teacher, todos los métodos | Student_B | 403 | 403 (8 subidas multipart dan 404 antes de autorizar) | ✅ | Alta |
| Endpoints protegidos sin sesión | anónimo | 401 | 401 | ✅ | Alta |
| 64 GET de Student con ids ajenos en ruta y query | Student_B | sin datos ajenos | sin datos ajenos | ✅ | Alta |
| 35 mutaciones de Student con id ajeno en la ruta | Student_B | sin efecto sobre otros | sin efecto (notificación de A intacta, sin unirse al grupo 2) | ✅ | Alta |
| `GET /api/exams/{intento de C o de A}/review`, `answer`, `finish` | Student_B | 403/404 sin claves | 403/404 | ✅ | Alta |
| `GET /api/exams/{intento abierto propio}/review` | Student_A | 4xx sin claves | 4xx | ✅ | Alta |
| `signs/check`, `daily/answer`, `mistakes/answer` con pregunta del examen abierto | Student_A | 4xx sin `correctOptionId` | 4xx | ✅ | Crítica (P-C1) |
| `POST /api/student/play/duel/{code}/answer` con pregunta del examen abierto | Student_A + Student_B | 409 `question_in_open_attempt` | 200 con la clave → **corregido**: 409 | ✅ | Media (P-M9) |
| Marcar leída / borrar la notificación de A | Student_B | sin cambios | sin cambios | ✅ | Media |
| `POST /api/groups/join` con el código del grupo de la escuela 2 | Student_B | 4xx, no se une | 4xx | ✅ | Media |
| `POST /api/exams/start` con el banco privado de Teacher_2 | Student_B | 403 `bank_not_visible` | 403 | ✅ | Alta |
| `POST /api/exams/start` banco oficial: payload y revisión antes de terminar | Student_B | sin `isCorrect` ni solucionario; revisión 4xx | igual; la revisión muestra la clave solo al terminar | ✅ | Alta |
| `GET/POST /api/staff/inactive-students` | Student_B | vacío o 4xx | vacío / `sent: 0` | ✅ | Media |
| Ranking global y `groupId` de otra escuela | Student_B | sin correos ni grupo ajeno | igual (nombre + inicial por diseño, P-B14) | ✅ | Baja |

## Pendientes sin prueba automática (decisión de negocio o cambio de UI)

| Caso | Motivo |
|------|--------|
| Puntaje del juego de señales enviado por el cliente (P-B6) | Solo gamificación |
