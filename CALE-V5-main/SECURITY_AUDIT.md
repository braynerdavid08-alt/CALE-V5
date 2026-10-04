# Auditoría de seguridad — Luz Verde (CALE-V5)

Fecha: 2026-10-03 · Alcance: backend ASP.NET Core 8 (`src/`), frontend Angular 18 (`frontend/`), configuración de despliegue (Render), dependencias.

Modelo: **Zero Trust**. Se asume que cualquier usuario autenticado (estudiante, instructor, escuela) puede ser malicioso, que el navegador está totalmente bajo su control (DevTools, Postman, scripts) y que todo lo que llega al navegador puede copiarse.

> Aclaración importante: el cifrado de respuestas (`WireEncryptionMiddleware`, cabecera `X-Cale-Wire`), el bloqueo de F12/clic derecho (`content-guard.service.ts`) y los IDs no secuenciales **no son controles de seguridad**. La clave de cifrado viaja en el bundle de JavaScript. Solo dificultan la copia casual. Toda la seguridad real de este documento está en el servidor.

Leyenda de estado: ✅ corregido en este cambio · ⚠️ mitigado parcialmente · ⏳ pendiente (requiere decisión o cambio mayor).

---

## CRITICAL

### C1 — Toma de control de cualquier cuenta por una escuela ✅
- **Ubicación / archivo:** `src/Cale.Modules.Identity/Application/Commands/SchoolMemberHandlers.cs` (`AttachSchoolMemberHandler`, `UpdateSchoolMemberHandler`).
- **Endpoints:** `POST /api/school/members/attach`, `PUT /api/school/members/{id}`.
- **Vulnerabilidad:** una escuela podía vincular cualquier cuenta existente por correo y después cambiarle la contraseña o el correo desde el panel de miembros.
- **Riesgo:** robo total de cuentas de otras escuelas, instructores o estudiantes independientes.
- **Escenario:** la escuela A conoce el correo de un instructor de la escuela B → lo "vincula" → le pone una contraseña nueva → entra como él.
- **Solución aplicada:** nuevo campo `User.CreatedBySchoolId` (columna `EscuelaCreadoraId`, creada por `UserCreatorSchemaGuard`). La escuela solo puede cambiar contraseña o correo de cuentas **que ella misma creó** y que siguen en su escuela (`User.CredentialsManagedBy`). Si no, responde 403 `credentials_not_managed`. Al cambiar la contraseña se revocan todas las sesiones del usuario.
- **Prioridad:** inmediata.

---

## HIGH

### H1 — Preguntas privadas y respuestas correctas legibles por cualquier instructor (IDOR) ✅
- **Archivo:** `Catalog/Application/Queries/ListQuestionsHandler.cs`, `GetQuestionHandler.cs`, `Infrastructure/CatalogStore.cs`, `Api/Controllers/QuestionsController.cs`.
- **Endpoints:** `GET /api/questions`, `GET /api/questions/{id}`.
- **Vulnerabilidad:** el listado y el detalle devolvían preguntas de bancos privados de otros instructores, con la opción correcta.
- **Escenario:** un instructor recorre `GET /api/questions/1..N` y descarga el banco privado de otro, con solucionario.
- **Solución:** filtro por propietario en la consulta (`BankOwnerId == null || == viewer`) y en el detalle 404 si el banco no es visible (`Bank.IsVisibleTo`). Paginación limitada (página ≤ 10 000, búsqueda ≤ 200 caracteres).

### H2 — Extracción masiva del banco mediante simulacros sin límite ✅ / ⚠️
- **Archivo:** `Assessment/Application/Commands/StartExamHandler.cs`.
- **Endpoint:** `POST /api/exams/start` y `GET /api/exams/{attemptId}/review`.
- **Vulnerabilidad:** `QuestionCount` y `TimeLimitMinutes` venían del cliente sin límites; un estudiante podía pedir 5 000 preguntas, terminar y ver la revisión con todas las respuestas. También podía mezclar el examen oficial de su escuela en una práctica, o enviar `Mode=Official`.
- **Solución:** práctica por banco limitada a 10–50 preguntas y 5–120 minutos; el modo siempre es `Practice` en este flujo; el examen oficial no se puede mezclar (`official_exam_not_mixable`); límite de 30 inicios / 10 min y 60 revisiones / 10 min por usuario.
- **Residual ⚠️:** la revisión sigue mostrando la respuesta correcta tras terminar (es una función pedagógica). Con los límites, extraer el banco completo requiere muchas horas y queda en los logs.

