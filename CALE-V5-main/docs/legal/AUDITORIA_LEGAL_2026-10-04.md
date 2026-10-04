# Auditoría previa del sistema legal (punto 28 del encargo)

**Fecha:** 4 oct 2026. **Alcance:** solo lectura del código; no se cambió nada.
Encargo completo y datos pendientes: `PENDIENTE_SISTEMA_LEGAL.md`.

Clasificación: CRÍTICO · ALTO · MEDIO · BAJO · INFORMATIVO.

## 1. Documentos legales y aceptación

| Hallazgo | Nivel | Dónde |
|---|---|---|
| No hay términos, política de datos, privacidad, cookies ni política de IA. No hay rutas `/legal/*` ni enlaces en el pie de página. | CRÍTICO | `frontend/src/app/app.routes.ts`, `features/public/public-shell.component.ts` |
| Los tres registros (estudiante, instructor, escuela) no tienen casillas de aceptación ni autorización de datos. El backend no guarda ninguna aceptación. | CRÍTICO | `features/auth/pages/register*.page.*`, `AuthDtos.cs` (`RegisterRequest`, `RegisterSchoolRequest`) |
| No existen tablas de documentos legales, versiones ni aceptaciones. | CRÍTICO | Sin coincidencias en el modelo |
| Cuentas creadas sin que el titular vea nada: alta de instructores y escuelas por el admin, importación de aprendices desde Excel por la escuela. | ALTO | `AuthDtos.cs` (`CreateTeacherRequest`, `CreateSchoolRequest`), `SchoolExcelImportService.cs` |
| El correo de verificación no identifica al responsable ni enlaza la política de datos. No existe recuperación de contraseña. | MEDIO | `EmailConfirmationService.cs` |
| Textos de la página de inicio que prometen resultados ("obtén tu licencia", "tipo examen oficial") sin aviso de que no se garantiza la aprobación. | MEDIO | `features/public/landing.page.ts` |
| Ya existe un aviso correcto: el resumen de progreso "No son certificados oficiales ni tienen validez legal". | INFORMATIVO | `student-certificates.page.ts` |

## 2. Menores de edad

| Hallazgo | Nivel |
|---|---|
| No se pide fecha de nacimiento ni edad: la app no puede saber si un usuario es menor. No hay representante legal, autorización ni trazabilidad. | CRÍTICO |
| La foto de perfil se toma con la cámara ("Mira a la cámara"): normalmente es una imagen del rostro, también de menores. | ALTO |

## 3. Datos personales que se tratan hoy

- **Usuario** (`User.cs`): nombre, correo, contraseña con hash, foto, fecha de creación y último acceso.
- **Expediente del aprendiz** (`SchoolApprenticeProfile`, lo llena la escuela): tipo y número de documento, celular, dirección, correo de contacto, valores pagados y saldos, medios de pago, recibos, PIN de matrícula, estado RUNT y notas libres. Se exporta a Excel.
- **Escuela** (`SchoolProfile`): razón social, NIT, correo de facturación, teléfono, dirección y comprobantes de pago.
- **Uso:** resultados de exámenes, progreso, juegos y ranking; suscripciones push con el user agent.
- **No hay** datos de salud, biométricos ni fecha de nacimiento.

| Hallazgo | Nivel | Dónde |
|---|---|---|
| El asistente de IA envía al proveedor externo (por defecto GitHub Models, `openai/gpt-4o-mini`) el número de documento, el celular y los saldos del aprendiz cuando una escuela pregunta por un estudiante. No hay aviso al usuario. | ALTO | `Services/Assistant/AssistantToolbox.cs` (`StudentDetailAsync`), `AssistantService.cs` |
| Los logs del login guardan el correo en texto plano, también el de intentos fallidos. | MEDIO | `LoginUserHandler.cs` |
| Datos de pago de Luz Verde ficticios en el código ("CALE Formación Vial SAS", NIT 901.000.000-1, `pagos@cale.local`) que se muestran a las escuelas. | ALTO | `SchoolProfile.cs` (datos bancarios), `school-membership.page.ts` |

## 4. Cuentas, suspensión y derechos del titular

