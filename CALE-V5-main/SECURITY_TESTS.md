# Pruebas de seguridad — Luz Verde (CALE-V5)

Pruebas automáticas en `tests/Cale.UnitTests/Security/SecurityHardeningTests.cs` (47 casos) más las existentes `CourseVideoGateMiddlewareTests`, `WireEncryptionMiddlewareTests`, `Catalog/QuestionOwnershipTests`, `RefreshTokenStoreTests`, `PasswordHasherTests`, `RawSqlLiteralTests`.

Ejecutar:

```powershell
dotnet test tests/Cale.UnitTests
dotnet test tests/Cale.ArchitectureTests
```

Resultado actual: **294/294 unitarias y 4/4 de arquitectura en verde.**

## Matriz de ataques

"Prueba" indica la prueba automática que demuestra que el ataque falla. "Manual" indica verificación hecha contra la API local.

| # | Ataque | Resultado esperado | Cobertura |
|---|--------|--------------------|-----------|
| 1 | Llamar a endpoints sin sesión | 401 | `AuthorizationSurfaceTests.Every_endpoint_declares_an_explicit_access_decision`; manual: `GET /api/admin/users` → 401 |
| 2 | Estudiante/instructor llamando a rutas de Admin | 403 | `AuthorizationSurfaceTests.Every_admin_route_requires_the_admin_role` |
| 3 | Instructor cambiando la configuración global de 100 Dijeron | 403 | `AuthorizationSurfaceTests.Global_game_show_settings_are_admin_only` |
| 4 | Escuela tomando el control de una cuenta vinculada (cambiar contraseña/correo) | 403 `credentials_not_managed` | `AccountTakeoverTests` (3 casos) |
| 5 | Leer preguntas de un banco privado ajeno (IDOR) | 404 | `PrivateBankVisibilityTests`; `Catalog/QuestionOwnershipTests` |
| 6 | Usar el JWT de un usuario desactivado | 401 `session_revoked` | `SessionRevocationTests.Token_of_a_deactivated_user_is_rejected` |
| 7 | Usar el JWT de un usuario borrado | 401 | `SessionRevocationTests.Token_of_a_deleted_user_is_rejected` |
| 8 | Usar un JWT emitido antes de un cambio de rol (escalada) | 401 | `SessionRevocationTests.Token_issued_before_a_role_change_is_rejected` |
| 9 | Saltarse el cambio obligatorio de contraseña | 403 `password_change_required` | `SessionRevocationTests.Forced_password_change_blocks_the_api` |
| 10 | Subir HTML con extensión `.png` | Rechazado | `UploadSniffingTests.Html_svg_and_executables_disguised_as_images_are_rejected` |
| 11 | Subir SVG con `onload` | Rechazado | Ídem |
| 12 | Subir un ejecutable disfrazado | Rechazado | Ídem |
| 13 | Hacer que un archivo almacenado se ejecute como página | Se sirve como descarga, `nosniff`, CSP `sandbox` | `UploadSniffingTests.Stored_active_content_is_served_as_a_download_never_inline` |
| 14 | Descargar comprobantes de pago sin sesión | 401 | `ReceiptGateTests.Anonymous_user_cannot_download_payment_receipts` |
| 15 | Estudiante/instructor descargando comprobantes | 403 | `ReceiptGateTests.Students_and_teachers_cannot_download_payment_receipts` |
| 16 | Registrar como comprobante una URL externa, `javascript:` o con `../` | Rechazado | `PaymentProofTests.Foreign_or_crafted_receipt_urls_are_rejected` |
| 17 | Ver vídeos de cursos sin sesión | 401 | `CourseVideoGateMiddlewareTests` |
| 18 | Provocar un error para leer cadenas de conexión | Mensaje genérico en producción | `ErrorDisclosureTests.Production_errors_do_not_leak_internal_messages` |
| 19 | Inyectar `<script>` (XSS) / enmarcar la app (clickjacking) | Bloqueado por CSP y `X-Frame-Options` | `SecurityHeadersTests` |
| 20 | Inyección de fórmulas en CSV exportado | Celda neutralizada con `'` | `CsvInjectionTests` |
| 21 | Fuerza bruta de login | 429 tras 10 intentos/min | Manual: 10 × 401 y luego 429 |
| 22 | Pedir 5 000 preguntas en un simulacro / `Mode=Official` | Limitado a 50, modo práctica | Revisión de código en `StartExamHandler` (constantes y `Math.Clamp`); límite `exam-start` por usuario |
| 23 | Instructor agregando estudiantes de otra escuela a su grupo | 403 `group_wrong_school` | Revisión de código en `GroupCommandHandler.AddMemberAsync` |
| 24 | Host de aula en vivo usando banco/presentación ajenos | 404 / 403 | Revisión de código en `LiveSessionHandler.ResolveBankIdsAsync` y `LiveController.EnsurePresentationAccessAsync` |

## Verificación manual realizada (API local, entorno Development)

```
GET  /api/health                         -> 200 (X-Frame-Options: SAMEORIGIN)
GET  /api/admin/users        (anónimo)    -> 401
GET  /uploads/receipts/<id>.png (anónimo) -> 401
POST /api/auth/logout        (anónimo)    -> 204
POST /api/auth/login × 12    (clave mala) -> 401 ×10, 429 ×2
```

Build de producción del frontend: correcto; sin scripts inline, sin manejadores `on*=` y sin `eval`/`new Function` en los bundles (compatibles con la CSP).

## Pendiente de automatizar

- Pruebas de integración HTTP completas (`WebApplicationFactory`) para los casos 22–24, que hoy se validan por revisión de código.
- Prueba de carga del límite global por IP.