### H3 — "Aprobar" un intento con 1 sola pregunta ✅
- Mismo archivo que H2. El mínimo de 10 preguntas impide intentos triviales que cuenten como aprobados.

### H4 — XSS almacenado mediante archivos subidos (SVG/HTML con Content-Type falso) ✅
- **Archivos:** `MediaController.cs`, `PresentationsController.cs`, `CoursesController.cs`, `ProfilePhotoController.cs`, `SchoolController.cs` (comprobantes).
- **Endpoints:** `POST /api/media/upload`, `POST /api/presentations/upload`, `POST /api/courses/upload`, `POST /api/auth/me/photo`, `POST /api/school/plan/proof/upload`, y los `GET` que sirven esos archivos.
- **Vulnerabilidad:** se confiaba en el `Content-Type` y la extensión del cliente. Un `foto.png` que en realidad era HTML/SVG con `<script>` se servía desde nuestro dominio → robo de sesión de quien lo abriera.
- **Solución:** nuevo `Api/Infrastructure/MediaSniffer.cs` que decide el tipo por los *magic bytes*; SVG eliminado de cursos; `SafeMediaResponse` sirve todo con `nosniff`, CSP `sandbox` y, si no es imagen/vídeo/audio/PDF, como descarga `application/octet-stream`.

### H5 — Clave JWT débil solo generaba un aviso ✅
- **Archivo:** `Api/Extensions/WebApplicationExtensions.cs`, `ServiceCollectionExtensions.cs`.
- **Vulnerabilidad:** si `Jwt:Key` quedaba con el valor de ejemplo, la app arrancaba igual; cualquiera podría firmar tokens de Admin.
- **Solución:** fuera de Development la app **no arranca** si la clave tiene < 32 caracteres o contiene `CHANGE-ME`. Validación estricta: firma obligatoria, solo HS256, expiración obligatoria, `ClockSkew` 30 s.

### H6 — Comprobantes de pago públicos ✅
- **Archivo:** `Api/Middleware/CourseVideoGateMiddleware.cs`, `Identity/Domain/SchoolProfile.cs`.
- **Ruta:** `/uploads/receipts/*`, `POST /api/school/plan/proof`.
- **Vulnerabilidad:** los comprobantes (datos bancarios/personales) se servían a cualquiera con la URL; además la escuela podía registrar como comprobante una URL arbitraria (externa o `javascript:`).
- **Solución:** solo Admin o Escuela pueden descargarlos (401/403 para el resto), se sirven como adjunto `private, no-store`; la URL del comprobante debe ser una emitida por nuestro endpoint (`IsIssuedReceiptPath`).

### H7 — Sesiones que sobreviven a desactivación, borrado o cambio de rol ✅
- **Archivos:** `Api/Middleware/MustChangePasswordMiddleware.cs`, `Identity/Application/Commands/UpdateUserHandler.cs`, `SetUserActiveHandler.cs`, `DeleteUserHandler.cs`.
- **Vulnerabilidad:** un usuario desactivado, borrado o degradado seguía operando con su JWT hasta 60 min y renovándolo con el refresh token.
- **Solución:** cada petición autenticada a `/api` comprueba en BD que el usuario existe, está activo y conserva el rol del token (401 `session_revoked`). Al desactivar, borrar, cambiar rol, contraseña o correo se revocan todos los refresh tokens. Se registra auditoría (`Audit: admin X ...`).

### H8 — Autorregistro como Instructor o Escuela con acceso inmediato ⏳ (decisión de negocio)
- **Archivos:** `RegisterTeacherHandler.cs`, `RegisterSchoolHandler.cs`, `BuildingBlocks.Domain/Access/FreeAccessPolicy.cs` (`Access:FreeForAll`, por defecto `true`).
- **Endpoints:** `POST /api/auth/register-teacher`, `POST /api/auth/register-school`.
- **Riesgo:** con el modo gratuito activo, cualquiera crea una cuenta de instructor/escuela y obtiene acceso al banco oficial (lectura de catálogo) y a herramientas de exportación.
- **Solución propuesta:** aprobación manual por Admin de nuevas escuelas/instructores, o `Access__FreeForAll=false` en Render cuando termine el piloto. No se cambió porque altera el modelo comercial.

