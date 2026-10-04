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
| M11 | Puntaje de juegos (señales/duelos) lo envía el cliente | `Api/Services/Play/PlayService.cs` | ⏳ Solo afecta rankings de gamificación, no certificaciones |
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
| L2 | No hay detección de reutilización de refresh token (rotación con gracia de 60 s) | ⏳ |
| L3 | `POST /api/auth/logout` exigía token válido: con el access token vencido no se revocaba el refresh | ✅ Ahora es anónimo y revoca usando la cookie |
| L4 | CSRF: cookie de acceso `SameSite=Lax` | ⚠️ Mitigado (API JSON, CORS restringido, refresh `Strict`) |
| L5 | En el flujo de autoconfirmación el JWT puede quedar en `localStorage` | ⏳ |
| L6 | CORS con dominios de ejemplo en `appsettings` | ⏳ Revisar `Cors__AllowedOrigins` en Render |
| L7 | Cuotas del asistente IA en memoria (se reinician con cada despliegue) | ⚠️ Añadido límite 20/min por usuario |
| L8 | Endpoint de suscripción push acepta URLs arbitrarias (SSRF limitado) | ⏳ |
| L9 | Cambio de correo sin verificación del nuevo correo | ⏳ |
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
