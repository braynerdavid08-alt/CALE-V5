# Hardening aplicado — Luz Verde (CALE-V5)

Complementa `SECURITY_AUDIT.md`. Describe **qué se cambió, dónde y cómo operarlo**.

## 1. Configuración obligatoria en Render

| Variable | Valor | Por qué |
|----------|-------|---------|
| `Jwt__Key` | Cadena aleatoria de **≥ 32 caracteres**, sin `CHANGE-ME` | Si falta o es débil, la app **no arranca** fuera de Development. Si la clave actual se compartió alguna vez fuera de Render, **rótala** (todas las sesiones se cerrarán, es lo esperado). |
| `Security__Csp__Mode` | `enforce` (por defecto) · `report-only` · `off` | Interruptor de emergencia: si la CSP bloquea algo legítimo, pon `report-only` sin redesplegar código. |
| `RateLimiting__GlobalPerIpPerMinute` | `3000` por defecto | Techo de peticiones `/api` por IP. Alto porque un salón entero comparte la IP del colegio. |
| `Uploads__DefaultMaxBodyMegabytes` | `30` por defecto | Tamaño máximo de cuerpo para endpoints sin límite propio. |
| `Access__FreeForAll` | Hoy `true` | Decisión pendiente (H8): ponerlo en `false` al terminar el piloto. |

Generar una clave segura (PowerShell):

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

## 2. Autenticación y sesiones

- **JWT:** firma obligatoria, solo `HS256`, expiración obligatoria, tolerancia de reloj de 30 s (`ServiceCollectionExtensions.AddCaleAuth`).
- **Revalidación por petición** (`MustChangePasswordMiddleware`): en cada llamada autenticada a `/api` se consulta el usuario; si no existe, está inactivo o su rol cambió → 401 `session_revoked` y aviso en logs `Security: rejected token ...`.
- **Revocación de refresh tokens** al: desactivar (`SetUserActiveHandler`), borrar (`DeleteUserHandler`), cambiar rol/contraseña/correo desde Admin (`UpdateUserHandler`) y cambiar contraseña desde la escuela (`UpdateSchoolMemberHandler`).
- **Logout** (`POST /api/auth/logout`) ya no exige un access token vigente: revoca el refresh de la cookie y borra las cookies.
- **Auditoría:** las acciones administrativas sobre usuarios quedan en logs con el prefijo `Audit:` (actor, objetivo, cambio).

## 3. Autorización (control de acceso en servidor)

- **Escuelas ↔ cuentas:** `User.CreatedBySchoolId` + `CredentialsManagedBy`. Columna `EscuelaCreadoraId` creada al arrancar por `UserCreatorSchemaGuard` (Postgres y SQLite). Relleno inicial único: se marcan como creadas por la escuela las cuentas que no tienen un evento `MemberAttached`.
- **Preguntas:** filtro por propietario del banco en listado y detalle.
- **Simulacros:** 10–50 preguntas, 5–120 min, modo forzado a práctica, examen oficial no mezclable.
- **Grupos:** un instructor solo agrega estudiantes de su misma escuela.
- **Aula en vivo:** bancos visibles para el host y presentaciones a las que tiene acceso.
- **100 Estudiantes Dijeron:** la configuración global es solo de Admin; en la pantalla de configuración los instructores ven un aviso y ajustan cada partida.
- **Comprobantes de pago:** solo Admin/Escuela; URL emitida por nuestro endpoint.
- **Prueba automática** (`AuthorizationSurfaceTests`): toda ruta `api/admin*` exige rol Admin y **todo endpoint declara `[Authorize]` o `[AllowAnonymous]` explícito**. Un endpoint nuevo sin decisión de acceso rompe el build de pruebas.

## 4. Subida y entrega de archivos

- `MediaSniffer` identifica el tipo real por *magic bytes* (JPEG, PNG, GIF, WebP, BMP, PDF, MP4/MOV/M4A/M4V, WebM, AVI, WAV, OGG, MP3). Lo que no se reconoce se rechaza; SVG ya no se acepta.
- `SafeMediaResponse` entrega con `X-Content-Type-Options: nosniff`, CSP `sandbox`, y como descarga si no es multimedia/PDF.
- Límite de cuerpo por defecto 30 MB; cada endpoint de subida declara el suyo.

## 5. Cabeceras del navegador (`SecurityHeadersMiddleware`)

`Content-Security-Policy` (solo scripts propios, sin `eval`, `object-src 'none'`, `frame-ancestors 'self'`), `X-Frame-Options: SAMEORIGIN`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy` (cámara solo propia; micrófono, geolocalización y pagos desactivados), `Cross-Origin-Opener-Policy`, y `Strict-Transport-Security` sobre HTTPS.

Para que la CSP funcione sin `unsafe-inline` en scripts, el script de tema inline de `index.html` se movió a `frontend/public/theme-init.js`. En producción no se generan *source maps*.

## 6. Rate limiting

El limitador se ejecuta **después** de la autenticación para poder limitar por usuario.

| Política | Clave | Límite |
|----------|-------|--------|
| Global `/api` | IP | 3000 / min (configurable) |
| `login` | IP | 10 / min |
| `register` | IP | 20 / 10 min |
| `email-code` | IP | 10 / 10 min |
| `refresh` (refresh y logout) | IP | 60 / min |
| `public-join` (unirse a aula en vivo y a 100 Dijeron) | IP | 120 / min |
| `change-password` | usuario | 10 / 10 min |
| `exam-start` | usuario | 30 / 10 min |
| `exam-review` | usuario | 60 / 10 min |
| `uploads` (todas las subidas) | usuario | 40 / 10 min |
| `assistant` | usuario | 20 / min |
| `client-errors` | IP | 30 / min |

Respuesta: 429 con `detail: too_many_requests`.

## 7. Errores y datos sensibles

- Fuera de Development, los errores 500 devuelven un mensaje genérico y el `traceId`; el detalle queda solo en los logs.
- Exportaciones CSV neutralizan fórmulas (`CsvCell.Escape`); al reimportar un CSV de 100 Dijeron se revierte (`CsvCell.Unescape`).
- Página "Nosotros": el HTML pasa por el saneador de Angular.

## 8. Dependencias

Fijadas en `Cale.BuildingBlocks.Infrastructure.csproj`: `Newtonsoft.Json 13.0.3`, `System.Net.Http 4.3.4`, `System.Text.RegularExpressions 4.3.1`. Pendiente: SQLite nativo (solo dev), Angular 18 → 20+, Node 20 → 22, .NET 8 → 10.

## 9. Lo que NO es seguridad (y se mantiene solo como disuasión)

- Cifrado de respuestas con clave en el bundle (`WireEncryptionMiddleware`).
- Bloqueo de F12, clic derecho y selección (`content-guard.service.ts`).
- Ofuscación/minificación del JavaScript.

Cualquier usuario con sesión puede ver lo que su rol tiene permitido ver. La protección real es que **el servidor solo entrega a cada rol lo que le corresponde**.

## 10. Verificación tras el despliegue

1. `GET /api/health` → 200 con cabeceras `Content-Security-Policy` y `X-Frame-Options`.
2. Abrir la app, iniciar sesión con cada rol y navegar: si algo no carga y la consola muestra "Refused to ... Content Security Policy", poner `Security__Csp__Mode=report-only` y avisar.
3. `GET /api/admin/users` sin sesión → 401.
4. 11 intentos de login fallidos seguidos → el 11.º devuelve 429.