---

## MEDIUM

| ID | Hallazgo | Archivo / endpoint | Estado |
|----|----------|--------------------|--------|
| M1 | Instructor sin escuela podía agregar a su grupo a cualquier estudiante por correo (y ver su progreso) | `Classroom/Application/Commands/GroupCommandHandler.cs` · `POST /api/groups/{id}/members` | ✅ Solo estudiantes de su misma escuela |
| M2 | Aula en vivo: el host podía usar bancos privados ajenos y vincular presentaciones ajenas (que luego se mostraban a los participantes) | `LiveSessionHandler.CreateAsync`, `LiveController.Create` · `POST /api/live/sessions` | ✅ Se valida `Bank.IsVisibleTo` y acceso a la presentación |
| M3 | Cualquier instructor podía cambiar la configuración **global** de "100 Estudiantes Dijeron" | `GameShowController` · `PUT /api/game-show/settings`, `POST /api/game-show/settings/restore` | ✅ Solo Admin (los instructores ajustan cada partida) |
| M4 | Mensajes internos de excepción (cadena de conexión, SQL) devueltos al cliente en producción | `Api/Middleware/ExceptionHandlingMiddleware.cs` | ✅ Mensaje genérico fuera de Development; detalle solo en logs con `traceId` |
| M5 | Sin cabeceras de seguridad (CSP, X-Frame-Options, nosniff, HSTS, Referrer/Permissions-Policy) | `Api/Middleware/SecurityHeadersMiddleware.cs` (nuevo) | ✅ CSP `script-src 'self'`; script inline de tema movido a `theme-init.js` |
| M6 | Límite global de cuerpo de 200 MB en todos los endpoints (DoS de memoria) | `ServiceCollectionExtensions.cs` | ✅ 30 MB por defecto; los endpoints de subida declaran su propio límite |
| M7 | Rate limiting solo en login/registro/código | `ServiceCollectionExtensions.cs`, `RateLimitPolicies.cs` | ✅ Límite global por IP en `/api` y por usuario en inicio de examen, revisión, subidas, asistente, cambio de contraseña; por IP en refresh/logout y unión a salas |
| M8 | Inyección de fórmulas en CSV exportados (`=HYPERLINK(...)` en nombres) | `LiveSessionHandler.ExportResultsCsvAsync`, `GameShowHandler` export | ✅ `CsvCell.Escape` antepone `'` a celdas que empiezan por `= + - @` |
| M9 | `bypassSecurityTrustHtml` en la página "Nosotros" (HTML editable por Admin sin sanear) | `frontend/src/app/features/public/public-about.page.ts` | ✅ Se usa el saneador de Angular |
| M10 | Aula en vivo devuelve `isCorrect` al participante incluso en modo examen | `LiveSessionHandler` (respuesta a `answer`) | ⏳ Bajo impacto (preguntas ya visibles en pantalla); revisar al rediseñar modo examen |
| M11 | Puntaje de juegos (señales/duelos) lo envía el cliente | `Api/Services/Play/PlayService.cs` | ⏳ Solo afecta rankings de gamificación, no certificaciones (ver Fase 2, P-B6) |
| M12 | Lecciones: se pueden completar sin responder y `check` revela soluciones | `CourseService` | ⏳ Función pedagógica; no otorga certificados |
| M13 | GET anónimo de "100 Estudiantes Dijeron" expone código de unión e IDs | `GET /api/game-show/{id}` | ⏳ Las acciones de host sí exigen ser el dueño |
| M14 | Fuerza bruta de códigos de confirmación de correo | `POST /api/auth/confirm-email` | ⚠️ 10 intentos / 10 min por IP; falta bloqueo por cuenta |
| M15 | `X-Forwarded-For` con `KnownProxies` vacíos | `WebApplicationExtensions.cs` | ⚠️ ASP.NET solo toma el último salto (el que añade Render); documentado |
| M16 | Importaciones Excel/Word en memoria (zip bomb) | Importadores de exámenes/escuelas | ⚠️ Límite de tamaño por endpoint (2–50 MB) y rate limit; falta límite de descompresión |
| M17 | Instructores con acceso de lectura pueden exportar el banco oficial | Exportaciones de exámenes/presentaciones | ⏳ Decisión de negocio (ligado a H8) |

---

## LOW