| Hallazgo | Nivel | Dónde |
|---|---|---|
| Suspender un usuario solo cambia `IsActive`; no guarda motivo, fecha, administrador, evidencia, duración ni revisión. El usuario solo ve "Tu cuenta está inactiva". | ALTO | `User.cs`, `SetUserActiveHandler.cs`, `map-api-error.ts` |
| Las escuelas sí guardan motivo e historial al suspenderse (`MembershipEvent`): sirve de modelo. | INFORMATIVO | `SchoolProfile.cs`, `MembershipEvent.cs` |
| El usuario no puede descargar ni eliminar sus datos. El borrado del admin elimina la fila del usuario y deja sus datos en otras tablas (no hay llaves foráneas a `Usuarios`). No existe anonimización ni borrado lógico. | ALTO | `DeleteUserHandler.cs` |
| No hay canal de PQR ni de derechos del titular. "Solicitudes" (pregunta, idea, reporte) puede ampliarse, pero exige iniciar sesión y borra la solicitud si se retira. El contacto público solo tiene enlaces de correo y teléfono. | ALTO | `UserRequest.cs`, `UserRequestService.cs`, `public-contact.page.ts` |
| No hay limpieza automática de ningún dato (tokens vencidos, notificaciones, comprobantes, solicitudes). | MEDIO | Servicios en segundo plano |
| No existe proceso para cuando una escuela termina: exportar, transición, conservación y eliminación. Tampoco se puede desvincular a un estudiante de una escuela. | MEDIO | `User.LeaveSchool()` sin uso |

## 5. Pagos

| Hallazgo | Nivel |
|---|---|
| Las escuelas pagan por transferencia, suben comprobante y el admin activa. El servidor decide plan, precio y vigencia (bien hecho). | INFORMATIVO |
| El modelo pago está apagado (`Access:FreeForAll=true`). Los estudiantes nunca pagan; las donaciones son un QR de Nequi/Bre-B sin registro. | INFORMATIVO |
| Planes con precios en el código (mensual 150.000, semestral 800.000, anual 1.500.000 COP) que el propietario debe confirmar. No hay política de reembolsos ni pasarela. | PENDIENTE |

## 6. Contenido de terceros y licencias

| Hallazgo | Nivel |
|---|---|
| 20 videos de la ANSV (y 1 del Ministerio de Transporte con la ANSV) descargados y alojados en la app, con crédito pero sin licencia o permiso registrado. | ALTO |
| Texto del Código de Tránsito extraído literalmente de transiteca.app con un script. La ley es pública, pero la compilación de un tercero puede estar protegida. | ALTO |
| 8 videos (`clase-alcoholemia-0..4`, `experimento-30-kmh`, `exceso-velocidad`, `puntos-ciegos`), 13 fotos de cursos, el logo y los sonidos sin fuente registrada. | MEDIO |
| Las imágenes "reales" de señales salen de exámenes de instructores; su origen no está registrado. | MEDIO |
| El juego "100 Estudiantes Dijeron" imita el nombre de un formato de TV conocido. | BAJO |
| No hay archivo LICENSE ni aviso de derechos reservados en la app (solo un mensaje en la consola). | MEDIO |
| Fuentes DM Sans (licencia OFL) e íconos estilo Lucide (ISC) sin crédito. | BAJO |
| Las preguntas de los bancos citan su fuente (ANSV, Ley 769, Manual de Señalización) y dicen "redacción propia". | INFORMATIVO |

## 7. Proveedores que reciben datos

Render (hosting y base de datos PostgreSQL), servidor SMTP (opcional), servicios push de los navegadores,
proveedor de IA (GitHub Models por defecto), Google Fonts y YouTube (reproductor sin cookies). No hay analítica
ni rastreo de errores externos.

## 8. Lo que ya sirve como base

- Vinculación escuela–usuario solo con consentimiento de ambas partes (`SchoolJoinRequest`).
- Directorio de instructores por consentimiento explícito (`InstructorListings`), aunque sin historial.
- Ranking con opción de ocultarse (visible por defecto).
- Historial de membresías de escuelas con actor y motivo (`MembershipEvent`).
- Tokens de sesión con hash; claves de IA solo en el servidor; límites de uso; cabeceras de seguridad.
