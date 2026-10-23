# Auditoría funcional — Luz Verde (CALE v5)

**Fecha:** 22 de octubre de 2026  
**Alcance:** repositorio `CALE-V5-main` (ASP.NET Core 8 + Angular 18)  
**Método:** inspección de código, rutas, servicios y documentación interna. No se ejecutó una demostración end-to-end contra producción en esta auditoría.  
**Criterio de estados:**

| Código | Significado |
|--------|-------------|
| **1** | Implementada y funcional (FE + BE conectados; uso operativo plausible) |
| **2** | Implementada parcialmente |
| **3** | Presente en código, sin integración o validación suficiente (pocas pruebas / sin nav / free desactiva) |
| **4** | Planeada o solo en documentación |
| **5** | No encontrada |

---

## 1. Resumen ejecutivo

Luz Verde es una plataforma de **formación vial y operación de escuelas (CEA)** con paneles por rol (Admin, Escuela, Instructor, Estudiante), simulacros del examen teórico, aula en vivo, agenda, expediente de aprendices, asistencia, teoría, práctica y un asistente de IA con límites diarios.

**Contexto comercial importante (comprobado en código):**

- `Access:FreeForAll` está habilitado por defecto: el cobro de membresía de escuela **no se exige** en el modo actual de piloto/gratis.
- Los precios históricos en código (`SchoolPlans.cs`) **no coinciden** con los precios de referencia de esta propuesta comercial ($500.000 / $5.000.000). Eso es una **decisión pendiente del propietario**.
- El sistema legal completo (términos definitivos, proveedor de pagos, reembolsos, retención de datos) sigue **aplazado**; ver `docs/legal/PENDIENTE_SISTEMA_LEGAL.md`.

---

## 2. Stack y arquitectura (verificado)

| Elemento | Evidencia |
|----------|-----------|
| Backend | ASP.NET Core 8, EF Core, JWT/cookies |
| Frontend | Angular 18 standalone, PWA |
| BD | PostgreSQL (prod), SQL Server / SQLite local |
| Roles | Admin, School, Teacher, Student |
| README | Formación vial; eslogan «Formación que impulsa tu camino.» |

---

## 3. Inventario por área