| ID | Hallazgo | Estado |
|----|----------|--------|
| L1 | Enumeración de usuarios por mensajes distintos en registro/recuperación | ⏳ |
| L2 | No hay detección de reutilización de refresh token (rotación con gracia de 60 s) | ✅ Fase 2 (P-A2): máx. 2 reusos en la gracia; reuso posterior revoca todas las sesiones |
| L3 | `POST /api/auth/logout` exigía token válido: con el access token vencido no se revocaba el refresh | ✅ Ahora es anónimo y revoca usando la cookie |
| L4 | CSRF: cookie de acceso `SameSite=Lax` | ✅ Fase 2 (P-M1): `CsrfOriginMiddleware` valida `Origin`/`Sec-Fetch-Site` |
| L5 | En el flujo de autoconfirmación el JWT puede quedar en `localStorage` | ⏳ |
| L6 | CORS con dominios de ejemplo en `appsettings` | ✅ Fase 2: eliminados `tudominio.com` (registrable por terceros) |
| L7 | Cuotas del asistente IA en memoria (se reinician con cada despliegue) | ⚠️ Añadido límite 20/min por usuario |
| L8 | Endpoint de suscripción push acepta URLs arbitrarias (SSRF limitado) | ✅ Fase 2 (P-B1): solo hosts de servicios push reales |
| L9 | Cambio de correo sin verificación del nuevo correo | ✅ El correo de acceso ya no se puede cambiar (ver P-M8) |
| L10 | Borradores en `localStorage` no se borran al cerrar sesión | ⏳ |
| L11 | `X-Request-Id` sin límite de longitud | ⏳ |

---

## INFO (verificado correcto)

- **Secretos:** no hay claves reales en el repositorio ni en el historial revisado (solo valores de ejemplo `CHANGE-ME`). **No se requiere rotación por exposición en git.** Si alguna vez se compartió la `Jwt:Key` o la cadena de conexión de Render fuera de Render (capturas, chats), **debe rotarse**.
- Las respuestas correctas **no** se envían al iniciar un examen; la nota se calcula en el servidor.
- Rutas de archivos protegidas contra *path traversal*.
- Paginación con tope en servidor.
- Pagos/planes decididos en servidor (el cliente no puede activarse un plan).
- Contraseñas con hash (PBKDF2 de ASP.NET Identity); refresh tokens guardados como SHA-256.
- Cookies de sesión `HttpOnly`, `Secure` en HTTPS.
- Sin `eval` ni `new Function` en el bundle de producción; source maps desactivados en producción.

---

## Fase 2 — Auditoría profunda post-implementación

Modelo de atacante: controla todo el navegador (DevTools, Burp, cURL), tiene una cuenta legítima de cualquier rol y
modifica IDs, roles, cuerpos y cabeceras a mano. Se probó contra el pipeline HTTP real (`WebApplicationFactory`) sobre una
base SQLite desechable, con las cuentas `Student_A_School_1`, `Student_B_School_1`, `Student_C_School_2`, `Teacher_School_1`,
`Teacher_School_2`, `School_Admin_1`, `School_Admin_2`, `Admin_Global` y un estudiante desactivado. Matriz completa en
[`SECURITY_TEST_MATRIX.md`](SECURITY_TEST_MATRIX.md). No se proporcionó archivo HAR; el análisis se hizo sobre el bundle compilado y
el tráfico real de la API.

> **Incidente durante la auditoría (ya resuelto).** La primera ejecución del arnés usó el entorno `Development`, y
> `Program.cs` carga `appsettings.Development.local.json`, que apunta a la base **de producción** de Render. Se crearon 8
> usuarios `@pruebas.test` (IDs 128–135), 2 intentos, 1 banco, 1 pregunta, 2 grupos y 1 notificación. Se borraron
> todos en una transacción filtrando por esos IDs y correos (verificado: 0 restantes). El arnés ahora usa el entorno
> `Testing`, fuerza SQLite y se niega a sembrar datos si la conexión no es el archivo temporal.
> **Recomendación:** que `appsettings.Development.local.json` apunte a una base de desarrollo, no a producción; si ese archivo
> se compartió alguna vez, rotar la contraseña de la base en Render.

### CRÍTICO

**P-C1 — Los juegos de práctica revelaban la respuesta correcta de preguntas de un examen abierto** ✅
- **Dónde:** `Api/Services/Play/PlayService.cs` (`SyncMistakesAsync`, `AnswerMistakeAsync`, `AnswerDailyAsync`) y `PlayService.Pool.cs` (`CheckSignsQuestionAsync`).
- **Riesgo:** durante un examen (incluido el teórico oficial de la escuela) el estudiante respondía una pregunta, `GET /api/student/play/mistakes` la listaba si estaba mal, `POST /api/student/play/mistakes/answer` devolvía `correctOptionId`, y el estudiante cambiaba su respuesta del examen. Así sacaba 100 % en cualquier examen.
- **Reproducir:** Student_A inicia un examen, responde Q mal y llama `POST /api/student/play/mistakes/answer {questionId:Q, optionId:0}`. Antes: 200 con la clave. Ahora: **409 `question_in_open_attempt`**.
- **Corrección:** los errores solo se sincronizan desde intentos **terminados**, y los tres endpoints que revelan la clave rechazan preguntas que estén en un intento abierto del usuario.
- **Prueba:** `SessionAndRequestForgeryTests.Practice_mistakes_never_reveal_the_key_of_a_question_in_an_open_exam`.

### ALTO

**P-A1 — El refresh cerraba la conexión de la base del request (`ObjectDisposedException: NpgsqlConnection`)** ✅
- **Dónde:** `Modules.Identity/Infrastructure/Persistence/RefreshTokenStore.cs` (`await using var conn = _db.Database.GetDbConnection()`).
- **Riesgo:** el `await using` dispone la conexión del `DbContext`; la siguiente consulta del mismo request fallaba con 500 **después** de revocar el token, y el usuario quedaba deslogueado. Es el error que se vio en producción. Además un `catch {}` vacío ocultaba fallos de base como "token inválido".
- **Reproducir:** iniciar sesión y llamar `POST /api/auth/refresh` en PostgreSQL.
- **Corrección:** `OpenConnectionAsync`/`CloseConnectionAsync` de EF (sin disponer), `catch` limitado a `DbException` con log.
- **Prueba:** `Refresh_rotates_and_the_new_cookie_works` (refresh encadenado dos veces sobre el mismo host).

**P-A2 — Refresh reutilizable sin límite durante 60 s y sin detección de robo** ✅
- **Dónde:** `RefreshTokenStore.ConsumeAsync`; columnas nuevas `RotatedAt`, `GraceUses` (`RefreshTokenSchemaGuard`, también corre en producción con `ApplyFeatureSchema=false`).
- **Riesgo:** un token recién rotado se podía canjear ilimitadamente durante la gracia; cada canje creaba una sesión de 365 días independiente. Un token robado y rotado por el atacante no dejaba rastro.
- **Reproducir:** canjear la misma cookie `cale_refresh` 4 veces seguidas; o canjear un token rotado hace más de 60 s.
- **Corrección:** máximo 2 reusos dentro de la gracia (pestañas simultáneas). Si un token rotado aparece **fuera** de la gracia, se revocan todas las sesiones del usuario y se registra `Refresh token reuse detected` (una sola vez por token, para que una cookie vieja no cierre sesiones nuevas una y otra vez).
- **Pruebas:** `Rotated_refresh_is_only_accepted_a_couple_of_times_inside_the_grace_window`, `Replaying_a_rotated_refresh_after_the_grace_window_revokes_every_session_of_that_user`, `Refresh_after_logout_is_401`, `Garbage_refresh_cookie_is_401`.

### MEDIO