### 3.1 Administración y gestión de la escuela

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Panel inicio escuela (pendientes / operaciones) | **1** | `school-home.page.ts`, `GET api/school/dashboard` |
| Perfil / datos de la escuela | **1** | `school-info-form`, `GET/PUT api/school/profile` |
| Instructores y usuarios (invitar, aceptar/rechazar) | **1** | `school-users.page.ts`, invitaciones y `join-requests` |
| Crear cuentas de estudiante desde la escuela | **5** | Retirado (vinculación por consentimiento; PR #195 / docs legales) |
| Membresía / plan / comprobante de pago | **2** | FE+BE existen; **ocultos** si `FreeForAll`; sin pasarela electrónica |
| Aprobación de escuelas por administrador | **1** | Cola admin + tests de aprobación |
| Importar expedientes (Excel) | **1** | `school-import.page.ts`, `SchoolExcelImportService` |
| Exportar aprendices / pagos (Excel) | **1** | endpoints de export |
| Roles tipo «secretaria» con permisos limitados | **4** | Mencionado en `PLAN-ESCUELA-FACIL.md`, no implementado |
| UX «escuela fácil» (WhatsApp, barra móvil avanzada) | **4** | Plan documental, no producto |

### 3.2 Estudiantes y seguimiento académico

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Listado de aprendices + filtros | **1** | `school-apprentices.page.ts` |
| Expediente (dossier) | **1** | `school-apprentice-dossier.page.ts` |
| Abonos / saldo del aprendiz (cartera interna de la escuela) | **1** | Pagos en `SchoolApprenticeController` — **no** es cobro de Luz Verde |
| Progreso académico (vista escuela y estudiante) | **1** | APIs de progress |
| Autorizar examen teórico / práctica | **1** | UI + `TheoryExamAutoAuthorizer` |
| Solicitud / invitación a escuela | **1** | Perfil estudiante + APIs de membership |
| Cursos virtuales / malla | **1** | Rutas student/teacher/admin de cursos |
| Biblioteca jurídica (normas) | **1** | Panel estudiante e instructor |

### 3.3 Instructores, horarios y clases

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Agenda semanal (teoría, práctica, examen) | **1** | `school-agenda.page.ts` |
| Clases teóricas (salones, temas, reservas) | **1** / **3*** | Operativo en app; cobertura de pruebas limitada |
| Asistencia del día (teoría) | **1** | `school-attendance-today.page.ts` |
| Horarios de examen + cupos | **1** | `ExamScheduleController`, tests de scheduling |
| Control de examen en curso (check-in / no-show) | **1** | `school-exam-control.page.ts` |
| Clases de manejo / programación práctica | **1** / **3*** | FE+BE; poca cobertura de tests |
| Flota de vehículos del día | **1** / **3*** | `school-practical-fleet.page.ts` |
| Aula en vivo (SignalR) | **1** | Host instructor + join estudiante; tests Live |
| Presentaciones del instructor | **1** | CRUD, proyección, import |
| Grupos / aula (anuncios, materiales, actividades) | **1** | `GroupsController`, `ClassroomController` |
| Game show «100 Estudiantes Dijeron» | **1** | `GameShowController` |
| Marketplace de instructores (directorio público) | **2** | Directorio + rating + contacto; **no** es marketplace de la CEA ni cobro in-app |
| Clases particulares (ruta teacher) | **2** | Ruta existe; **no** está en el menú del instructor |

\*Marcadas **3** solo en el sentido de «validación formal / QA de campo incompleta», no de ausencia de pantalla.

### 3.4 Evaluaciones y simuladores

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Bancos de preguntas y exámenes | **1** | Admin/teacher; escuela en lectura |
| Simulador de examen (estudiante) | **1** | `simulator.page.ts` |
| Evaluaciones asignadas | **1** | `student-evaluations.page.ts` |
| Reto diario, ranking, logros, señal relámpago | **1** | Módulo Play |
| Resultados / informes (escuela e instructor) | **1** | Dashboards de results |

### 3.5 Pagos, planes y reportes (plataforma Luz Verde)

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Catálogo de planes en código | **2** | Mensual 150.000 / Semestral 800.000 / Anual 1.500.000 COP + cupos; **no alineado** con oferta comercial nueva |
| Solicitud de plan + subida de comprobante | **1** (cuando free off) | Transferencia + revisión admin |
| Pasarela de pagos / webhooks | **5** | No existe |
| Facturación electrónica / reembolsos | **4** / **5** | Pendiente legal |
| Métricas admin (actividad, uso app, ratings) | **1** | No incluyen consumo de IA |
| Tienda de cascos (WhatsApp) | **1** | Catálogo + CTA; no checkout in-app |

### 3.6 Inteligencia artificial

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Widget asistente (estudiante y escuela) | **1** | `assistant-widget`, `AssistantController` |
| Clave API solo en servidor | **1** | `Assistant:ApiKey`; no en frontend |
| Límite mensajes/usuario/día (default 20) | **1** | `AssistantOptions.DailyMessagesPerUser` |
| Límite requests/usuario, /escuela, /global | **1** | Defaults 40 / 60 / 140; día Colombia |
| Contadores durables (BD / multi-instancia) | **5** | Solo `IMemoryCache` |
| Conteo de tokens / costo por escuela | **5** | No implementado |
| Panel de consumo IA / overage / alertas 80–100 % | **5** | No implementado |
| Persistencia de conversaciones | **5** | Historial en cliente; no ledger de chats |
| Asistente para instructor/admin | **5** | Roles no soportados |

Detalle comercial: ver `03-modelo-comercial-ia.md`.

### 3.7 Notificaciones y soporte al usuario

| Funcionalidad | Estado | Evidencia / notas |
|---------------|--------|-------------------|
| Notificaciones in-app + preferencias | **1** | `NotificationsController` |
| Push web (opcional en perfil) | **1** | `PushController` |
| Entrada «Mensajes» en nav Escuela/Instructor | **5** | Solo estudiante la tiene en nav; ruta global existe |
| PQR y datos personales | **1** | `/pqr`, `/mis-pqr`, admin PQR (textos legales aún borrador) |
| Donaciones voluntarias | **1** | `/donaciones`, `/apoyar` |

---

## 4. Backend sin frontend o con acceso débil

| Caso | Nota |
|------|------|
| `/teacher/clases-particulares` | Página existe; no aparece en `nav.config` del Teacher |
| Membresía escuela | Completa, pero **redirigida/oculta** con acceso gratis |
| Contadores IA | Existen en memoria; no hay UI de administración de consumo LLM |

---

## 5. Lo que no debe venderse como listo

1. Pasarela de pagos automática ni facturación DIAN.  
2. Creación masiva de cuentas por la escuela (solo invitación/solicitud).  
3. Consumo de IA facturable por overage (medición comercial incompleta).  
4. Roles administrativos internos tipo secretaria.  
5. Integración WhatsApp operativa como canal de la escuela (solo plan documental / CTA de tienda).  
6. Textos legales definitivos (NIT, responsable, política de IA cerrada).  
7. Precios del código (150k/800k/1.5M) como oferta oficial sin decisión explícita.

---

## 6. Lo que sí puede demostrarse hoy (demo B2B)

Orden sugerido: panel escuela (agenda → aprendices/expediente → asistencia → teoría/examen → práctica) → cuenta instructor (aula en vivo / presentación) → cuenta estudiante (simulador, progreso, agendar) → asistente IA (si hay `ApiKey` configurada).

---

## 7. Limitaciones de esta auditoría

- No se midió rendimiento bajo carga real de un CEA.  
- No se verificó el contenido CMS de la página de inicio en producción (mensaje «Cargando…» es loading temporal con fallback local).  
- No se inspeccionaron secretos ni configuración de producción.  
- Estados «1» significan «implementado en el producto», no «certificado en campo con clientes».