| ID | Hallazgo | Dónde | Estado / prueba |
|----|----------|-------|-----------------|
| P-M1 | CSRF dependía solo de `SameSite=Lax` | `Api/Middleware/CsrfOriginMiddleware.cs` (nuevo) | ✅ POST/PUT/PATCH/DELETE en `/api` con cookies se rechazan (403 `csrf_origin_rejected`) si `Origin` no es el propio sitio o un origen CORS, o si `Sec-Fetch-Site: cross-site`. Bearer y clientes sin esas cabeceras no se afectan. Pruebas `Cross_origin_state_change_with_cookies_is_rejected`, `Cross_site_fetch_metadata_is_rejected_even_without_origin`, `Same_origin_state_change_is_allowed` |
| P-M2 | Simulacro personalizado (mezcla) evitaba la restricción del examen oficial y permitía revisar sin límite la clave de exámenes de 1 intento | `Modules.Assessment/.../StartExamHandler.cs` | ✅ Usa `GetSchoolOfficialTheoryExamIdAsync` (antes solo bloqueaba si el estudiante ya estaba autorizado) y rechaza exámenes de un solo intento. El selector del simulador ya no los muestra |
| P-M3 | Reto diario usaba preguntas del examen oficial y de exámenes de 1 intento (devolvía la clave) | `PlayService.Pool.cs` `LoadSchoolQuestionIdsAsync` | ✅ Excluidos |
| P-M4 | Respuestas de `/api` sin `Cache-Control` (datos personales cacheables) | `SecurityHeadersMiddleware.cs` | ✅ `no-store` + `Pragma: no-cache` salvo que el endpoint fije su propia caché (medios públicos). Prueba `Private_api_responses_are_not_cacheable` |
| P-M5 | Rutas `/api` desconocidas devolvían `index.html` con 200 | `WebApplicationExtensions.cs` | ✅ 404 `application/problem+json`. Prueba `Unknown_api_route_is_a_json_problem` |
| P-M6 | Presupuesto diario del asistente IA compartido por todas las escuelas (2 cuentas lo agotaban) | `Api/Services/Assistant/AssistantService.cs`, `AssistantOptions.cs` | ✅ Topes de llamadas al proveedor por usuario (40/día) y por escuela (60/día), además del global. La API key sigue solo en servidor |
| P-M7 | Una escuela puede vincular por correo a cualquier estudiante/instructor sin escuela, sin su consentimiento, y los errores distinguen si el correo existe | `SchoolMemberHandlers.cs` (`attach`), `ImportSchoolMembersHandler.cs` | ✅ Eliminados crear/vincular/import CSV. La escuela ya no crea cuentas (tampoco el import Excel). Vinculación solo con consentimiento: el miembro solicita y la escuela acepta, o la escuela invita y el miembro acepta (`SchoolJoinRequestHandler`, `api/me/school-membership`, `api/school/invitations`). Pruebas: `SchoolLinkingTests` |
| P-M8 | Cambio de correo de acceso sin contraseña ni verificación del nuevo correo | `UpdateMyProfileHandler.cs` | ✅ Nadie cambia su correo de acceso: `PUT /api/auth/me` y `PUT /api/school/members/{id}` responden 400 `email_change_disabled`. Solo el administrador puede corregirlo como soporte |
| P-M9 | El duelo devolvía la opción correcta y la explicación de una pregunta que el jugador tenía en un examen abierto (evadía la protección de P-C1; basta un segundo estudiante que acepte el duelo) | `PlayController.AnswerDuel` | ✅ Aplica `EnsureNotInOpenAttemptAsync` como reto diario, errores y señales: 409 `question_in_open_attempt`. Prueba `Duel_does_not_reveal_the_key_of_a_question_in_the_players_open_exam` (falló con 200 antes del arreglo) |

### BAJO

| ID | Hallazgo | Estado |
|----|----------|--------|
| P-B1 | SSRF ciego vía suscripción push (`https://host-interno/`) | ✅ `PushController.IsValidEndpoint`: solo FCM, Mozilla, WNS y Apple, sin IPs ni puertos. Prueba `Push_subscription_to_an_internal_host_is_rejected` |
| P-B2 | Otro usuario podía quedarse con la suscripción push de un dispositivo conociendo su endpoint | ✅ `PushServices.SubscribeAsync` solo reasigna si las claves del navegador coinciden |
| P-B3 | `TimeMinutes` sin tope: un instructor podía guardar un examen que da 500 a todos (desbordamiento) | ✅ `Exam.Validate`: 1–1440 min |
| P-B4 | Estudiante retirado de un grupo seguía viendo la lista de exámenes del grupo | ✅ `ExamsController.Published` usa solo membresías activas |
| P-B5 | `client-errors` registraba URLs con query string (códigos/tokens) | ✅ Se recorta `?` y `#` antes de registrar |
| P-B6 | Puntaje del juego de señales lo envía el cliente; `signs/check` repetible | ⏳ Solo gamificación; el riesgo de clave durante examen queda cubierto por P-C1 |
| P-B7 | Medios públicos con `Cache-Control: public, immutable` (URLs con GUID) | ⏳ Las URLs no son adivinables; evaluar URLs firmadas para medios privados |
| P-B8 | Comprobantes de pago no ligados a la escuela que los subió | ⏳ Solo Admin/Escuela pueden descargarlos (prueba `Payment_receipts_require_admin_or_school`); nombres GUID |
| P-B9 | Error de librería devuelto al importar Word | ✅ Mensaje fijo salvo errores propios del parser |
| P-B10 | Sin `kid` ni rotación de clave JWT; access token válido hasta 60 min tras logout | ⏳ Mitigado: cada request revalida usuario activo y rol (`MustChangePasswordMiddleware`). Prueba `Valid_token_of_a_deactivated_user_is_401` |
| P-B11 | Si `Cors:Origins` se configura como `*` se abre a todo origen (sin credenciales) | ⏳ Configuración; no usar `*` en producción |
| P-B12 | Perfil de usuario y token de jugador de "100 Estudiantes" en `localStorage` | ⏳ Sin tokens de sesión (cookies HttpOnly); mover a `sessionStorage` en una próxima versión |
| P-B13 | `GET /api/public/instructors` (anónimo) lista a todos los instructores activos con id, nombre + inicial y escuela, sin que el instructor lo elija | ⏳ Sin correos ni datos de contacto (barrido `StudentPentestSweepTests`); decidir si debe ser opcional |
| P-B14 | Ranking "Todo Luz Verde" muestra id y nombre + inicial de estudiantes de otras escuelas | ⏳ Por diseño; existe la opción de ocultarse (`ranking/visibility`). Sin correos (prueba `Ranking_never_exposes_contact_data_and_ignores_foreign_group_ids`) |
| P-B15 | Hub `GameShowHub.JoinAsScreen(sessionId)` no exige sesión ni ser el dueño | ⏳ Solo recibe la vista pública de la partida (misma que M13). Revisado en código, sin prueba automática (requiere cliente SignalR) |

### INFORMATIVO (verificado correcto en Fase 2)

- **BOLA/IDOR:** ningún ID cambiado dio acceso a datos ajenos: intentos (`review`, `answer`, `finish`), grupos y miembros, preguntas privadas, miembros de otra escuela, exámenes, cursos, progreso, notificaciones, asistente.
- **Multi-tenant:** escuela A no lista ni edita miembros de escuela B; instructor no mete estudiantes de otra escuela en su grupo.
- **JWT:** rol editado, otra clave, `alg: none`, otra audiencia y token vencido → 401. Solo HS256; clave débil bloquea el arranque.
- **Mass assignment:** `role`, `schoolId`, `isPaid`, `isActive` extra en el cuerpo se ignoran; `score` en `finish` se ignora (nota calculada en servidor).
- **Pagos:** la escuela no puede activarse plan (403); precios y estados salen del servidor.
- **Cifrado de respuestas (`X-Cale-Wire`):** confirmado que **no es un control**: sin la cabecera la API responde JSON plano. La seguridad depende de la autorización en servidor.
- **Bundle:** sin source maps, sin `sourceMappingURL`, sin claves de API, cadenas de conexión ni llaves privadas.
- **Rate limit:** login 10/min por IP → 429 (prueba `Brute_force_login_gets_429`).
- **Logs:** no se registran contraseñas, JWT, refresh tokens ni API keys; se registran reutilización de refresh y CSRF bloqueado.

---

## Dependencias

| Paquete | Versión | Problema | Acción |
|---------|---------|----------|--------|
| Newtonsoft.Json (transitivo) | 10.0.3 | GHSA-5crp-9r3c-p9vr (High) | ✅ Fijado a 13.0.3 |
| System.Net.Http (transitivo) | 4.3.0 | GHSA-7jgj-8wvc-jh57 (High) | ✅ Fijado a 4.3.4 |
| System.Text.RegularExpressions (transitivo) | 4.3.0 | GHSA-cmhx-cq75-c4mj (High) | ✅ Fijado a 4.3.1 |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.6 | GHSA-2m69-gcr7-jv3q (High) | ⏳ 2.1.11 sigue afectada; la corrección exige saltar de versión mayor. Producción usa PostgreSQL, SQLite solo en desarrollo/pruebas |
| Angular | 18 | 8 avisos npm; Angular 18 fuera de soporte | ⏳ Migrar a Angular 20+ (cambio mayor, planificar) |
| Node.js | 20 | Fuera de soporte | ⏳ Usar Node 22 LTS en build |
| .NET | 8 | Soporte hasta nov-2026 | ⏳ Planificar .NET 10 LTS |

No se actualizaron automáticamente dependencias mayores para no romper compatibilidad.
